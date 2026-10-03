using TheShedding.InventorySystem;
using TheShedding.Items;
using UnityEngine;

namespace TheShedding.Characters
{
    [RequireComponent(typeof(Inventory))]
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

        public void UseHealItem()
        {
            if (Inventory == null || itemDatabase == null) return;
            if (!Inventory.TryRemoveFirstOfType<ConsumableItem>(itemDatabase, out var item)) return;
            Recover(item.healAmount);
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
