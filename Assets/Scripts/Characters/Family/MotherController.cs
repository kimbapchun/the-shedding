using System;
using TheShedding.InventorySystem;
using TheShedding.Items;
using UnityEngine;

namespace TheShedding.Characters
{
    public sealed class MotherController : FamilyController, IInventoryOwner
    {
        [Header("Mother Attack")]
        [SerializeField] private float attackRange = 1.0f;
        [SerializeField] private LayerMask attackTargetLayer;
        [SerializeField] private int attackDamage = 1;

        [Header("Trap")]
        [SerializeField] private float trapPlacementOffset = 1.5f;

        [Header("Inventory")]
        [SerializeField] private ItemDatabase itemDatabase;

        public Inventory Inventory { get; private set; }
        public event Action<TrapType, Vector3> OnTrapPlaced;

        private TrapItemData SelectedTrap =>
            (Inventory != null && itemDatabase != null)
                ? itemDatabase.Get(Inventory.SelectedItemId) as TrapItemData
                : null;
        private GameObject trapPreviewInstance;
        private TrapItemData lastPreviewedTrap;

        private static readonly Collider[] AttackBuffer = new Collider[8];

        protected override void Awake()
        {
            moveSpeed = 4f;
            BodyScale = 4;
            maxLifeSegments = 3;
            attackCooldownDuration = 1f;
            skillCooldownDuration = 5f;
            Inventory = GetComponent<Inventory>();
            base.Awake();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            DestroyPreview();
        }

        protected override void Update()
        {
            base.Update();
            UpdateTrapPreview();
        }

        public override void OnPreviousItem() => Inventory?.SelectPrevious();
        public override void OnNextItem()     => Inventory?.SelectNext();

        // ── 함정 설치 위치 ────────────────────────────────────────────────

        private Vector3 GetPlacementPosition()
            => transform.position + transform.forward * trapPlacementOffset;

        // ── 프리뷰 ────────────────────────────────────────────────────────

        private void UpdateTrapPreview()
        {
            var trap = SelectedTrap;

            if (!CanAct() || trap == null || trap.previewPrefab == null || !IsSkillReady)
            {
                if (trapPreviewInstance != null) trapPreviewInstance.SetActive(false);
                return;
            }

            if (lastPreviewedTrap != trap)
            {
                DestroyPreview();
                CreatePreview(trap.previewPrefab);
                lastPreviewedTrap = trap;
            }

            trapPreviewInstance.SetActive(true);
            trapPreviewInstance.transform.position = GetPlacementPosition();
        }

        private void CreatePreview(GameObject prefab)
        {
            trapPreviewInstance = Instantiate(prefab);
            foreach (var col in trapPreviewInstance.GetComponentsInChildren<Collider>())
                col.enabled = false;
            // TODO: 반투명 머티리얼 적용
        }

        private void DestroyPreview()
        {
            if (trapPreviewInstance == null) return;
            Destroy(trapPreviewInstance);
            trapPreviewInstance = null;
            lastPreviewedTrap = null;
        }

        // ── 기본 공격 (좌클릭): 칼 공격 ──────────────────────────────────

        protected override bool TryAttack()
        {
            int count = Physics.OverlapSphereNonAlloc(
                transform.position, attackRange, AttackBuffer, attackTargetLayer);

            bool hit = false;
            for (int i = 0; i < count; i++)
            {
                if (AttackBuffer[i].TryGetComponent<RobberController>(out var target))
                {
                    target.TakeDamage(attackDamage);
                    hit = true;
                }
            }
            return hit;
        }

        // ── 우클릭 스킬: 함정 설치 ───────────────────────────────────────

        protected override bool UseSkill()
        {
            var trap = SelectedTrap;
            if (trap == null) return false;
            if (!Inventory.RemoveItem(trap.id)) return false;

            OnTrapPlaced?.Invoke(trap.trapType, GetPlacementPosition());
            return true;
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
            Gizmos.color = new Color(0f, 1f, 0f, 0.5f);
            Gizmos.DrawWireSphere(GetPlacementPosition(), 0.3f);
        }
    }
}
