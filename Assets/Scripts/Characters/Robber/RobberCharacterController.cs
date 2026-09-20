using TheShedding.InventorySystem;
using TheShedding.Items;
using UnityEngine;

namespace TheShedding.Characters
{
    public sealed class RobberCharacterController : RobberController, IInventoryOwner
    {
        [Header("Flashlight")]
        [SerializeField] private float flashRange = 5f;
        [SerializeField] private float flashStunDuration = 2f;
        [SerializeField] private LayerMask familyLayer;

        [Header("Inventory")]
        [SerializeField] private ItemDatabase itemDatabase;

        public bool IsFlashlightOn { get; private set; }
        public Inventory Inventory { get; private set; }

        private static readonly Collider[] FlashlightBuffer = new Collider[16];

        protected override void Awake()
        {
            moveSpeed = 5f;
            BodyScale = 2;
            maxLifeSegments = 3;
            Inventory = GetComponent<Inventory>();
            base.Awake();
        }

        protected override void Update()
        {
            base.Update();

            if (!CanAct()) return;

            if (IsFlashlightOn)
            {
                int count = Physics.OverlapSphereNonAlloc(
                    transform.position, flashRange, FlashlightBuffer, familyLayer);

                for (int i = 0; i < count; i++)
                {
                    if (FlashlightBuffer[i].TryGetComponent<FamilyController>(out var family))
                        family.ApplyFlashlightStun(flashStunDuration);
                }
            }
        }

        public override void OnSkillInput()
        {
            if (!CanAct()) return;
            IsFlashlightOn = !IsFlashlightOn;
        }

        public override void OnPreviousItem() => Inventory?.SelectPrevious();
        public override void OnNextItem()     => Inventory?.SelectNext();

        // RobberController.Recover()를 인벤토리와 연동해 오버라이드
        public override void Recover()
        {
            if (CurrentStatusEffect == StatusEffect.KnockedDown) return;
            if (Inventory == null || itemDatabase == null) return;
            if (!Inventory.TryRemoveFirstOfType<ConsumableItemData>(itemDatabase, out var item)) return;
            ApplyHeal(item.healAmount);
            ApplyStatusEffect(StatusEffect.None, 0f);
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            Gizmos.color = IsFlashlightOn
                ? new Color(1f, 1f, 0f, 0.6f)
                : new Color(1f, 1f, 0f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, flashRange);
        }
    }
}
