using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using TheShedding.Characters;

namespace TheShedding.Network
{
    /// <summary>
    /// 내 캐릭터가 스폰되면 씬의 카메라를 나에게 붙인다. 캐릭터 프리팹에 붙인다.
    ///
    /// 카메라는 씬에 한 대뿐이고 클라이언트마다 자기 씬을 들고 있으므로,
    /// "누구를 비출 것인가"만 정해주면 된다. 그 답은 소유자 본인이다.
    ///
    /// ThirdPersonCamera는 게임플레이 어셈블리에 있어 네트워크를 알지 못한다.
    /// 소유권을 아는 쪽이 카메라를 찾아가는 방향이어야 의존성이 한쪽으로만 흐른다.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class LocalPlayerCameraBinder : NetworkBehaviour
    {
        public override void OnNetworkSpawn()
        {
            // 남의 캐릭터가 내 카메라를 뺏어가지 않게 한다.
            // 이 검사가 없으면 마지막으로 스폰된 캐릭터가 모두의 카메라를 가져간다.
            if (!IsOwner)
            {
                return;
            }

            var camera = FindFirstObjectByType<ThirdPersonCamera>();

            if (camera == null)
            {
                Debug.LogError("[LocalPlayerCameraBinder] 씬에서 ThirdPersonCamera를 찾지 못했습니다. " +
                               "카메라가 캐릭터를 따라가지 않습니다.");
                return;
            }

            if (!TryGetComponent(out BaseCharacterController controller))
            {
                Debug.LogError("[LocalPlayerCameraBinder] 같은 오브젝트에 BaseCharacterController가 없습니다.");
                return;
            }

            // SetTarget이 PlayerInput에서 Look 액션을 꺼내 쓴다. 비소유자는
            // PlayerNetworkAuthority가 PlayerInput을 꺼두지만 여기는 소유자 경로라 살아 있다.
            if (!TryGetComponent(out PlayerInput playerInput))
            {
                Debug.LogError("[LocalPlayerCameraBinder] 같은 오브젝트에 PlayerInput이 없습니다. " +
                               "시점 조작을 연결할 수 없습니다.");
                return;
            }

            camera.SetTarget(controller, playerInput);
        }
    }
}
