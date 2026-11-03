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

        // 범위 안에 있는 동안 매 프레임 호출되므로, 갱신 주기를 둬 상태 복제가 매번 일어나지 않게 한다.
        private const float StunRefreshInterval = 0.5f;

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
            if (!IsStunned || stunEndTime - Time.time <= duration - StunRefreshInterval)
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
