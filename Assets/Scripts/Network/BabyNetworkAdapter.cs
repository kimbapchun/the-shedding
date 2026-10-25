using Unity.Netcode;
using UnityEngine;
using TheShedding.Characters;

namespace TheShedding.Network
{
    [RequireComponent(typeof(BabyController))]
    public class BabyNetworkAdapter : CharacterInputNetworkAdapter
    {
        public override void Skill() => StealRpc();

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void StealRpc() => Controller.OnSkillInput();
    }
}
