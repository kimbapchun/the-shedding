using TheShedding.InventorySystem;
using TheShedding.Items;
using UnityEngine;

namespace TheShedding.Characters
{
    [RequireComponent(typeof(Inventory))]
    public sealed class BabyController : FamilyController, IInventoryOwner
    {
        [Header("Steal")]
        [SerializeField] private float stealRange = 0.8f;
        [SerializeField] private LayerMask stealTargetLayer;

        [Header("Inventory")]
        [SerializeField] private ItemDatabase itemDatabase;

        public Inventory Inventory { get; private set; }

        private static readonly Collider[] StealBuffer = new Collider[8];

        protected override void Awake()
        {
            moveSpeed = 8f;
            BodyScale = 1;
            maxLifeSegments = 2;
            skillCooldownDuration = 2f;
            Inventory = GetComponent<Inventory>();
            base.Awake();
        }

        public override void OnAttackInput() { }
        protected override bool TryAttack() => false;

        protected override bool UseSkill() => TryStealItem();

        public bool TryStealItem()
        {
            int count = Physics.OverlapSphereNonAlloc(
                transform.position, stealRange, StealBuffer, stealTargetLayer);

            RobberCharacterController nearest = null;
            float minDist = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                var col = StealBuffer[i];
                if (col.gameObject == gameObject) continue;
                if (!col.TryGetComponent<RobberCharacterController>(out var robber)) continue;

                float dist = Vector3.Distance(transform.position, col.transform.position);
                if (dist < minDist) { minDist = dist; nearest = robber; }
            }

            if (nearest?.Inventory == null || itemDatabase == null || Inventory == null) return false;
            if (Inventory.IsFull && !Inventory.HasOfType<BoneItem>(itemDatabase)) return false;

            if (!nearest.Inventory.TryRemoveFirstOfType<HouseItem>(itemDatabase, out var stolen))
                return false;

            if (Inventory.TryRemoveFirstOfType<BoneItem>(itemDatabase, out var bone))
                nearest.Inventory.AddItem(bone.id);

            Inventory.AddItem(stolen.id);

            return true;
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, stealRange);
        }
    }
}
