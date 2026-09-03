using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheShedding.Network
{
    /// <summary>
    /// 부트스트랩 씬의 로비 UI.
    ///
    /// 이 클래스는 NetworkManager도, NGO API도 전혀 모른다. 오직 ConnectionManager의
    /// 상태와 이벤트만 보고 화면을 갱신한다. 덕분에 나중에 재연결·에러 처리 같은 로직이
    /// 늘어나도 이 파일은 손대지 않아도 된다.
    ///
    /// 씬 전환이 일어나면 이 오브젝트는 파괴되지만 ConnectionManager는 살아남는다.
    /// (NetworkManager와 함께 DontDestroyOnLoad를 타기 때문)
    /// 그래서 나중에 로비로 돌아왔을 때 새로 생긴 LobbyUI가 기존 ConnectionManager에
    /// 다시 붙는 구조가 된다.
    /// </summary>
    public class LobbyUI : MonoBehaviour
    {
        [SerializeField] private Button hostButton;
        [SerializeField] private Button clientButton;
        [SerializeField] private Button disconnectButton;
        [SerializeField] private Button startGameButton;
        [SerializeField] private TMP_Text statusText;

        private ConnectionManager m_Connection;
        private NetworkSceneLoader m_SceneLoader;

        // Awake가 아니라 Start를 쓰는 이유:
        // ConnectionManager는 자기 Awake()에서 Instance를 설정한다. Unity는 모든 오브젝트의
        // Awake를 끝낸 뒤에 Start를 돌리므로, Start 시점에는 Instance가 반드시 준비되어 있다.
        // (Awake에서 조회하면 실행 순서에 따라 null일 수 있다)
        private void Start()
        {
            // Inspector 참조 대신 싱글톤을 조회한다.
            // ConnectionManager는 다른 씬(DontDestroyOnLoad)에 있어서 씬 안의 오브젝트로는
            // 참조를 걸 수 없고, 걸어도 씬을 오갈 때마다 끊어진다.
            m_Connection = ConnectionManager.Instance;

            if (m_Connection == null)
            {
                Debug.LogError("[LobbyUI] ConnectionManager를 찾지 못했습니다. " +
                               "NetworkManager 오브젝트에 ConnectionManager가 붙어 있는지 확인하세요.");
                enabled = false;
                return;
            }

            // 버튼은 ConnectionManager의 메서드를 그대로 호출한다.
            // 여기서 NetworkManager.StartHost()를 직접 부르지 않는 것이 핵심이다.
            hostButton.onClick.AddListener(m_Connection.StartHost);
            clientButton.onClick.AddListener(m_Connection.StartClient);
            disconnectButton.onClick.AddListener(m_Connection.Disconnect);
            startGameButton.onClick.AddListener(HandleStartGameClicked);

            m_Connection.OnStateChanged += HandleStateChanged;
            m_Connection.OnConnectionFailed += HandleConnectionFailed;

            // 씬 전환 실패도 같은 상태 표시줄에 보여준다.
            m_SceneLoader = NetworkSceneLoader.Instance;
            if (m_SceneLoader != null)
            {
                m_SceneLoader.OnSceneLoadFailed += HandleConnectionFailed;
            }

            // 이벤트는 "상태가 바뀔 때"만 발행되므로, 시작 시점의 현재 상태는
            // 직접 한 번 반영해줘야 화면이 빈 채로 남지 않는다.
            HandleStateChanged(m_Connection.State);
        }

        private void OnDestroy()
        {
            if (m_Connection == null)
            {
                return;
            }

            // 구독 해제를 빼먹으면 안 되는 이유:
            // ConnectionManager는 이 오브젝트보다 오래 산다. 씬이 바뀌어 LobbyUI가 파괴돼도
            // 구독이 남아 있으면, 다음 상태 변화 때 이미 파괴된 객체의 메서드를 호출하려다
            // MissingReferenceException이 난다.
            hostButton.onClick.RemoveListener(m_Connection.StartHost);
            clientButton.onClick.RemoveListener(m_Connection.StartClient);
            disconnectButton.onClick.RemoveListener(m_Connection.Disconnect);
            startGameButton.onClick.RemoveListener(HandleStartGameClicked);

            m_Connection.OnStateChanged -= HandleStateChanged;
            m_Connection.OnConnectionFailed -= HandleConnectionFailed;

            if (m_SceneLoader != null)
            {
                m_SceneLoader.OnSceneLoadFailed -= HandleConnectionFailed;
            }
        }

        /// <summary>
        /// 게임 시작은 호스트만 누를 수 있다. 실제 권한 검사는 NetworkSceneLoader가 하고,
        /// 여기서는 버튼을 눌렀다는 사실만 전달한다.
        /// </summary>
        private void HandleStartGameClicked()
        {
            if (m_SceneLoader == null)
            {
                Debug.LogError("[LobbyUI] NetworkSceneLoader를 찾지 못했습니다. " +
                               "NetworkManager 오브젝트에 붙어 있는지 확인하세요.");
                return;
            }

            m_SceneLoader.LoadGameScene();
        }

        /// <summary>
        /// 연결 상태에 따라 버튼 활성화와 안내 문구를 갱신한다.
        /// 상태를 매 프레임 확인하지 않고 바뀔 때만 호출된다.
        /// </summary>
        private void HandleStateChanged(ConnectionState state)
        {
            // Disconnected일 때만 새로 연결을 시작할 수 있고,
            // 그 외(Connecting/Connected)에는 끊기만 가능하다.
            // 연결 중에 버튼을 연타해도 ConnectionManager가 막아주지만,
            // 아예 누를 수 없게 해두는 편이 사용자에게 더 명확하다.
            var idle = state == ConnectionState.Disconnected;

            hostButton.interactable = idle;
            clientButton.interactable = idle;
            disconnectButton.interactable = !idle;

            // 게임 시작은 호스트만, 그것도 연결이 끝난 뒤에야 가능하다.
            // 클라이언트가 눌러도 NetworkSceneLoader가 막지만, 애초에 누를 수 없게 둔다.
            startGameButton.interactable =
                state == ConnectionState.Connected && m_Connection.IsHost;

            statusText.text = state switch
            {
                ConnectionState.Disconnected => "연결 없음",
                ConnectionState.Connecting => "연결 중...",
                ConnectionState.Connected => "연결됨",
                _ => state.ToString()
            };
        }

        /// <summary>
        /// 연결 실패·예기치 않은 끊김일 때 사유를 보여준다.
        ///
        /// 이 시점엔 HandleStateChanged(Disconnected)가 먼저 호출되어 문구가 "연결 없음"으로
        /// 바뀐 뒤이므로, 여기서 덮어써야 실패 사유가 화면에 남는다.
        /// 사용자가 직접 Disconnect()를 누른 경우엔 이 이벤트가 발행되지 않아
        /// "연결 없음"이 그대로 유지된다.
        /// </summary>
        private void HandleConnectionFailed(string reason)
        {
            statusText.text = reason;
        }
    }
}
