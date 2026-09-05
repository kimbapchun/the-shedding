using UnityEngine;

namespace TheShedding.Characters
{
    // 렙틸리언 가족 공통 추상 계층
    // - 상태이상: 시간이 지나면 자동 회복
    // - 플래시 스턴: 강도의 플래시라이트에 의해 일시 행동 불가
    public abstract class FamilyController : BaseCharacterController
    {
        [Header("Attack")]
        [SerializeField] protected float attackCooldownDuration = 1f;

        [Header("Skill")]
        [SerializeField] protected float skillCooldownDuration = 3f;

        [Header("Trap Slow")]
        [SerializeField] private float trapSlowMultiplier = 0.5f;

        [Header("Flashlight Stun")]
        [SerializeField] private float defaultStunDuration = 2f;

        protected float attackCooldownEndTime;
        protected float skillCooldownEndTime;
        private float trapSlowEndTime;
        private float stunEndTime;

        public bool IsTrapped    => Time.time < trapSlowEndTime;
        private bool IsStunned   => Time.time < stunEndTime;
        protected bool IsAttackReady => Time.time >= attackCooldownEndTime;
        protected bool IsSkillReady  => Time.time >= skillCooldownEndTime;

        protected override void Update()
        {
            // 상태이상 자동 회복
            if (currentStatusEffect != StatusEffect.None && Time.time >= statusEffectEndTime)
                ApplyStatusEffect(StatusEffect.None, 0f);

            base.Update();
        }

        // ── 이동 (스턴 중 차단) ───────────────────────────────────────────

        public override void Move(Vector2 input, bool sprintPressed)
        {
            if (IsStunned)
            {
                StopMovement();
                return;
            }
            base.Move(input, sprintPressed);
        }

        // ── 함정 감속 ─────────────────────────────────────────────────────

        public void ApplyTrapSlow(float duration)
        {
            trapSlowEndTime = Time.time + duration;
        }

        protected override float GetSpeedMultiplier()
        {
            if (IsTrapped) return trapSlowMultiplier;
            return base.GetSpeedMultiplier();
        }

        // ── 플래시 스턴 ───────────────────────────────────────────────────

        public void ApplyFlashlightStun(float duration)
        {
            float d = duration > 0f ? duration : defaultStunDuration;
            stunEndTime = Time.time + d;
            StopMovement();
        }

        // ── 기본 공격 (PlayerInputReader → OnAttackInput) ─────────────────

        public override void OnAttackInput()
        {
            if (!IsAttackReady) return;
            if (IsStunned) return;
            if (isLying) return;

            attackCooldownEndTime = Time.time + attackCooldownDuration;
            animator?.SetTrigger("isAttacking");
            TryAttack();
        }

        protected abstract bool TryAttack();

        // ── 고유 스킬 (PlayerInputReader → OnSkillInput) ──────────────────

        public override void OnSkillInput()
        {
            if (!IsSkillReady) return;
            if (IsStunned) return;

            if (UseSkill())
                skillCooldownEndTime = Time.time + skillCooldownDuration;
        }

        protected abstract bool UseSkill();
    }
}
