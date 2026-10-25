using UnityEngine;
using TheShedding.Characters;

namespace TheShedding.Network
{
    // TODO (#42): 손전등 토글·스턴의 서버 판정과 상태 복제를 연결해야 한다. 현재는 로컬 실행만 구현됨.
    [RequireComponent(typeof(RobberCharacterController))]
    public class RobberNetworkAdapter : CharacterInputNetworkAdapter { }
}
