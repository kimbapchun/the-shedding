using System;
using Unity.Netcode;
using UnityEngine;

namespace TheShedding.Network
{
    /// <summary>
    /// 연결 흐름의 단일 진입점.
    ///
    /// UI를 비롯한 다른 코드는 NetworkManager를 직접 호출하지 않고 이 클래스만 사용한다.
    /// 이렇게 한 겹 감싸는 이유는 두 가지다.
    ///  1) 나중에 재연결·타임아웃·에러 표시 같은 로직이 붙을 자리가 여기 한 곳으로 모인다.
    ///     (버튼에 StartHost()를 직접 연결해두면 그때 UI 코드까지 전부 고쳐야 한다)
    ///  2) UI는 "지금 어떤 상태인지"만 알면 되고, NGO API를 몰라도 된다.
    ///
    /// 반드시 NetworkManager와 같은 GameObject에 붙일 것.
    /// NetworkManager가 자기 자신에게 DontDestroyOnLoad를 걸기 때문에(NetworkManager.cs:1097)
    /// 같은 오브젝트에 있으면 이 컴포넌트도 씬 전환 후 함께 살아남는다.
    /// </summary>
    [RequireComponent(typeof(NetworkManager))]
    public class ConnectionManager : MonoBehaviour
    {
        /// <summary>
        /// 씬 전환 후에도 UI 등에서 접근해야 하므로 싱글톤으로 노출한다.
        /// (협업 규칙 B의 "플레이어 상태를 static으로 두지 않기"는 플레이어 상태에 대한 규칙이고,
        ///  매니저 단일 인스턴스 참조는 여기에 해당하지 않는다)
        /// </summary>
        public static ConnectionManager Instance { get; private set; }

        [Tooltip("Connecting 상태가 이 시간을 넘기면 실패로 처리한다.")]
        [SerializeField] private float connectTimeoutSeconds = 10f;

        /// <summary>
        /// 연결 상태가 바뀔 때마다 발행. UI는 이 이벤트만 구독한다.
        /// 상태를 매 프레임 폴링하지 않아도 되도록 이벤트로 밀어준다.
        /// </summary>
        public event Action<ConnectionState> OnStateChanged;

        /// <summary>
        /// 연결 실패 또는 예기치 않은 끊김. 인자는 사용자에게 그대로 보여줄 사유 문자열.
        /// OnStateChanged(Disconnected)와 함께 발행되지만, "내가 끊은 것"과
        /// "끊겨버린 것"을 UI가 구분할 수 있도록 별도 이벤트로 둔다.
        /// </summary>
        public event Action<string> OnConnectionFailed;

        public ConnectionState State { get; private set; } = ConnectionState.Disconnected;

        private NetworkManager m_NetworkManager;

        /// <summary>
        /// 연결 시도 마감 시각. 남은 시간을 깎아나가는 대신 "끝나는 시점"을 저장한다.
        /// (협업 규칙 B — 나중에 서버 시간 기준으로 바꾸기 쉽다)
        /// </summary>
        private float m_ConnectDeadline;

        private void Awake()
        {
            // 씬을 다시 로드하는 등의 이유로 두 번째 인스턴스가 생기면 자기 자신만 제거한다.
            // Destroy(gameObject)가 아니라 Destroy(this)인 이유: 이 오브젝트에는
            // NetworkManager도 같이 붙어 있어서, 오브젝트째로 지우면 안 된다.
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            m_NetworkManager = GetComponent<NetworkManager>();
        }

        private void OnEnable()
        {
            // NGO가 알려주는 연결 생애주기 콜백들.
            // 호스트/클라이언트에 따라 발화하는 콜백이 다르다(아래 핸들러 주석 참고).
            m_NetworkManager.OnServerStarted += HandleServerStarted;
            m_NetworkManager.OnClientConnectedCallback += HandleClientConnected;
            m_NetworkManager.OnClientStopped += HandleClientStopped;
            m_NetworkManager.OnServerStopped += HandleServerStopped;
        }

        private void OnDisable()
        {
            // 플레이 종료·씬 언로드 시 NetworkManager가 먼저 파괴될 수 있다.
            // 그 상태에서 이벤트 해제를 시도하면 NullReference가 나므로 방어한다.
            if (m_NetworkManager == null)
            {
                return;
            }

            m_NetworkManager.OnServerStarted -= HandleServerStarted;
            m_NetworkManager.OnClientConnectedCallback -= HandleClientConnected;
            m_NetworkManager.OnClientStopped -= HandleClientStopped;
            m_NetworkManager.OnServerStopped -= HandleServerStopped;
        }

        private void Update()
        {
            // 타임아웃 감시는 Connecting 상태일 때만 의미가 있다.
            if (State != ConnectionState.Connecting)
            {
                return;
            }

            // StartClient()는 "연결 시도를 시작했다"는 뜻으로 true를 돌려줄 뿐,
            // 서버가 실제로 없어도 실패를 즉시 알려주지 않는다(UDP라 응답이 없을 뿐이다).
            // 그래서 직접 시간을 재서 끊어줘야 무한 "연결 중..."에 빠지지 않는다.
            //
            // Time.time이 아니라 realtimeSinceStartup을 쓰는 이유:
            // timeScale = 0으로 게임을 멈춰도 네트워크 타임아웃은 흘러가야 한다.
            if (Time.realtimeSinceStartup >= m_ConnectDeadline)
            {
                // 시도 중이던 연결을 정리해야 다음 시도가 깨끗하게 시작된다.
                m_NetworkManager.Shutdown();
                Fail("연결 시간이 초과되었습니다. 호스트가 실행 중인지 확인하세요.");
            }
        }

        // ── 외부 진입점 ──────────────────────────────────────────────────

        /// <summary>호스트(서버 + 클라이언트 겸용)로 시작한다.</summary>
        public void StartHost()
        {
            if (!CanStart())
            {
                return;
            }

            BeginConnecting();

            // StartHost()는 소켓 바인딩까지 동기적으로 처리하므로 여기서 바로 실패를 알 수 있다.
            // (예: 7777 포트를 다른 프로세스가 이미 쓰고 있는 경우)
            if (!m_NetworkManager.StartHost())
            {
                Fail("호스트를 시작하지 못했습니다. 포트가 이미 사용 중일 수 있습니다.");
            }
        }

        /// <summary>설정된 주소(UnityTransport의 Address/Port)로 접속을 시도한다.</summary>
        public void StartClient()
        {
            if (!CanStart())
            {
                return;
            }

            BeginConnecting();

            // 여기서의 false는 "시도조차 못 했다"는 뜻이다(전송 계층 초기화 실패 등).
            // 서버가 없어서 실패하는 경우는 여기가 아니라 Update()의 타임아웃이 잡는다.
            if (!m_NetworkManager.StartClient())
            {
                Fail("클라이언트를 시작하지 못했습니다.");
            }
        }

        /// <summary>
        /// 사용자가 의도적으로 연결을 끊을 때 호출한다.
        /// 실패가 아니므로 OnConnectionFailed는 발행하지 않는다.
        /// </summary>
        public void Disconnect()
        {
            if (State == ConnectionState.Disconnected)
            {
                return;
            }

            // 상태를 먼저 Disconnected로 바꿔두면, Shutdown() 때문에 뒤이어 호출될
            // HandleClientStopped/HandleServerStopped가 "이미 정리됨"으로 보고 그냥 빠져나간다.
            // (그래서 사용자가 직접 끊은 경우엔 실패 메시지가 뜨지 않는다)
            m_NetworkManager.Shutdown();
            SetState(ConnectionState.Disconnected);
        }

        // ── NetworkManager 콜백 ──────────────────────────────────────────

        /// <summary>
        /// 서버가 뜬 직후 호출된다. 호스트로 시작한 경우가 여기에 해당한다.
        /// 호스트는 자기 자신에게 접속하는 개념이라 OnClientConnectedCallback을 기다리지 않고
        /// 서버가 뜬 시점을 곧바로 "연결 완료"로 본다.
        /// </summary>
        private void HandleServerStarted()
        {
            if (m_NetworkManager.IsHost)
            {
                SetState(ConnectionState.Connected);
            }
        }

        /// <summary>
        /// 누군가 서버에 접속할 때마다 호출된다. 호스트 입장에서는 다른 플레이어가
        /// 들어올 때도 매번 불린다. 여기서 관심 있는 건 "내가 접속에 성공했는가"뿐이므로
        /// 내 ID일 때만 처리한다.
        /// </summary>
        private void HandleClientConnected(ulong clientId)
        {
            if (clientId == m_NetworkManager.LocalClientId)
            {
                SetState(ConnectionState.Connected);
            }
        }

        /// <summary>
        /// 클라이언트가 멈췄을 때 호출된다. wasHost는 "멈춘 주체가 호스트였는지"를 뜻한다.
        /// 호스트를 끄면 클라이언트와 서버가 둘 다 멈춰서 이 콜백과 HandleServerStopped가
        /// 모두 불리는데, 두 번 처리하지 않도록 호스트인 경우는 서버 쪽에 넘긴다.
        /// </summary>
        private void HandleClientStopped(bool wasHost)
        {
            if (wasHost)
            {
                return;
            }

            HandleStopped();
        }

        /// <summary>서버가 멈췄을 때 호출된다. 호스트를 끈 경우도 포함된다.</summary>
        private void HandleServerStopped(bool wasHost)
        {
            HandleStopped();
        }

        /// <summary>
        /// 끊김 처리의 공통 경로.
        /// 내가 Disconnect()로 끊은 경우엔 상태가 이미 Disconnected라 여기서 걸러진다.
        /// 즉 여기까지 온 건 "의도하지 않게 끊긴" 경우뿐이다.
        /// </summary>
        private void HandleStopped()
        {
            if (State == ConnectionState.Disconnected)
            {
                return;
            }

            // 서버가 연결을 거부하거나 강제로 끊을 때 사유를 실어 보낼 수 있다.
            // 5단계(재연결)에서 연결 승인 로직을 붙이면 여기에 값이 들어온다.
            var reason = m_NetworkManager.DisconnectReason;
            Fail(string.IsNullOrEmpty(reason) ? "연결이 끊어졌습니다." : reason);
        }

        // ── 내부 ─────────────────────────────────────────────────────────

        /// <summary>
        /// 연결을 새로 시작해도 되는 상태인지 확인한다.
        /// 버튼 연타나 이미 연결된 상태에서의 재요청을 막는다.
        /// </summary>
        private bool CanStart()
        {
            if (State == ConnectionState.Disconnected)
            {
                return true;
            }

            Debug.LogWarning($"[ConnectionManager] 이미 {State} 상태이므로 요청을 무시합니다.");
            return false;
        }

        private void BeginConnecting()
        {
            m_ConnectDeadline = Time.realtimeSinceStartup + connectTimeoutSeconds;
            SetState(ConnectionState.Connecting);
        }

        /// <summary>연결을 실패로 마무리한다. 상태 변경과 실패 사유를 함께 알린다.</summary>
        private void Fail(string reason)
        {
            SetState(ConnectionState.Disconnected);
            OnConnectionFailed?.Invoke(reason);
        }

        /// <summary>
        /// 상태 변경의 유일한 통로. 실제로 값이 바뀔 때만 이벤트를 발행해서
        /// 같은 상태가 연속으로 통지되는 일을 막는다.
        /// </summary>
        private void SetState(ConnectionState next)
        {
            if (State == next)
            {
                return;
            }

            State = next;
            OnStateChanged?.Invoke(next);
        }
    }
}
