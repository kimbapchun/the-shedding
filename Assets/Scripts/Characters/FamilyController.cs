using UnityEngine;

namespace TheShedding.Characters
{
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
            if (CurrentStatusEffect != StatusEffect.None && Time.time >= statusEffectEndTime)
                ApplyStatusEffect(StatusEffect.None, 0f);

            base.Update();
        }

        protected override bool CanAct() => base.CanAct() && !IsStunned && CurrentStatusEffect != StatusEffect.KnockedDown;

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
            if (!CanAct()) return;
            if (!IsAttackReady) return;
            if (IsLying) return;

            attackCooldownEndTime = Time.time + attackCooldownDuration;
            animator?.SetTrigger(HashIsAttacking);
            TryAttack();
        }

        protected abstract bool TryAttack();

        // ── 고유 스킬 (PlayerInputReader → OnSkillInput) ──────────────────

        public override void OnSkillInput()
        {
            if (!CanAct()) return;
            if (!IsSkillReady) return;

            if (UseSkill())
                skillCooldownEndTime = Time.time + skillCooldownDuration;
        }

        protected abstract bool UseSkill();
    }
}
