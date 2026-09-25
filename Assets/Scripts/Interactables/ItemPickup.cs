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

        // 씬에 배치된 IItemSpawner(Local/Network)가 초기화 시점에 주입한다.
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
            return itemData.CanBePickedUpBy(interactor);
        }

        // ── 반영 ─────────────────────────────────────────────────────────

        public void Interact(BaseCharacterController interactor)
        {
            if (!CanInteract(interactor)) return;

            if (Spawner == null)
            {
                Debug.LogError(
                    $"[ItemPickup] '{name}' Spawner 미주입 — 픽업 취소. " +
                    "씬에 LocalItemSpawner 또는 NetworkItemSpawner가 배치돼 있는지 확인하세요.", this);
                return;
            }

            var owner = (IInventoryOwner)interactor;
            if (!owner.Inventory.AddItem(itemData.id)) return;
            Spawner.NotifyPickedUp(this, interactor);
        }

        public string GetInteractPrompt(BaseCharacterController interactor)
            => $"줍기: {DisplayName}";
    }
}
