using UnityEngine;
using TheShedding.Characters;

namespace TheShedding.Network
{
    // 손전등은 인벤토리와 무관해 로컬 실행(기본 Skill)을 그대로 쓴다.
    [RequireComponent(typeof(RobberCharacterController))]
    public class RobberNetworkAdapter : InventoryOwnerNetworkAdapter { }
}
