using System;
using System.Text;
using Unity.Netcode;
using UnityEngine;

namespace TheShedding.Network
{
    /// <summary>
    /// 연결 흐름의 단일 진입점. UI는 NetworkManager 대신 이 클래스만 사용한다.
    /// NetworkManager와 같은 GameObject에 붙여야 씬 전환에도 함께 살아남는다.
    /// </summary>
    [RequireComponent(typeof(NetworkManager))]
    public class ConnectionManager : MonoBehaviour
    {
        public static ConnectionManager Instance { get; private set; }

        [Tooltip("Connecting 상태가 이 시간을 넘기면 실패로 처리한다.")]
        [SerializeField] private float connectTimeoutSeconds = 10f;

        /// <summary>연결 상태가 실제로 바뀔 때만 발행.</summary>
        public event Action<ConnectionState> OnStateChanged;

        /// <summary>실패·예기치 않은 끊김. 인자는 화면에 그대로 띄울 사유.</summary>
        public event Action<string> OnConnectionFailed;

        public ConnectionState State { get; private set; } = ConnectionState.Disconnected;

        /// <summary>UI가 NGO를 참조하지 않고도 호스트 전용 기능을 판단하도록 대신 노출한다.</summary>
        public bool IsHost => m_NetworkManager != null && m_NetworkManager.IsHost;

        /// <summary>
        /// 이 실행 인스턴스를 식별하는 값. 접속할 때 서버로 보내 승인 심사에 쓰인다.
        /// 앱을 다시 켜면 새로 발급되므로 "실행 중 끊겼다 다시 붙는" 경우만 같은 사람으로 알아본다.
        /// NGO의 clientId는 재접속하면 바뀌기 때문에 별도 식별자가 필요하다.
        /// </summary>
        public string LocalPlayerId { get; private set; }

        private NetworkManager m_NetworkManager;

        /// <summary>연결 시도 마감 시각 (협업 규칙 B — 남은 시간이 아니라 끝나는 시점).</summary>
        private float m_ConnectDeadline;

        private string m_LastFailureReason;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // 이 오브젝트에는 NetworkManager도 붙어 있으므로 오브젝트째 지우면 안 된다.
                Destroy(this);
                return;
            }

            Instance = this;
            m_NetworkManager = GetComponent<NetworkManager>();
            LocalPlayerId = Guid.NewGuid().ToString("N");
        }

        private void OnEnable()
        {
            m_NetworkManager.OnServerStarted += HandleServerStarted;
            m_NetworkManager.OnClientConnectedCallback += HandleClientConnected;
            m_NetworkManager.OnClientStopped += HandleClientStopped;
            m_NetworkManager.OnServerStopped += HandleServerStopped;
        }

        private void OnDisable()
        {
            // 플레이 종료 시 NetworkManager가 먼저 파괴될 수 있다.
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
            if (State != ConnectionState.Connecting)
            {
                return;
            }

            // StartClient()는 서버가 없어도 true를 돌려주므로, 직접 시간을 재지 않으면
            // 영원히 Connecting에 멈춘다.
            // unscaledTime을 쓰는 이유: timeScale은 무시해야 하고(게임을 멈춰도 연결은 진행 중),
            // realtimeSinceStartup은 에디터 Pause 시간까지 세서 재개 즉시 타임아웃이 터진다.
            if (Time.unscaledTime >= m_ConnectDeadline)
            {
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
            ApplyConnectionPayload();

            // StartHost()는 소켓 바인딩까지 동기적으로 하므로 여기서 바로 실패를 알 수 있다.
            if (!m_NetworkManager.StartHost())
            {
                Fail("호스트를 시작하지 못했습니다. 포트가 이미 사용 중일 수 있습니다.");
            }
        }

        /// <summary>UnityTransport에 설정된 주소로 접속을 시도한다.</summary>
        public void StartClient()
        {
            if (!CanStart())
            {
                return;
            }

            BeginConnecting();
            ApplyConnectionPayload();

            // 여기서의 false는 "시도조차 못 했다"는 뜻. 서버가 없어서 실패하는 경우는
            // Update()의 타임아웃이 잡는다.
            if (!m_NetworkManager.StartClient())
            {
                Fail("클라이언트를 시작하지 못했습니다.");
            }
        }

        /// <summary>사용자가 의도적으로 끊는다. 실패가 아니므로 사유를 발행하지 않는다.</summary>
        public void Disconnect()
        {
            if (State == ConnectionState.Disconnected)
            {
                return;
            }

            m_LastFailureReason = null;

            // 상태를 먼저 바꿔두면 Shutdown()이 부를 HandleStopped가 "이미 정리됨"으로 보고
            // 빠져나간다. 그래서 직접 끊은 경우엔 실패 메시지가 뜨지 않는다.
            m_NetworkManager.Shutdown();
            SetState(ConnectionState.Disconnected);
        }

        /// <summary>
        /// 마지막 실패 사유를 꺼낸다. 한 번 꺼내면 비워진다.
        /// 끊긴 뒤 로비로 돌아오는 사이 UI가 파괴되어 이벤트를 놓치므로 필요하다.
        /// </summary>
        public string ConsumeLastFailureReason()
        {
            var reason = m_LastFailureReason;
            m_LastFailureReason = null;
            return reason;
        }

        // ── NetworkManager 콜백 ──────────────────────────────────────────

        /// <summary>호스트는 자기 자신에게 접속하므로 서버가 뜬 시점이 곧 연결 완료다.</summary>
        private void HandleServerStarted()
        {
            if (m_NetworkManager.IsHost)
            {
                SetState(ConnectionState.Connected);
            }
        }

        /// <summary>누가 들어와도 매번 불리므로 내 ID일 때만 처리한다.</summary>
        private void HandleClientConnected(ulong clientId)
        {
            if (clientId == m_NetworkManager.LocalClientId)
            {
                SetState(ConnectionState.Connected);
            }
        }

        /// <summary>호스트를 끄면 이 콜백과 HandleServerStopped가 모두 불리므로 한쪽만 처리한다.</summary>
        private void HandleClientStopped(bool wasHost)
        {
            if (wasHost)
            {
                return;
            }

            HandleStopped();
        }

        private void HandleServerStopped(bool wasHost)
        {
            HandleStopped();
        }

        /// <summary>여기까지 온 것은 의도하지 않게 끊긴 경우뿐이다(Disconnect()는 위에서 걸러진다).</summary>
        private void HandleStopped()
        {
            if (State == ConnectionState.Disconnected)
            {
                return;
            }

            // DisconnectReason은 서버가 보낸 사유와 NGO의 영문 진단 문자열을 같은 자리에서
            // 반환한다. 구분할 공개 API가 없어 진단 문자열의 고정 접두사로 판별한다.
            var reason = m_NetworkManager.DisconnectReason;
            var isDiagnostic = string.IsNullOrEmpty(reason) || reason.StartsWith("[Disconnect Event]");

            if (isDiagnostic && !string.IsNullOrEmpty(reason))
            {
                Debug.Log($"[ConnectionManager] 끊김 상세: {reason}");
            }

            Fail(isDiagnostic ? "연결이 끊어졌습니다." : reason);
        }

        // ── 내부 ─────────────────────────────────────────────────────────

        private bool CanStart()
        {
            if (State == ConnectionState.Disconnected)
            {
                return true;
            }

            Debug.LogWarning($"[ConnectionManager] 이미 {State} 상태이므로 요청을 무시합니다.");
            return false;
        }

        /// <summary>
        /// 접속 시 서버로 보낼 식별자를 싣는다. 승인 콜백이 이 값을 읽어 심사한다.
        /// 시작 직전에 넣어야 하며, 호스트도 자기 자신을 심사 대상으로 거치므로 함께 설정한다.
        /// </summary>
        private void ApplyConnectionPayload()
        {
            m_NetworkManager.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(LocalPlayerId);
        }

        private void BeginConnecting()
        {
            // Update()의 비교와 같은 시계를 써야 한다.
            m_ConnectDeadline = Time.unscaledTime + connectTimeoutSeconds;
            SetState(ConnectionState.Connecting);
        }

        private void Fail(string reason)
        {
            // 이벤트보다 먼저 보관해야, 상태 변화를 듣고 씬을 바꾸는 쪽이 새 화면에서 꺼내 쓸 수 있다.
            m_LastFailureReason = reason;

            SetState(ConnectionState.Disconnected);
            OnConnectionFailed?.Invoke(reason);
        }

        /// <summary>상태 변경의 유일한 통로. 값이 실제로 바뀔 때만 이벤트를 발행한다.</summary>
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
