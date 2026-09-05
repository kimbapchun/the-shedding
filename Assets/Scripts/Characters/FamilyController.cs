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


        private static readonly int HashIsAttacking = Animator.StringToHash("isAttacking");

        protected float attackCooldownEndTime;
        protected float skillCooldownEndTime;
        private float stunEndTime;

        private bool IsStunned   => Time.time < stunEndTime;
        protected bool IsAttackReady => Time.time >= attackCooldownEndTime;
        protected bool IsSkillReady  => Time.time >= skillCooldownEndTime;

        protected override void Update()
        {
            // 상태이상 자동 회복
            if (CurrentStatusEffect != StatusEffect.None && Time.time >= statusEffectEndTime)
                ApplyStatusEffect(StatusEffect.None, 0f);

            base.Update();
        }

        protected override bool CanAct() => base.CanAct() && !IsStunned;

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

        // ── 플래시 스턴 ───────────────────────────────────────────────────

        public void ApplyFlashlightStun(float duration)
        {
            stunEndTime = Time.time + duration;
            StopMovement();
        }

        // ── 기본 공격 (PlayerInputReader → OnAttackInput) ─────────────────

        public override void OnAttackInput()
        {
            if (!IsAlive()) return;
            if (!IsAttackReady) return;
            if (IsStunned) return;
            if (IsLying) return;

            attackCooldownEndTime = Time.time + attackCooldownDuration;
            animator?.SetTrigger(HashIsAttacking);
            TryAttack();
        }

        protected abstract bool TryAttack();

        // ── 고유 스킬 (PlayerInputReader → OnSkillInput) ──────────────────

        public override void OnSkillInput()
        {
            if (!IsAlive()) return;
            if (!IsSkillReady) return;
            if (IsStunned) return;

            if (UseSkill())
                skillCooldownEndTime = Time.time + skillCooldownDuration;
        }

        protected abstract bool UseSkill();
    }
}
