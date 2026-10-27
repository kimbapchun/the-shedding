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
        private MotherController mother;
        private readonly NetworkVariable<double> trapCooldownEndsAt = new();

        protected override void Awake()
        {
            base.Awake();
            inventory = GetComponent<Inventory>();
            mother = (MotherController)Controller;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (IsServer)
                mother.OnTrapCooldownStarted += HandleCooldownStarted;
            else
            {
                trapCooldownEndsAt.OnValueChanged += HandleCooldownChanged;
                ApplyCooldown(trapCooldownEndsAt.Value);
            }
        }

        public override void OnNetworkDespawn()
        {
            mother.OnTrapCooldownStarted -= HandleCooldownStarted;
            trapCooldownEndsAt.OnValueChanged -= HandleCooldownChanged;
            base.OnNetworkDespawn();
        }

        private void HandleCooldownStarted(float duration)
            => trapCooldownEndsAt.Value = NetworkManager.ServerTime.Time + duration;

        private void HandleCooldownChanged(double previous, double current) => ApplyCooldown(current);

        private void ApplyCooldown(double endsAt)
            => mother.SynchronizeTrapCooldown((float)System.Math.Max(0d, endsAt - NetworkManager.ServerTime.Time));

        // 선택 슬롯은 각자 로컬이라 고른 함정 id를 같이 보낸다.
        public override void Skill() => PlaceTrapRpc(inventory.SelectedItemId);

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void PlaceTrapRpc(int trapId)
        {
            ((MotherController)Controller).TryPlaceTrap(trapId);
        }
    }
}
