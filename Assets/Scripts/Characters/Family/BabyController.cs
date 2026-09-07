using UnityEngine;

namespace TheShedding.Characters
{
    public sealed class BabyController : FamilyController
    {
        [Header("Steal")]
        [SerializeField] private float stealRange = 0.8f;
        [SerializeField] private LayerMask stealTargetLayer;

        private static readonly Collider[] StealBuffer = new Collider[8];

        protected override void Awake()
        {
            moveSpeed = 8f;
            BodyScale = 1;
            maxLifeSegments = 2;
            skillCooldownDuration = 2f;
            base.Awake();
        }

        public override void OnAttackInput() { }

        protected override bool TryAttack() => false;

        // 우클릭 스킬: 훔치기
        protected override bool UseSkill() => TryStealItem();

        public bool TryStealItem()
        {
            int count = Physics.OverlapSphereNonAlloc(
                transform.position, stealRange, StealBuffer, stealTargetLayer);

            RobberController nearest = null;
            float minDist = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                var col = StealBuffer[i];
                if (col.gameObject == gameObject) continue;
                if (!col.TryGetComponent<RobberController>(out var robber)) continue;

                float dist = Vector3.Distance(transform.position, col.transform.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = robber;
                }
            }

            if (nearest == null) return false;

            // TODO: 인벤토리 시스템 구현 후 연동
            //       아기 인벤에 뼈다귀가 있으면 교환, 없으면 훔치기만
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
