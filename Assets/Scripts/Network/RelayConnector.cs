using System.Threading.Tasks;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;

namespace TheShedding.Network
{
    /// <summary>
    /// Unity Relay 할당을 받아 UnityTransport에 주입한다. 상태 관리는 ConnectionManager가 맡는다.
    /// </summary>
    internal static class RelayConnector
    {
        // WebGL은 UDP 소켓이 없어 WebSocket으로만 Relay에 붙을 수 있다.
#if UNITY_WEBGL && !UNITY_EDITOR
        private const string k_ConnectionType = "wss";
#else
        private const string k_ConnectionType = "dtls";
#endif

        /// <summary>호스트용 할당을 만들고 참가 코드를 돌려준다.</summary>
        public static async Task<string> AllocateHostAsync(UnityTransport transport, int maxConnections)
        {
            await EnsureSignedInAsync();

            var allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);
            Apply(transport, allocation.ToRelayServerData(k_ConnectionType));

            return await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
        }

        /// <summary>참가 코드로 호스트의 할당에 합류한다.</summary>
        public static async Task JoinAsync(UnityTransport transport, string joinCode)
        {
            await EnsureSignedInAsync();

            var allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
            Apply(transport, allocation.ToRelayServerData(k_ConnectionType));
        }

        private static void Apply(UnityTransport transport, Unity.Networking.Transport.Relay.RelayServerData data)
        {
            transport.UseWebSockets = k_ConnectionType == "wss";
            transport.SetRelayServerData(data);
        }

        /// <summary>Relay는 인증된 플레이어만 쓸 수 있어 익명 로그인을 먼저 거친다.</summary>
        private static async Task EnsureSignedInAsync()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                await UnityServices.InitializeAsync();
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
        }
    }
}
