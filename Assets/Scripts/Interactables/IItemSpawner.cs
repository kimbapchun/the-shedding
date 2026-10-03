using TheShedding.Characters;
using UnityEngine;

namespace TheShedding.Interactables
{
    /// <summary>
    /// 씬에 있는 아이템의 생성·제거 담당자. 게임 로직 코드가 직접
    /// Instantiate/Destroy 또는 SetActive를 호출하지 않도록 한 겹 감싼다.
    ///
    /// 오프라인 테스트용 구현과 네트워크 구현이 각각 이 인터페이스를 구현한다.
    /// </summary>
    public interface IItemSpawner
    {
        /// <summary>씬에 있던 <paramref name="pickup"/>이 방금 <paramref name="collector"/>에게
        /// 획득되었음을 알린다. 실제 오브젝트 제거·동기화는 구현체가 담당한다.</summary>
        void NotifyPickedUp(ItemPickup pickup, BaseCharacterController collector);

        /// <summary>주어진 아이템 id의 아이템을 <paramref name="position"/>에 스폰한다.</summary>
        void Spawn(int itemId, Vector3 position);
    }
}
