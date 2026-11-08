using UnityEngine;

namespace TheShedding.Characters
{
    public abstract class FamilyController : BaseCharacterController
    {
        public event System.Action OnAttackStarted;
        [Header("Attack")]
        [SerializeField] protected float attackCooldownDuration = 1f;

        [Header("Skill")]
        [SerializeField] protected float skillCooldownDuration = 3f;


        private static readonly int HashIsAttacking = Animator.StringToHash("isAttacking");

        protected float attackCooldownEndTime;
        protected float skillCooldownEndTime;
        private float stunEndTime;
        private float flashlightImmunityEndTime;

        [Header("Flashlight")]
        [SerializeField, Min(0f)] private float flashlightImmunityDuration = 3f;


        private bool IsStunned   => Time.time < stunEndTime;
        protected bool IsAttackReady => Time.time >= attackCooldownEndTime;
        protected bool IsSkillReady  => Time.time >= skillCooldownEndTime;

        public float FlashlightStunEndTime => stunEndTime;

        public void SynchronizeFlashlightStun(float remaining)
        {
            stunEndTime = Time.time + Mathf.Max(0f, remaining);
            if (IsStunned) StopMovement();
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
            // 연속 조사나 여러 강도의 조사로 스턴/면역 종료 시각을 연장하지 않는다.
            if (duration <= 0f || IsStunned || Time.time < flashlightImmunityEndTime) return;
            stunEndTime = Time.time + duration;
            flashlightImmunityEndTime = stunEndTime + Mathf.Max(0f, flashlightImmunityDuration);
            StopMovement();
        }

        // ── 기본 공격 (PlayerInputReader → OnAttackInput) ─────────────────

        public override void OnAttackInput()
        {
            if (!CanAct()) return;
            if (!IsAttackReady) return;
            if (IsLying) return;

            attackCooldownEndTime = Time.time + attackCooldownDuration;
            if (!NetworkSimulation) animator?.SetTrigger(HashIsAttacking);
            OnAttackStarted?.Invoke();
            TryAttack();
        }

        protected abstract bool TryAttack();

        // ── 고유 스킬 (PlayerInputReader → OnSkillInput) ──────────────────

        public override void OnSkillInput()
        {
            if (!CanAct()) return;
            if (!IsSkillReady) return;

            if (UseSkill()) StartSkillCooldown();
        }

        protected virtual void StartSkillCooldown()
            => skillCooldownEndTime = Time.time + skillCooldownDuration;

        protected abstract bool UseSkill();
    }
}
