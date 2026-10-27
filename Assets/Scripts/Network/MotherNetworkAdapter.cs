using Unity.Netcode;
using UnityEngine;
using TheShedding.Characters;
using TheShedding.InventorySystem;
using TheShedding.Items;

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
            {
                mother.OnTrapCooldownStarted += HandleCooldownStarted;
                mother.TrapPlacement = TrySpawnTrap;
            }
            else
            {
                trapCooldownEndsAt.OnValueChanged += HandleCooldownChanged;
                ApplyCooldown(trapCooldownEndsAt.Value);
            }
        }

        public override void OnNetworkDespawn()
        {
            mother.OnTrapCooldownStarted -= HandleCooldownStarted;
            mother.TrapPlacement = null;
            trapCooldownEndsAt.OnValueChanged -= HandleCooldownChanged;
            base.OnNetworkDespawn();
        }

        private void HandleCooldownStarted(float duration)
            => trapCooldownEndsAt.Value = NetworkManager.ServerTime.Time + duration;

        private void HandleCooldownChanged(double previous, double current) => ApplyCooldown(current);

        private void ApplyCooldown(double endsAt)
            => mother.SynchronizeTrapCooldown((float)System.Math.Max(0d, endsAt - NetworkManager.ServerTime.Time));

        private bool TrySpawnTrap(TrapItem trap, Vector3 position)
        {
            if (!IsServer || trap.placementPrefab == null
                || !trap.placementPrefab.TryGetComponent<NetworkObject>(out var prefab)
                || !NetworkManager.NetworkConfig.Prefabs.Contains(trap.placementPrefab)) return false;
            var instance = Instantiate(prefab, position, Quaternion.identity);
            try
            {
                instance.Spawn(destroyWithScene: true);
                return instance.IsSpawned;
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception, this);
                Destroy(instance.gameObject);
                return false;
            }
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
