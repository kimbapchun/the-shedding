using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheShedding.Network
{
    /// <summary>
    /// 네트워크 씬 전환의 단일 진입점.
    ///
    /// 씬 전환은 반드시 서버에서만 실행한다. 서버가 LoadScene을 호출하면 NGO가 접속한
    /// 모든 클라이언트에게 같은 씬을 로드하라고 알리고, 로딩이 끝나면 상태를 맞춰준다.
    /// 클라이언트가 각자 UnityEngine의 SceneManager.LoadScene을 부르면 서로 다른 씬에
    /// 있게 되어 동기화가 깨진다.
    /// (협업 규칙 A — SceneManager.LoadScene 직접 호출 금지)
    ///
    /// 늦게 들어온 클라이언트(late join)는 따로 처리하지 않아도 된다. NGO가 접속 시점에
    /// 서버가 현재 어떤 씬에 있는지 알려주고 자동으로 같은 씬으로 맞춰준다.
    ///
    /// ConnectionManager와 마찬가지로 NetworkManager와 같은 GameObject에 붙인다.
    /// (NetworkManager가 DontDestroyOnLoad를 걸어주므로 씬이 바뀌어도 살아남는다)
    /// </summary>
    [RequireComponent(typeof(NetworkManager))]
    public class NetworkSceneLoader : MonoBehaviour
    {
        public static NetworkSceneLoader Instance { get; private set; }

        [Tooltip("로비에서 게임을 시작할 때 이동할 씬. 빌드 설정에 등록되어 있어야 한다.")]
        [SerializeField] private string gameSceneName = "FirstFloorScene";

        [Tooltip("연결이 끝난 뒤 돌아갈 로비 씬.")]
        [SerializeField] private string lobbySceneName = "Bootstrap";

        /// <summary>
        /// 모든 클라이언트가 씬 로딩을 마쳤을 때 발행. 인자는 씬 이름.
        /// "플레이어를 배치해도 안전한 시점"을 알리는 신호라, 6단계에서 캐릭터 스폰이
        /// 이 이벤트를 기준으로 붙게 된다.
        /// </summary>
        public event Action<string> OnSceneLoadCompleted;

        /// <summary>씬 전환을 시작하지 못했을 때 사유와 함께 발행. UI가 그대로 표시한다.</summary>
        public event Action<string> OnSceneLoadFailed;

        public string GameSceneName => gameSceneName;
        public string LobbySceneName => lobbySceneName;

        private NetworkManager m_NetworkManager;

        private void Awake()
        {
            // ConnectionManager와 같은 이유로 자기 자신만 제거한다.
            // 이 오브젝트에는 NetworkManager도 붙어 있어 오브젝트째 지우면 안 된다.
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
            // NetworkManager.SceneManager는 서버/클라이언트가 시작된 뒤에야 만들어진다.
            // 그래서 여기서 바로 구독할 수 없고, 시작 시점을 알려주는 콜백을 거쳐 구독한다.
            m_NetworkManager.OnServerStarted += SubscribeSceneEvents;
            m_NetworkManager.OnClientStarted += SubscribeSceneEvents;
        }

        private void OnDisable()
        {
            // 플레이 종료 시 NetworkManager가 먼저 파괴될 수 있으므로 방어한다.
            if (m_NetworkManager == null)
            {
                return;
            }

            m_NetworkManager.OnServerStarted -= SubscribeSceneEvents;
            m_NetworkManager.OnClientStarted -= SubscribeSceneEvents;

            // SceneManager는 연결이 끊기면 파괴되므로 존재할 때만 해제한다.
            if (m_NetworkManager.SceneManager != null)
            {
                m_NetworkManager.SceneManager.OnLoadEventCompleted -= HandleLoadEventCompleted;
            }
        }

        // ── 외부 진입점 ──────────────────────────────────────────────────

        /// <summary>
        /// 로비에서 게임 씬으로 전환한다. 서버에서만 의미가 있다.
        /// 전환이 일어나면 Bootstrap 씬이 언로드되면서 로비 UI도 함께 사라진다.
        /// </summary>
        public void LoadGameScene()
        {
            LoadNetworkScene(gameSceneName);
        }

        /// <summary>게임을 끝내고 로비 씬으로 돌아간다. 서버에서만 의미가 있다.</summary>
        public void LoadLobbyScene()
        {
            LoadNetworkScene(lobbySceneName);
        }

        // ── 내부 ─────────────────────────────────────────────────────────

        private void LoadNetworkScene(string sceneName)
        {
            // 클라이언트가 호출하면 NGO도 ServerOnlyAction으로 거절하지만,
            // 여기서 먼저 걸러야 "왜 아무 일도 안 일어나는지" 바로 드러난다.
            if (!m_NetworkManager.IsServer)
            {
                Fail("씬 전환은 호스트만 할 수 있습니다.");
                return;
            }

            // LoadSceneMode.Single: 기존 씬을 언로드하고 새 씬만 남긴다.
            // NetworkManager는 DontDestroyOnLoad 씬에 있어 언로드 대상이 아니라 그대로 유지된다.
            //
            // LoadScene은 예외를 던지지 않고 상태 코드를 돌려준다. Started가 아니면
            // 전환이 시작조차 되지 않은 것이라, 반환값을 확인하지 않으면 조용히 실패한다.
            var status = m_NetworkManager.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);

            if (status != SceneEventProgressStatus.Started)
            {
                Fail(DescribeFailure(sceneName, status));
            }
        }

        private void SubscribeSceneEvents()
        {
            // 호스트는 서버이자 클라이언트라 OnServerStarted와 OnClientStarted가 모두 불린다.
            // 그대로 두면 이벤트가 두 번 구독되어 콜백도 두 번 실행되므로,
            // 해제를 먼저 한 뒤 구독해 중복을 막는다.
            m_NetworkManager.SceneManager.OnLoadEventCompleted -= HandleLoadEventCompleted;
            m_NetworkManager.SceneManager.OnLoadEventCompleted += HandleLoadEventCompleted;
        }

        /// <summary>
        /// 모든 클라이언트가 로딩을 마쳤을 때 호출된다.
        ///
        /// clientsTimedOut은 제한 시간 안에 로딩을 끝내지 못한 클라이언트 목록이다.
        /// 사양이 낮은 기기나 큰 씬에서 발생할 수 있고, 해당 클라이언트는 다른 사람들과
        /// 다른 씬에 남아 있게 되므로 원인 추적을 위해 로그로 남긴다.
        /// </summary>
        private void HandleLoadEventCompleted(
            string sceneName,
            LoadSceneMode loadSceneMode,
            List<ulong> clientsCompleted,
            List<ulong> clientsTimedOut)
        {
            if (clientsTimedOut.Count > 0)
            {
                Debug.LogWarning($"[NetworkSceneLoader] '{sceneName}' 로딩 시간 초과 클라이언트: " +
                                 string.Join(", ", clientsTimedOut));
            }

            OnSceneLoadCompleted?.Invoke(sceneName);
        }

        /// <summary>
        /// 상태 코드를 사용자와 개발자가 읽을 수 있는 문구로 옮긴다.
        /// 대부분 설정 실수에서 나오는 값이라, 무엇을 고쳐야 하는지까지 문구에 담는다.
        /// </summary>
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
            // 화면 표시와 별개로 콘솔에도 남긴다. 씬 전환 실패는 대부분 설정 문제라
            // 로그가 있어야 원인을 빨리 찾을 수 있다.
            Debug.LogError($"[NetworkSceneLoader] {reason}");
            OnSceneLoadFailed?.Invoke(reason);
        }
    }
}
