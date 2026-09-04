using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheShedding.Characters
{
    [RequireComponent(typeof(Rigidbody))]
    public abstract class BaseCharacterController : MonoBehaviour
    {
        // ── 인스펙터 설정 ────────────────────────────────────────────────

        [Header("Movement")]
        [SerializeField] protected float moveSpeed = 5f;
        [SerializeField] private float runSpeedMultiplier = 1.5f;
        [SerializeField] private float turnSpeed = 10f;
        [SerializeField] public int bodyScale = 2;
        [SerializeField] public bool canPassNarrowPath;

        [Header("Life")]
        [SerializeField] protected int maxLifeSegments = 3;

        [Header("Interact")]
        [SerializeField] protected float interactRadius = 1.2f;
        [SerializeField] protected LayerMask interactableLayer;

        // ── 런타임 상태 ──────────────────────────────────────────────────

        public int currentLifeSegments { get; protected set; }
        public StatusEffect currentStatusEffect { get; protected set; }
        protected float statusEffectEndTime;
        public bool isSitting { get; protected set; }
        public bool isLying { get; protected set; }

        // ── 이벤트 ───────────────────────────────────────────────────────

        public event Action<int> OnLifeChanged;
        public event Action<StatusEffect> OnStatusChanged;
        public event Action OnDied;

        // ── 컴포넌트 레퍼런스 ────────────────────────────────────────────

        protected Rigidbody rb;
        protected Animator animator;

        // ── 상수 ─────────────────────────────────────────────────────────

        private const float LIMP_SPEED_MULTIPLIER  = 0.75f;
        private const float BLEED_SPEED_MULTIPLIER = 0.5f;

        // ── Unity 생명주기 ────────────────────────────────────────────────

        protected virtual void Awake()
        {
            rb = GetComponent<Rigidbody>();
            animator = GetComponent<Animator>();

            rb.constraints = RigidbodyConstraints.FreezeRotation;

            if (animator != null)
                animator.applyRootMotion = false;

            currentLifeSegments = maxLifeSegments;
            canPassNarrowPath = bodyScale <= 2;
        }

        protected virtual void OnEnable() { }
        protected virtual void OnDisable() { }

        protected virtual void Update()
        {
#if UNITY_EDITOR
            if (Keyboard.current.pKey.isPressed)
                ApplyStatusEffect(StatusEffect.Limp, 0.5f);
#endif
        }

        // ── 이동 ─────────────────────────────────────────────────────────

        // PlayerInputReader가 매 프레임 호출
        public virtual void Move(Vector2 input, bool sprintPressed)
        {
            bool isMoving    = input != Vector2.zero;
            bool isLimping   = currentStatusEffect == StatusEffect.Limp || currentStatusEffect == StatusEffect.LimpAndBleed;
            bool isSprinting = isMoving && !isLimping && sprintPressed;

            float multiplier = GetSpeedMultiplier();
            if (isSprinting) multiplier *= runSpeedMultiplier;

            Vector3 velocity = new Vector3(input.x, 0f, input.y) * moveSpeed * multiplier;
            rb.linearVelocity = velocity;

            if (animator != null)
            {
                animator.SetBool("isSprinting", isSprinting);
                animator.SetBool("isWalking",  isMoving && !isLimping && !isSprinting);
                animator.SetBool("isLimping",  isMoving && isLimping);
            }

            if (velocity != Vector3.zero)
            {
                Quaternion toRotation = Quaternion.LookRotation(velocity, Vector3.up);
                transform.rotation = Quaternion.Lerp(transform.rotation, toRotation, turnSpeed * Time.deltaTime);
            }
        }

        protected virtual float GetSpeedMultiplier()
        {
            return currentStatusEffect switch
            {
                StatusEffect.Limp         => LIMP_SPEED_MULTIPLIER,
                StatusEffect.LimpAndBleed => BLEED_SPEED_MULTIPLIER,
                _ => 1f
            };
        }

        // ── 상호작용 ──────────────────────────────────────────────────────

        public void TryInteract()
        {
            Collider[] hits = Physics.OverlapSphere(
                transform.position, interactRadius, interactableLayer);

            IInteractable closest = null;
            float minDist = float.MaxValue;

            foreach (var hit in hits)
            {
                if (!hit.TryGetComponent<IInteractable>(out var interactable)) continue;
                if (!interactable.CanInteract(this)) continue;

                float dist = Vector3.Distance(transform.position, hit.transform.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    closest = interactable;
                }
            }

            closest?.Interact(this);
        }

        // ── 상태이상 ──────────────────────────────────────────────────────

        public void ApplyStatusEffect(StatusEffect type, float duration)
        {
            if (currentStatusEffect == StatusEffect.KnockedDown && type != StatusEffect.None)
                return;

            currentStatusEffect = type;
            statusEffectEndTime = Time.time + duration;
            OnStatusChanged?.Invoke(type);
            OnStatusEffectApplied(type);
        }

        protected virtual void OnStatusEffectApplied(StatusEffect type) { }

        // ── 데미지 / 생사 ─────────────────────────────────────────────────

        // 판정: 실제 적용할 데미지 계산 (방어력 등 추가 시 override)
        protected virtual int CalculateDamage(int rawAmount) => rawAmount;

        // 반영
        public virtual void ApplyDamage(int amount)
        {
            if (!IsAlive()) return;
            currentLifeSegments = Mathf.Max(0, currentLifeSegments - amount);
            OnLifeChanged?.Invoke(currentLifeSegments);
            OnDamageTaken(amount);
            if (!IsAlive())
            {
                OnDied?.Invoke();
                OnDeath();
            }
        }

        // 회복
        public virtual void ApplyHeal(int amount)
        {
            if (!IsAlive()) return;
            currentLifeSegments = Mathf.Min(currentLifeSegments + amount, maxLifeSegments);
            OnLifeChanged?.Invoke(currentLifeSegments);
            OnHealTaken(amount);
        }

        // 외부 호출: 판정 → 반영
        public void TakeDamage(int rawAmount)
        {
            ApplyDamage(CalculateDamage(rawAmount));
        }

        public bool IsAlive() => currentLifeSegments > 0;

        protected virtual void OnDamageTaken(int amount) { }
        protected virtual void OnHealTaken(int amount) { }
        protected virtual void OnDeath() { }

        // ── 앉기 / 눕기 ───────────────────────────────────────────────────

        public virtual void SetSittingState(bool sitting)
        {
            if (currentStatusEffect == StatusEffect.KnockedDown) return;

            isSitting = sitting;
            if (sitting)
            {
                isLying = false;
                rb.linearVelocity = Vector3.zero;
                if (animator != null) animator.SetBool("isLying", false);
            }

            if (animator != null)
                animator.SetBool("isSitting", sitting);
        }

        public virtual void SetLyingState(bool lying)
        {
            if (currentStatusEffect == StatusEffect.KnockedDown) return;

            isLying = lying;
            if (lying)
            {
                isSitting = false;
                rb.linearVelocity = Vector3.zero;
                if (animator != null) animator.SetBool("isSitting", false);
            }

            if (animator != null)
                animator.SetBool("isLying", lying);
        }

        // ── 입력 진입점 (PlayerInputReader → 서브클래스 override) ─────────

        public virtual void OnAttackInput() { }
        public virtual void OnSkillInput() { }
        public virtual void OnPreviousItem() { }
        public virtual void OnNextItem() { }

        // ── 에디터 Gizmo ──────────────────────────────────────────────────

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, interactRadius);
        }
    }
}
