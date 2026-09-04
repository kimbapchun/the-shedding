using UnityEngine;

namespace TheShedding.Characters
{
    public sealed class MotherController : FamilyController
    {
        [Header("Mother Attack")]
        [SerializeField] private float attackRange = 1.0f;
        [SerializeField] private LayerMask attackTargetLayer;
        [SerializeField] private int attackDamage = 1;

        [Header("Trap")]
        [SerializeField] private GameObject[] trapPrefabs;
        [SerializeField] private float trapPlacementOffset = 1.5f;

        private TrapType selectedTrapType;
        private GameObject trapPreviewInstance;

        protected override void Awake()
        {
            moveSpeed = 4f;
            bodyScale = 4;
            maxLifeSegments = 3;
            attackCooldownDuration = 1f;
            skillCooldownDuration = 5f;
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

        // ── 함정 설치 위치 ────────────────────────────────────────────────

        private Vector3 GetPlacementPosition()
        {
            return transform.position + transform.forward * trapPlacementOffset;
        }

        // ── 프리뷰 ────────────────────────────────────────────────────────

        private void UpdateTrapPreview()
        {
            int idx = (int)selectedTrapType;
            bool canPlace = IsSkillReady
                && trapPrefabs != null
                && idx < trapPrefabs.Length
                && trapPrefabs[idx] != null;

            if (!canPlace)
            {
                if (trapPreviewInstance != null)
                    trapPreviewInstance.SetActive(false);
                return;
            }

            // 함정 타입이 바뀌었으면 프리뷰 재생성
            if (trapPreviewInstance == null)
                CreatePreview(idx);

            trapPreviewInstance.SetActive(true);
            trapPreviewInstance.transform.position = GetPlacementPosition();
        }

        private void CreatePreview(int idx)
        {
            DestroyPreview();
            trapPreviewInstance = Instantiate(trapPrefabs[idx]);

            // 함정 기능 비활성화 (콜라이더, 트리거 등)
            foreach (var col in trapPreviewInstance.GetComponentsInChildren<Collider>())
                col.enabled = false;

            // TODO: 반투명 머티리얼 적용 (에디터에서 별도 머티리얼 지정 필요)
        }

        private void DestroyPreview()
        {
            if (trapPreviewInstance != null)
            {
                Destroy(trapPreviewInstance);
                trapPreviewInstance = null;
            }
        }

        // ── 기본 공격 (좌클릭): 칼 공격 ──────────────────────────────────

        protected override bool TryAttack()
        {
            Collider[] hits = Physics.OverlapSphere(
                transform.position, attackRange, attackTargetLayer);

            bool hit = false;
            foreach (var col in hits)
            {
                if (col.TryGetComponent<BaseCharacterController>(out var target))
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
            int idx = (int)selectedTrapType;
            if (trapPrefabs == null || idx >= trapPrefabs.Length || trapPrefabs[idx] == null)
                return false;

            Instantiate(trapPrefabs[idx], GetPlacementPosition(), Quaternion.identity);
            DestroyPreview();
            return true;
        }

        protected override void OnDeath()
        {
            // TODO: GameManager.Instance.OnFamilyMemberDied(this)
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
            Gizmos.color = new Color(0f, 1f, 0f, 0.5f);
            Gizmos.DrawWireSphere(GetPlacementPosition(), 0.3f);
        }
    }
}
