using TheShedding.Characters;
using TheShedding.InventorySystem;
using TheShedding.Items;
using UnityEngine;

namespace TheShedding.Interactables
{
    /// <summary>
    /// 씬에 배치된 아이템. 상호작용 시 인벤토리에 id를 넣고,
    /// 오브젝트 자체의 제거는 <see cref="IItemSpawner"/>에 위임한다.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ItemPickup : MonoBehaviour, IInteractable
    {
        [SerializeField] private ItemData itemData;

        // 씬/스포너가 주입한다. null이면 로컬 fallback (SetActive)로 동작한다.
        public IItemSpawner Spawner { get; set; }

        public int ItemId => itemData != null ? itemData.id : 0;
        public string DisplayName => itemData != null ? itemData.displayName : string.Empty;

        // TryInteract의 OverlapSphere가 Interactable 레이어로만 검색하므로
        // 프리팹 세팅이 어긋나면 조용히 감지 실패한다. 로그로 알린다.
        private void Awake()
        {
            int interactableLayer = LayerMask.NameToLayer("Interactable");
            if (interactableLayer < 0)
                Debug.LogWarning($"[ItemPickup] 'Interactable' 레이어가 정의되지 않았습니다.", this);
            else if (gameObject.layer != interactableLayer)
                Debug.LogWarning($"[ItemPickup] '{name}'가 Interactable 레이어가 아닙니다. 상호작용 감지 안 됨.", this);
        }

        // ── 판정 (B-2: "주울 수 있나?"와 "인벤토리에 넣기"는 분리) ────────

        public bool CanInteract(BaseCharacterController interactor)
        {
            if (itemData == null) return false;
            if (interactor is not IInventoryOwner owner) return false;
            if (owner.Inventory == null || owner.Inventory.IsFull) return false;
            return CanPickUp(interactor);
        }

        // 아이템 타입별 픽업 가능 캐릭터 제한. 새 타입이 늘면 ItemData로 이 판정을
        // 위임하는 방향으로 리팩터링한다.
        private bool CanPickUp(BaseCharacterController interactor)
        {
            if (itemData is HouseItemData or ConsumableItemData)
                return interactor is RobberController;
            return false;
        }

        // ── 반영 ─────────────────────────────────────────────────────────

        public void Interact(BaseCharacterController interactor)
        {
            if (!CanInteract(interactor)) return;
            var owner = (IInventoryOwner)interactor;

            if (!owner.Inventory.AddItem(itemData.id)) return;

            if (Spawner != null)
                Spawner.NotifyPickedUp(this, interactor);
            else
                gameObject.SetActive(false); // 오프라인·에디터 fallback
        }

        public string GetInteractPrompt(BaseCharacterController interactor)
            => $"줍기: {DisplayName}";
    }
}
