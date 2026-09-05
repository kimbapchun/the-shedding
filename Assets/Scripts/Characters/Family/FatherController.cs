using UnityEngine;

namespace TheShedding.Characters
{
    public sealed class FatherController : FamilyController
    {
        [Header("Father Attack")]
        [SerializeField] private float attackRange = 1.5f;
        [SerializeField] private LayerMask attackTargetLayer;
        [SerializeField] private int attackDamage = 1;

        protected override void Awake()
        {
            moveSpeed = 3.5f;
            BodyScale = 3;
            maxLifeSegments = 3;
            attackCooldownDuration = 1f;
            base.Awake();
        }

        // 기본 공격 (좌클릭): 근접 타격
        protected override bool TryAttack()
        {
            Collider[] hits = Physics.OverlapSphere(
                transform.position, attackRange, attackTargetLayer);

            bool hit = false;
            foreach (var col in hits)
            {
                if (col.TryGetComponent<RobberController>(out var target))
                {
                    target.TakeDamage(attackDamage);
                    hit = true;
                }
            }
            return hit;
        }

        public override void OnSkillInput() { }
        protected override bool UseSkill() => false;

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }
}
