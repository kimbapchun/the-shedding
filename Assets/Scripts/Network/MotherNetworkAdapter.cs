using Unity.Netcode;
using UnityEngine;
using TheShedding.Characters;
using TheShedding.InventorySystem;

namespace TheShedding.Network
{
    [RequireComponent(typeof(MotherController))]
    public class MotherNetworkAdapter : CharacterInputNetworkAdapter
    {
        private Inventory inventory;

        protected override void Awake()
        {
            base.Awake();
            inventory = GetComponent<Inventory>();
        }

        // 선택 슬롯은 각자 로컬이라 고른 함정 id를 같이 보낸다.
        public override void Skill() => PlaceTrapRpc(inventory.SelectedItemId);

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void PlaceTrapRpc(int trapId)
        {
            ((MotherController)Controller).TryPlaceTrap(trapId);
        }
    }
}
