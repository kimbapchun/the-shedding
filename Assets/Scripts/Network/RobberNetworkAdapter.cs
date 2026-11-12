using UnityEngine;
using Unity.Netcode;
using TheShedding.Characters;

namespace TheShedding.Network
{
    [RequireComponent(typeof(RobberCharacterController))]
    public class RobberNetworkAdapter : CharacterInputNetworkAdapter
    {
        private readonly NetworkVariable<bool> flashlightOn = new();
        private RobberCharacterController Robber => (RobberCharacterController)Controller;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            flashlightOn.OnValueChanged += HandleFlashlightChanged;
            Robber.SynchronizeFlashlight(flashlightOn.Value);
        }

        public override void OnNetworkDespawn()
        {
            flashlightOn.OnValueChanged -= HandleFlashlightChanged;
            base.OnNetworkDespawn();
        }

        public override void Skill() => ToggleFlashlightRpc();

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void ToggleFlashlightRpc()
        {
            Robber.OnSkillInput(); // 생존·행동 가능 여부는 컨트롤러에서 검증.
            flashlightOn.Value = Robber.IsFlashlightOn;
        }

        private void HandleFlashlightChanged(bool previous, bool current)
            => Robber.SynchronizeFlashlight(current);

        private void Update()
        {
            if (IsSpawned && IsServer) Robber.EvaluateFlashlight();
        }
    }
}
