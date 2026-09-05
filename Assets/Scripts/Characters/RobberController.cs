using UnityEngine;

namespace TheShedding.Characters
{
    // 강도 공통 추상 계층
    public abstract class RobberController : BaseCharacterController
    {
        [Header("KnockedDown Recovery")]
        [SerializeField] private float struggleGaugeMax = 100f;

        private float struggleGauge;

        // ── 이동 (KnockedDown 중 차단) ────────────────────────────────────

        public override void Move(Vector2 input, bool sprintPressed)
        {
            if (CurrentStatusEffect == StatusEffect.KnockedDown)
            {
                StopMovement();
                return;
            }
            base.Move(input, sprintPressed);
        }

        // ── Unity 생명주기 ────────────────────────────────────────────────

        protected override void Update()
        {
            if (CurrentStatusEffect == StatusEffect.KnockedDown)
                return;

            base.Update();
        }

        protected override bool CanAct() =>
            base.CanAct() && CurrentStatusEffect != StatusEffect.KnockedDown;

        // ── 버둥거림 게이지 ───────────────────────────────────────────────

        public void AddStruggleProgress(float amount)
        {
            if (CurrentStatusEffect != StatusEffect.KnockedDown) return;

            struggleGauge += amount;
            if (struggleGauge >= struggleGaugeMax)
            {
                struggleGauge = 0f;
                ApplyStatusEffect(StatusEffect.None, 0f);
            }
        }

        // ── 아이템 회복 ───────────────────────────────────────────────────

        // 붕대·햄버거 등 회복 아이템이 공통으로 호출
        public void Recover()
        {
            if (CurrentStatusEffect == StatusEffect.KnockedDown) return;
            ApplyHeal(1);
            ApplyStatusEffect(StatusEffect.None, 0f);
        }
    }
}
