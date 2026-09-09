using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheShedding.Network
{
    /// <summary>
    /// 네트워크 씬 전환의 단일 진입점. 서버가 전환하면 NGO가 클라이언트를 따라오게 한다.
    /// (협업 규칙 A — 게임플레이 코드는 SceneManager.LoadScene을 직접 부르지 않는다)
    ///
    /// 늦게 들어온 클라이언트는 NGO가 알아서 현재 씬으로 맞춰주므로 따로 처리하지 않는다.
    /// NetworkManager와 같은 GameObject에 붙인다.
    /// </summary>
    [RequireComponent(typeof(NetworkManager))]
    public class NetworkSceneLoader : MonoBehaviour
    {
        public static NetworkSceneLoader Instance { get; private set; }

        [Tooltip("로비에서 게임을 시작할 때 이동할 씬. 빌드 설정에 등록되어 있어야 한다.")]
        [SerializeField] private string gameSceneName = "FirstFloorScene";

        [Tooltip("연결이 끝난 뒤 돌아갈 로비 씬. 부팅 씬과 달라야 한다.")]
        [SerializeField] private string lobbySceneName = "Lobby";

        /// <summary>모든 클라이언트가 로딩을 마쳤을 때 발행. 캐릭터 스폰의 기준점이 된다.</summary>
        public event Action<string> OnSceneLoadCompleted;

        /// <summary>전환을 시작하지 못했을 때 사유와 함께 발행.</summary>
        public event Action<string> OnSceneLoadFailed;

        public string GameSceneName => gameSceneName;
        public string LobbySceneName => lobbySceneName;

        private NetworkManager m_NetworkManager;
        private ConnectionManager m_Connection;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            m_NetworkManager = GetComponent<NetworkManager>();

            // 싱글톤 대신 같은 오브젝트에서 가져온다. Awake 실행 순서와 무관하게 이미 존재한다.
            m_Connection = GetComponent<ConnectionManager>();
        }

        private void OnEnable()
        {
            // NetworkManager.SceneManager는 서버/클라이언트가 시작된 뒤에야 만들어지므로
            // 여기서 바로 구독할 수 없다.
            m_NetworkManager.OnServerStarted += SubscribeSceneEvents;
            m_NetworkManager.OnClientStarted += SubscribeSceneEvents;

            if (m_Connection != null)
            {
                m_Connection.OnStateChanged += HandleConnectionStateChanged;
            }
        }

        private void OnDisable()
        {
            if (m_Connection != null)
            {
                m_Connection.OnStateChanged -= HandleConnectionStateChanged;
            }

            if (m_NetworkManager == null)
            {
                return;
            }

            m_NetworkManager.OnServerStarted -= SubscribeSceneEvents;
            m_NetworkManager.OnClientStarted -= SubscribeSceneEvents;

            // SceneManager는 연결이 끊기면 사라진다.
            if (m_NetworkManager.SceneManager != null)
            {
                m_NetworkManager.SceneManager.OnLoadEventCompleted -= HandleLoadEventCompleted;
            }
        }

        private void Start()
        {
            // 부팅 씬은 매니저를 만들기 위한 곳이라 화면이 없다. 곧바로 로비로 넘긴다.
            //
            // 부팅 씬과 로비 씬을 분리한 이유: 로비로 돌아갈 때 부팅 씬을 다시 로드하면
            // NetworkManager 오브젝트가 씬에서 새로 하나 더 만들어진다. NGO는 중복
            // NetworkManager를 싱글톤으로 등록만 안 할 뿐 파괴하지는 않아서,
            // 자식으로 둔 EventSystem까지 두 벌이 되어 입력 처리가 꼬인다.
            //
            // 이 컴포넌트는 씬 전환에도 살아남으므로 Start는 앱 실행 중 한 번만 불린다.
            ReturnToLobbyLocal();
        }

        // ── 외부 진입점 ──────────────────────────────────────────────────

        /// <summary>게임 씬으로 전환한다. 서버 전용. 로비 UI는 씬과 함께 사라진다.</summary>
        public void LoadGameScene()
        {
            LoadNetworkScene(gameSceneName);
        }

        /// <summary>접속한 모두를 로비로 데려간다. 서버 전용, 연결이 살아 있을 때만.</summary>
        public void LoadLobbyScene()
        {
            LoadNetworkScene(lobbySceneName);
        }

        /// <summary>
        /// 연결이 끊긴 뒤 혼자 로비로 돌아간다.
        ///
        /// LoadLobbyScene과 구분해야 한다. Shutdown 이후에는 NetworkManager.SceneManager가
        /// 없고 알려줄 상대도 없으므로 Unity 기본 SceneManager로 직접 로드한다.
        /// 규칙 A가 막으려던 것은 "클라이언트끼리 씬이 어긋나는 것"이라 이 경로는 예외다.
        /// </summary>
        public void ReturnToLobbyLocal()
        {
            if (SceneManager.GetActiveScene().name == lobbySceneName)
            {
                return;
            }

            SceneManager.LoadScene(lobbySceneName, LoadSceneMode.Single);
        }

        // ── 내부 ─────────────────────────────────────────────────────────

        private void LoadNetworkScene(string sceneName)
        {
            // NGO도 ServerOnlyAction으로 거절하지만, 먼저 걸러야 원인이 바로 드러난다.
            if (!m_NetworkManager.IsServer)
            {
                Fail("씬 전환은 호스트만 할 수 있습니다.");
                return;
            }

            // LoadScene은 예외를 던지지 않고 상태 코드를 돌려준다.
            // Started가 아니면 전환이 시작조차 안 된 것이라 반환값을 반드시 확인해야 한다.
            var status = m_NetworkManager.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);

            if (status != SceneEventProgressStatus.Started)
            {
                Fail(DescribeFailure(sceneName, status));
            }
        }

        /// <summary>끊긴 이유가 무엇이든 "연결이 없다"는 결과는 같으므로 한 곳에서 처리한다.</summary>
        private void HandleConnectionStateChanged(ConnectionState state)
        {
            if (state != ConnectionState.Disconnected)
            {
                return;
            }

            ReturnToLobbyLocal();
        }

        private void SubscribeSceneEvents()
        {
            // 호스트는 OnServerStarted와 OnClientStarted가 모두 불려 두 번 구독되므로
            // 해제를 먼저 한다.
            m_NetworkManager.SceneManager.OnLoadEventCompleted -= HandleLoadEventCompleted;
            m_NetworkManager.SceneManager.OnLoadEventCompleted += HandleLoadEventCompleted;
        }

        private void HandleLoadEventCompleted(
            string sceneName,
            LoadSceneMode loadSceneMode,
            List<ulong> clientsCompleted,
            List<ulong> clientsTimedOut)
        {
            // 시간 안에 못 끝낸 클라이언트는 다른 사람들과 다른 씬에 남는다.
            if (clientsTimedOut.Count > 0)
            {
                Debug.LogWarning($"[NetworkSceneLoader] '{sceneName}' 로딩 시간 초과 클라이언트: " +
                                 string.Join(", ", clientsTimedOut));
            }

            OnSceneLoadCompleted?.Invoke(sceneName);
        }

        /// <summary>대부분 설정 실수에서 나오는 값이라 무엇을 고쳐야 하는지까지 담는다.</summary>
        private string DescribeFailure(string sceneName, SceneEventProgressStatus status)
        {
            return status switch
            {
                SceneEventProgressStatus.SceneNotLoaded =>
                    $"'{sceneName}' 씬을 불러오지 못했습니다.",
                SceneEventProgressStatus.SceneEventInProgress =>
                    "이미 다른 씬 전환이 진행 중입니다.",
                SceneEventProgressStatus.InvalidSceneName =>
                    $"'{sceneName}' 씬을 찾을 수 없습니다. 빌드 설정에 등록되어 있는지 확인하세요.",
                SceneEventProgressStatus.SceneManagementNotEnabled =>
                    "NetworkManager의 Enable Scene Management가 꺼져 있습니다.",
                SceneEventProgressStatus.ServerOnlyAction =>
                    "씬 전환은 호스트만 할 수 있습니다.",
                _ => $"씬 전환에 실패했습니다. ({status})"
            };
        }

        private void Fail(string reason)
        {
            Debug.LogError($"[NetworkSceneLoader] {reason}");
            OnSceneLoadFailed?.Invoke(reason);
        }
    }
}
