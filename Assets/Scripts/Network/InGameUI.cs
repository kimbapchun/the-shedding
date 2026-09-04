using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TheShedding.Network
{
    /// <summary>
    /// 게임 중에도 연결을 끊을 수 있게 해주는 UI.
    ///
    /// 로비 UI는 Bootstrap 씬과 함께 사라지므로 게임 씬에서는 나갈 방법이 없어진다.
    /// NetworkManager의 자식으로 두어 씬 전환에도 살아남게 한다.
    /// 게임 씬 파일을 건드리지 않아 맵 작업과 병합 충돌이 나지 않는 이점도 있다.
    /// </summary>
    public class InGameUI : MonoBehaviour
    {
        [Tooltip("게임 중에만 보여줄 패널. 이 컴포넌트가 붙은 오브젝트가 아니라 자식이어야 한다.")]
        [SerializeField] private GameObject panel;

        [SerializeField] private Button leaveButton;

        private ConnectionManager m_Connection;

        private void Start()
        {
            m_Connection = ConnectionManager.Instance;

            if (m_Connection == null)
            {
                Debug.LogError("[InGameUI] ConnectionManager를 찾지 못했습니다. " +
                               "NetworkManager 오브젝트에 붙어 있는지 확인하세요.");
                enabled = false;
                return;
            }

            leaveButton.onClick.AddListener(m_Connection.Disconnect);
            m_Connection.OnStateChanged += HandleStateChanged;

            // 연결 상태는 그대로인 채 씬만 바뀌는 경우(로비 → 게임)가 있다.
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;

            Refresh();
        }

        private void OnDestroy()
        {
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;

            if (m_Connection == null)
            {
                return;
            }

            leaveButton.onClick.RemoveListener(m_Connection.Disconnect);
            m_Connection.OnStateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(ConnectionState state)
        {
            Refresh();
        }

        private void HandleActiveSceneChanged(Scene previous, Scene next)
        {
            Refresh();
        }

        /// <summary>로비에는 이미 연결 끊기 버튼이 있으므로 게임 씬에서만 보여준다.</summary>
        private void Refresh()
        {
            var loader = NetworkSceneLoader.Instance;
            var inLobby = loader != null &&
                          SceneManager.GetActiveScene().name == loader.LobbySceneName;

            panel.SetActive(!inLobby && m_Connection.State == ConnectionState.Connected);
        }
    }
}
