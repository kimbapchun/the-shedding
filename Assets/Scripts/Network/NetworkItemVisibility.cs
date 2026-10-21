using Unity.Netcode;

namespace TheShedding.Network
{
    /// <summary>
    /// 씬 배치 아이템은 Despawn돼도 클라이언트에서 자동으로 꺼지지 않아 모든 인스턴스에서 직접 숨긴다.
    /// </summary>
    public class NetworkItemVisibility : NetworkBehaviour
    {
        public override void OnNetworkDespawn() => gameObject.SetActive(false);
    }
}
