using TheShedding.Characters;
using UnityEngine;

namespace TheShedding.Interactables
{
    /// <summary>
    /// 오프라인·에디터 테스트용 IItemSpawner 구현.
    /// 씬 로드 완료 시 자기 씬의 ItemPickup들에 자신을 주입한다.
    /// 네트워크 환경에서는 NetworkItemSpawner가 대신 붙는다.
    /// </summary>
    public class LocalItemSpawner : MonoBehaviour, IItemSpawner
    {
        private void Start()
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var pickup in root.GetComponentsInChildren<ItemPickup>(true))
                    pickup.Spawner = this;
        }

        public void NotifyPickedUp(ItemPickup pickup, BaseCharacterController collector)
        {
            if (pickup != null)
                pickup.gameObject.SetActive(false);
        }

        public void Spawn(int itemId, Vector3 position)
        {
            // TODO: 아이템 버리기 기능 구현 시 프리팹 인스턴스화 로직 추가
            Debug.LogWarning($"[LocalItemSpawner] Spawn({itemId}, {position}) 미구현", this);
        }
    }
}
