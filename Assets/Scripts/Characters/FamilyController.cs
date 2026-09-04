using UnityEngine;
using UnityEngine.InputSystem;

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

        private InputAction attackAction;
        private InputAction skillAction;

        public bool IsTrapped  => Time.time < trapSlowEndTime;
        private bool IsStunned => Time.time < stunEndTime;
        protected bool IsAttackReady => Time.time >= attackCooldownEndTime;
        protected bool IsSkillReady  => Time.time >= skillCooldownEndTime;

        protected override void Awake()
        {
            base.Awake();
            attackAction = playerInput.actions["Attack"];
            skillAction  = playerInput.actions["Skill"];
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            attackAction.performed += OnAttackPerformed;
            skillAction.performed  += OnSkillPerformed;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            attackAction.performed -= OnAttackPerformed;
            skillAction.performed  -= OnSkillPerformed;
        }

        protected override void Update()
        {
            // 상태이상 자동 회복 — 스턴 중에도 시간은 흐름
            if (currentStatusEffect != StatusEffect.None && Time.time >= statusEffectEndTime)
                ApplyStatusEffect(StatusEffect.None, 0f);

            // 스턴 중: 모든 이동·행동 차단
            if (IsStunned)
            {
                rb.linearVelocity = Vector3.zero;
                return;
            }

            base.Update();
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
            rb.linearVelocity = Vector3.zero;
        }

        // ── 기본 공격 (좌클릭) ────────────────────────────────────────────

        private void OnAttackPerformed(InputAction.CallbackContext ctx)
        {
            if (!IsAttackReady) return;
            if (IsStunned) return;

            animator?.SetTrigger("isAttacking");
            if (TryAttack())
                attackCooldownEndTime = Time.time + attackCooldownDuration;
        }

        protected abstract bool TryAttack();

        // ── 고유 스킬 (우클릭) ────────────────────────────────────────────

        private void OnSkillPerformed(InputAction.CallbackContext ctx)
        {
            if (!IsSkillReady) return;
            if (IsStunned) return;

            if (UseSkill())
                skillCooldownEndTime = Time.time + skillCooldownDuration;
        }

        protected abstract bool UseSkill();
    }
}
