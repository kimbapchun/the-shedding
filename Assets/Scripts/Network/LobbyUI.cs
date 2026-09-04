using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheShedding.Network
{
    /// <summary>
    /// 부트스트랩 씬의 로비 UI. NGO를 전혀 모르고 ConnectionManager의 상태만 보고 갱신한다.
    /// 씬 전환 시 이 오브젝트는 파괴되지만 매니저들은 살아남으므로,
    /// 로비로 돌아오면 새 LobbyUI가 기존 매니저에 다시 붙는다.
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

        // Awake가 아니라 Start인 이유: 매니저들이 자기 Awake에서 Instance를 설정하므로
        // 모든 Awake가 끝난 뒤인 Start에서 조회해야 안전하다.
        private void Start()
        {
            // 매니저는 DontDestroyOnLoad 씬에 있어 Inspector 참조를 걸 수 없다.
            m_Connection = ConnectionManager.Instance;

            if (m_Connection == null)
            {
                Debug.LogError("[LobbyUI] ConnectionManager를 찾지 못했습니다. " +
                               "NetworkManager 오브젝트에 ConnectionManager가 붙어 있는지 확인하세요.");
                enabled = false;
                return;
            }

            hostButton.onClick.AddListener(m_Connection.StartHost);
            clientButton.onClick.AddListener(m_Connection.StartClient);
            disconnectButton.onClick.AddListener(m_Connection.Disconnect);
            startGameButton.onClick.AddListener(HandleStartGameClicked);

            m_Connection.OnStateChanged += HandleStateChanged;
            m_Connection.OnConnectionFailed += HandleConnectionFailed;

            m_SceneLoader = NetworkSceneLoader.Instance;
            if (m_SceneLoader != null)
            {
                // 씬 전환 실패도 같은 상태 표시줄에 보여준다.
                m_SceneLoader.OnSceneLoadFailed += HandleConnectionFailed;
            }

            // 이벤트는 상태가 바뀔 때만 발행되므로 현재 상태는 직접 한 번 반영한다.
            HandleStateChanged(m_Connection.State);
        }

        private void OnDestroy()
        {
            if (m_Connection == null)
            {
                return;
            }

            // 매니저가 이 오브젝트보다 오래 살기 때문에, 해제하지 않으면 다음 상태 변화 때
            // 파괴된 객체를 호출하려다 MissingReferenceException이 난다.
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

        private void HandleStateChanged(ConnectionState state)
        {
            var idle = state == ConnectionState.Disconnected;

            hostButton.interactable = idle;
            clientButton.interactable = idle;
            disconnectButton.interactable = !idle;

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
        /// HandleStateChanged가 먼저 "연결 없음"으로 바꾼 뒤에 호출되므로 여기서 덮어써야
        /// 사유가 화면에 남는다. 직접 끊은 경우엔 이 이벤트가 오지 않는다.
        /// </summary>
        private void HandleConnectionFailed(string reason)
        {
            statusText.text = reason;
        }
    }
}
