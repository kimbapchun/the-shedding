using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using UnityEngine;

namespace TheShedding.Network
{
    /// <summary>
    /// 서버가 들어오는 접속을 심사한다. 서버에서만 동작하고 클라이언트에서는 아무 일도 하지 않는다.
    ///
    /// 거부할 때 실어 보낸 사유는 클라이언트의 DisconnectReason으로 그대로 전달되어
    /// 로비 화면에 표시된다. NGO가 채우는 영문 진단 문자열과 달리 우리가 쓴 문구가 나간다.
    ///
    /// NetworkManager와 같은 GameObject에 붙이고, Inspector에서
    /// NetworkManager의 Connection Approval을 켜야 콜백이 호출된다.
    /// </summary>
    [RequireComponent(typeof(NetworkManager))]
    public class ConnectionApprovalHandler : MonoBehaviour
    {
        [Tooltip("호스트를 포함한 최대 접속 인원.")]
        [SerializeField] private int maxPlayers = 5;

        private NetworkManager m_NetworkManager;

        /// <summary>승인된 접속의 clientId → 식별자. 재접속한 사람을 알아보는 근거가 된다.</summary>
        private readonly Dictionary<ulong, string> m_PlayerIdByClientId = new Dictionary<ulong, string>();

        private void Awake()
        {
            m_NetworkManager = GetComponent<NetworkManager>();
        }

        private void OnEnable()
        {
            // += 로 붙이면 멀티캐스트가 되어 NGO가 거부한다. 대입만 허용된다.
            m_NetworkManager.ConnectionApprovalCallback = HandleConnectionApproval;
            m_NetworkManager.OnClientDisconnectCallback += HandleClientDisconnect;
            m_NetworkManager.OnServerStopped += HandleServerStopped;
        }

        private void OnDisable()
        {
            if (m_NetworkManager == null)
            {
                return;
            }

            m_NetworkManager.ConnectionApprovalCallback = null;
            m_NetworkManager.OnClientDisconnectCallback -= HandleClientDisconnect;
            m_NetworkManager.OnServerStopped -= HandleServerStopped;
        }

        /// <summary>
        /// 방을 닫을 때 명단을 비운다. 한 번 실행한 앱에서 호스트를 여러 번 열 수 있는데,
        /// 남겨두면 인원수가 잘못 세어지고 재접속이 "이미 접속되어 있습니다"로 막힌다.
        ///
        /// 시작 시점이 아니라 종료 시점에 비우는 이유: 호스트 자신의 승인이
        /// OnServerStarted보다 먼저 일어날 수 있어, 시작할 때 비우면 호스트가 명단에서 빠진다.
        /// </summary>
        private void HandleServerStopped(bool wasHost)
        {
            m_PlayerIdByClientId.Clear();
        }

        private void Start()
        {
            // 이 설정이 꺼져 있으면 콜백이 아예 호출되지 않아, 인원 제한도 식별도 조용히 무시된다.
            if (!m_NetworkManager.NetworkConfig.ConnectionApproval)
            {
                Debug.LogWarning("[ConnectionApprovalHandler] NetworkManager의 Connection Approval이 꺼져 있어 " +
                                 "접속 심사가 동작하지 않습니다. Inspector에서 켜주세요.");
            }
        }

        /// <summary>
        /// 접속 요청 하나를 심사한다. 서버에서만 호출되며, 호스트 자신도 이 심사를 거친다.
        /// </summary>
        private void HandleConnectionApproval(
            NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            // 플레이어 오브젝트 생성은 6단계에서 캐릭터를 붙일 때 켠다.
            response.CreatePlayerObject = false;

            var playerId = Encoding.UTF8.GetString(request.Payload ?? new byte[0]);

            // 호스트 자신은 심사 대상이 아니다. 거부하면 방 자체가 안 열린다.
            if (request.ClientNetworkId == m_NetworkManager.LocalClientId)
            {
                Approve(response, request.ClientNetworkId, playerId);
                return;
            }

            if (string.IsNullOrEmpty(playerId))
            {
                Reject(response, "잘못된 접속 요청입니다.");
                return;
            }

            // 이미 접속 중인 사람이 또 들어오는 경우. 재접속은 앞선 연결이 정리된 뒤에 이뤄지므로
            // 여기에 걸린다면 같은 사람이 두 번 붙으려는 상황이다.
            if (m_PlayerIdByClientId.ContainsValue(playerId))
            {
                Reject(response, "이미 접속되어 있습니다.");
                return;
            }

            if (m_PlayerIdByClientId.Count >= maxPlayers)
            {
                Reject(response, $"방이 가득 찼습니다. (최대 {maxPlayers}명)");
                return;
            }

            Approve(response, request.ClientNetworkId, playerId);
        }

        private void Approve(
            NetworkManager.ConnectionApprovalResponse response,
            ulong clientId,
            string playerId)
        {
            m_PlayerIdByClientId[clientId] = playerId;
            response.Approved = true;

            Debug.Log($"[ConnectionApprovalHandler] 접속 승인 clientId={clientId} playerId={playerId} " +
                      $"({m_PlayerIdByClientId.Count}/{maxPlayers})");
        }

        private void Reject(NetworkManager.ConnectionApprovalResponse response, string reason)
        {
            response.Approved = false;
            response.Reason = reason;

            Debug.Log($"[ConnectionApprovalHandler] 접속 거부: {reason}");
        }

        /// <summary>
        /// 나간 사람의 자리를 비운다. 이걸 하지 않으면 인원수가 계속 차오르고
        /// 재접속이 "이미 접속되어 있습니다"로 거부된다.
        /// </summary>
        private void HandleClientDisconnect(ulong clientId)
        {
            if (m_PlayerIdByClientId.Remove(clientId))
            {
                Debug.Log($"[ConnectionApprovalHandler] 접속 해제 clientId={clientId} " +
                          $"({m_PlayerIdByClientId.Count}/{maxPlayers})");
            }
        }
    }
}
