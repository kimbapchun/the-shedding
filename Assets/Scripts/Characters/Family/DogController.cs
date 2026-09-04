using UnityEngine;

namespace TheShedding.Characters
{
    public sealed class DogController : FamilyController
    {
        [Header("Bite Attack")]
        [SerializeField] private float biteRange = 0.9f;
        [SerializeField] private LayerMask attackTargetLayer;
        [SerializeField] private float limpAndBleedDuration = 10f;

        [Header("Bark")]
        [SerializeField] private float barkRadius = 8f;

        protected override void Awake()
        {
            moveSpeed = 6f;
            bodyScale = 1;
            maxLifeSegments = 2;
            attackCooldownDuration = 0.8f;
            skillCooldownDuration = 5f;
            base.Awake();
        }

        // 기본 공격 (좌클릭): 물기
        // KnockedDown 강도 → 즉사 / 일반 강도 → LimpAndBleed 부여
        protected override bool TryAttack()
        {
            Collider[] hits = Physics.OverlapSphere(
                transform.position, biteRange, attackTargetLayer);

            bool hit = false;
            foreach (var col in hits)
            {
                if (!col.TryGetComponent<BaseCharacterController>(out var target)) continue;

                if (target.currentStatusEffect == StatusEffect.KnockedDown)
                    target.TakeDamage(target.currentLifeSegments);
                else
                    target.ApplyStatusEffect(StatusEffect.LimpAndBleed, limpAndBleedDuration);

                hit = true;
            }
            return hit;
        }

        // 우클릭 스킬: 짖기
        protected override bool UseSkill()
        {
            // TODO: SoundDetectionSystem 구현 후 연동
            //       SoundDetectionSystem.RevealNearbyNoise(transform.position, barkRadius)
            return true;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, biteRange);
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, barkRadius);
        }
    }
}
