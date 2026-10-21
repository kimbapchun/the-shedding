using Unity.Netcode;
using UnityEngine;
using TheShedding.Characters;

namespace TheShedding.Network
{
    [RequireComponent(typeof(BabyController))]
    public class BabyNetworkAdapter : InventoryOwnerNetworkAdapter
    {
        public override void Skill() => StealRpc();

        [Rpc(SendTo.Server, RequireOwnership = true)]
        private void StealRpc() => Controller.OnSkillInput();
    }
}
