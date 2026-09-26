using System;
using TheShedding.InventorySystem;
using TheShedding.Items;
using UnityEngine;

namespace TheShedding.Characters
{
    [RequireComponent(typeof(Inventory))]
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

        // 선택이 바뀔 때만 갱신되는 캐시. Update에서 매 프레임 database 조회를 피한다.
        private TrapItem selectedTrap;
        private GameObject trapPreviewInstance;

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

        protected override void OnEnable()
        {
            base.OnEnable();
            if (Inventory == null) return;
            Inventory.OnSelectedItemChanged += HandleSelectionChanged;
            HandleSelectionChanged(Inventory.SelectedItemId);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (Inventory != null)
                Inventory.OnSelectedItemChanged -= HandleSelectionChanged;
            DestroyPreview();
            selectedTrap = null;
        }

        protected override void Update()
        {
            base.Update();
            UpdateTrapPreview();
        }

        // ── 함정 설치 위치 ────────────────────────────────────────────────

        private Vector3 GetPlacementPosition()
            => transform.position + transform.forward * trapPlacementOffset;

        // ── 프리뷰 ────────────────────────────────────────────────────────

        private void HandleSelectionChanged(int id)
        {
            var next = itemDatabase != null ? itemDatabase.Get(id) as TrapItem : null;
            if (selectedTrap == next) return;
            selectedTrap = next;

            DestroyPreview();
            if (selectedTrap != null && selectedTrap.previewPrefab != null)
                CreatePreview(selectedTrap.previewPrefab);
        }

        private void UpdateTrapPreview()
        {
            if (trapPreviewInstance == null) return;

            bool show = CanAct() && selectedTrap != null && IsSkillReady;
            trapPreviewInstance.SetActive(show);
            if (show)
                trapPreviewInstance.transform.position = GetPlacementPosition();
        }

        private void CreatePreview(GameObject prefab)
        {
            trapPreviewInstance = Instantiate(prefab);
            trapPreviewInstance.SetActive(false);
            foreach (var col in trapPreviewInstance.GetComponentsInChildren<Collider>())
                col.enabled = false;
            // TODO: 반투명 머티리얼 적용
        }

        private void DestroyPreview()
        {
            if (trapPreviewInstance == null) return;
            Destroy(trapPreviewInstance);
            trapPreviewInstance = null;
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
            // RemoveItem이 EnsureSelectionValid를 동기로 부르면 HandleSelectionChanged가 실행되어
            // selectedTrap이 다음 아이템으로 바뀐다. 이벤트 발화 전에 로컬 스냅샷을 잡아둔다.
            var trap = selectedTrap;
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
