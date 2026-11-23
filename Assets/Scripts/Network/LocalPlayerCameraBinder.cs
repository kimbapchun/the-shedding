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
            if (IsOwner) StartCoroutine(BindLocalCamera());
        }

        private System.Collections.IEnumerator BindLocalCamera()
        {
            // 씬 동기화 중에는 캐릭터보다 카메라가 늦게 준비될 수 있다.
            float deadline = Time.realtimeSinceStartup + 10f;
            while (IsSpawned && IsOwner)
            {
                var camera = FindAnyObjectByType<ThirdPersonCamera>();
                if (camera != null && TryGetComponent(out BaseCharacterController controller)
                    && TryGetComponent(out PlayerInput input))
                {
                    input.enabled = true;
                    input.ActivateInput();
                    // 프리팹의 Default Action Map이 비어 있어도 로컬 입력을 활성화한다.
                    input.SwitchCurrentActionMap("Player");
                    camera.SetTarget(controller, input);
                    Debug.Log($"[LocalPlayerCameraBinder] 연결 owner={OwnerClientId} character={name} camera={camera.name} position={transform.position}", this);
                    yield break;
                }
                if (Time.realtimeSinceStartup >= deadline)
                {
                    Debug.LogError($"[LocalPlayerCameraBinder] 카메라 연결 시간 초과 owner={OwnerClientId} character={name} scene={gameObject.scene.name}", this);
                    yield break;
                }
                yield return null;
            }
        }

        public override void OnNetworkDespawn() => StopAllCoroutines();
    }
}
