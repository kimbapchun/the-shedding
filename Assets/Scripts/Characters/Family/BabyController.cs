using UnityEngine;

namespace TheShedding.Characters
{
    public sealed class BabyController : FamilyController
    {
        [Header("Steal")]
        [SerializeField] private float stealRange = 0.8f;
        [SerializeField] private LayerMask stealTargetLayer;

        protected override void Awake()
        {
            moveSpeed = 8f;
            bodyScale = 1;
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
            Collider[] hits = Physics.OverlapSphere(
                transform.position, stealRange, stealTargetLayer);

            foreach (var col in hits)
            {
                if (col.TryGetComponent<RobberController>(out _))
                {
                    // TODO: 인벤토리 시스템 구현 후 연동
                    //       아기 인벤에 뼈다귀가 있으면 교환, 없으면 훔치기만
                    return true;
                }
            }
            return false;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, stealRange);
        }
    }
}
