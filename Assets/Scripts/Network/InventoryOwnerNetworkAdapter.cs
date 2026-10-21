using Unity.Netcode;
using UnityEngine;
using TheShedding.Characters;

namespace TheShedding.Network
{
    /// <summary>
    /// 원격 클라이언트 소유자의 상호작용·스킬 입력을 서버로 보내 서버에서 실행한다.
    /// 호스트는 서버이므로 가로채지 않는다.
    /// </summary>
    [RequireComponent(typeof(PlayerInputReader))]
    public abstract class InventoryOwnerNetworkAdapter : NetworkBehaviour, ICharacterCommands
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
            if (IsOwner && !IsServer)
                inputReader.Commands = this;
        }

        public override void OnNetworkDespawn()
        {
            if (ReferenceEquals(inputReader.Commands, this))
                inputReader.Commands = null;
        }

        public void Interact() => InteractRpc();

        public virtual void Skill() => Controller.OnSkillInput();

        // 위치는 소유자 권위라 서버가 보는 위치로 다시 판정한다.
        [Rpc(SendTo.Server, RequireOwnership = true)]
        private void InteractRpc() => Controller.TryInteract();
    }
}
