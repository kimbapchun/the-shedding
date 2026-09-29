using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using TheShedding.Characters;

namespace TheShedding.Network
{
    /// <summary>
    /// 내 것이 아닌 캐릭터에서 로컬 조작을 걷어낸다. 캐릭터 프리팹에 붙인다.
    ///
    /// 위치와 애니메이션은 NetworkTransform / NetworkAnimator가 받아서 그려주므로,
    /// 비소유자 인스턴스는 입력도 루트모션도 스스로 돌릴 이유가 없다.
    /// (권위 모델 — 이동은 소유자 권위)
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class PlayerNetworkAuthority : NetworkBehaviour
    {
        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                return;
            }

            // 컨트롤러를 끄면 Update와 OnAnimatorMove가 호출되지 않는다.
            // 이 둘은 MonoBehaviour 메시지라 소유권과 무관하게 돌기 때문에,
            // 켜둔 채로는 남의 캐릭터가 내 키보드에 반응하고 루트모션이 동기화 위치와 싸운다.
            if (TryGetComponent(out BaseCharacterController controller))
            {
                controller.enabled = false;
            }

            if (TryGetComponent(out PlayerInputReader inputReader))
            {
                inputReader.enabled = false;
            }

            // PlayerInput이 여러 개 살아 있으면 Input System이 로컬 분할 플레이로 보고
            // 디바이스를 나눠 배정한다. 소유자 것만 남겨야 한다.
            if (TryGetComponent(out PlayerInput playerInput))
            {
                playerInput.enabled = false;
            }

            // 컨트롤러가 꺼지면서 OnAnimatorMove 오버라이드가 사라지면
            // Unity 기본 루트모션이 되살아나 XZ까지 캐릭터를 밀어버린다.
            if (TryGetComponent(out Animator animator))
            {
                animator.applyRootMotion = false;
            }

            // Rigidbody는 건드리지 않는다. NetworkRigidbody가 비권위 인스턴스를
            // 알아서 kinematic으로 돌린다.
        }
    }
}
