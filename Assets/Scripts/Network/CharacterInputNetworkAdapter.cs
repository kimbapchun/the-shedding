using Unity.Netcode;
using UnityEngine;
using TheShedding.Characters;
using TheShedding.InventorySystem;

namespace TheShedding.Network
{
    /// <summary>
    /// 원격 클라이언트 소유자의 상호작용·스킬 입력을 서버로 보내 서버에서 실행한다.
    /// 호스트도 같은 요청 경로를 사용한다.
    /// </summary>
    [RequireComponent(typeof(PlayerInputReader), typeof(BaseCharacterController))]
    public class CharacterInputNetworkAdapter : NetworkBehaviour, ICharacterCommands
    {
        protected BaseCharacterController Controller { get; private set; }
        private PlayerInputReader inputReader;

        protected virtual void Awake()
        {
            Controller = GetComponent<BaseCharacterController>();
            inputReader = GetComponent<PlayerInputReader>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
                inputReader.Commands = this;
        }

        public override void OnNetworkDespawn()
        {
            if (ReferenceEquals(inputReader.Commands, this))
                inputReader.Commands = null;
        }

        public void Interact() => InteractRpc();

        public virtual void Skill() => SkillRpc();
        public void Attack() => AttackRpc();
        public void UseHealItem() => HealRpc();
        public void SetSitting(bool sitting) => SetSittingRpc(sitting);
        public void SetLying(bool lying) => SetLyingRpc(lying);

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void SetSittingRpc(bool sitting) => Controller.SetSittingState(sitting);

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void SetLyingRpc(bool lying) => Controller.SetLyingState(lying);
        public void DropItem()
        {
            if (Controller is IInventoryOwner owner) DropRpc(owner.Inventory.SelectedItemId);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void SkillRpc() => Controller.OnSkillInput();

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void AttackRpc() => Controller.OnAttackInput();

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void HealRpc()
        {
            if (Controller is RobberCharacterController robber) robber.UseHealItem();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void DropRpc(int itemId)
        {
            if (!Controller.IsActionAllowed || Controller is not IInventoryOwner owner) return;
            var spawner = FindFirstObjectByType<NetworkItemSpawner>();
            if (spawner != null) spawner.TryDrop(owner.Inventory, itemId,
                transform.position + transform.forward * 1.5f);
        }

        // 위치는 소유자 권위라 서버가 보는 위치로 다시 판정한다.
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void InteractRpc() => Controller.TryInteract();
    }
}
