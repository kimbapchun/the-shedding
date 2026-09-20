using System;
using TheShedding.InventorySystem;
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
        [SerializeField] private float sittingSpeedMultiplier = 0.6f;
        [SerializeField] private float lyingSpeedMultiplier = 0.4f;
        [SerializeField] public int BodyScale = 2;

        [Header("Trap")]
        [SerializeField] private float trapSlowMultiplier = 0.5f;
        public bool CanPassNarrowPath => BodyScale <= 2;

        [Header("Life")]
        [SerializeField] protected int maxLifeSegments = 3;

        [Header("Interact")]
        [SerializeField] protected float interactRadius = 1.2f;
        [SerializeField] protected LayerMask interactableLayer;

        // ── 런타임 상태 ──────────────────────────────────────────────────

        public int CurrentLifeSegments { get; protected set; }
        public StatusEffect CurrentStatusEffect { get; protected set; }
        protected float statusEffectEndTime;
        private float trapSlowEndTime;
        public bool IsTrapped => Time.time < trapSlowEndTime;
        public bool IsSitting { get; protected set; }
        public bool IsLying { get; protected set; }
        private bool wasJumping;

        // ── 이벤트 ───────────────────────────────────────────────────────

        public event Action<int> OnLifeChanged;
        public event Action<StatusEffect> OnStatusChanged;
        public event Action OnDied;

        // ── 컴포넌트 레퍼런스 ────────────────────────────────────────────

        protected Rigidbody rb;
        protected Animator animator;
        private Camera cam;

        // ── 상수 ─────────────────────────────────────────────────────────

        private const float BleedSpeedMultiplier = 0.5f;

        // ── 애니메이터 파라미터 해시 ──────────────────────────────────────

        protected static readonly int HashIsWalking   = Animator.StringToHash("isWalking");
        protected static readonly int HashIsSprinting = Animator.StringToHash("isSprinting");
        protected static readonly int HashIsLimping   = Animator.StringToHash("isLimping");
        protected static readonly int HashIsSitting   = Animator.StringToHash("isSitting");
        protected static readonly int HashIsLying     = Animator.StringToHash("isLying");
        protected static readonly int HashIsJumping   = Animator.StringToHash("isJumping");
        protected static readonly int HashIsDead      = Animator.StringToHash("isDead");

        private static readonly int JumpStateHash = Animator.StringToHash("Jump");

        // ── 물리 버퍼 (NonAlloc) ─────────────────────────────────────────
        private static readonly Collider[] InteractBuffer = new Collider[8];

        // ── Unity 생명주기 ────────────────────────────────────────────────

        protected virtual void Awake()
        {
            rb = GetComponent<Rigidbody>();
            animator = GetComponent<Animator>();
            cam = Camera.main;

            rb.constraints = RigidbodyConstraints.FreezeRotation;

            if (animator != null)
                animator.applyRootMotion = true;

            CurrentLifeSegments = maxLifeSegments;
        }

        protected virtual void OnEnable() { }
        protected virtual void OnDisable() { }

        private void OnAnimatorMove()
        {
            bool isJumping = animator.GetCurrentAnimatorStateInfo(0).shortNameHash == JumpStateHash;

            // Jump 중에는 애니메이션 Y를 그대로 따라가야 하므로 중력을 꺼둔다.
            // 켜두면 linearVelocity.y가 계속 누적되어 착지 순간 급락한다.
            if (isJumping != wasJumping)
            {
                rb.useGravity = !isJumping;
                if (isJumping)
                {
                    Vector3 v = rb.linearVelocity;
                    v.y = 0f;
                    rb.linearVelocity = v;
                }
            }
            wasJumping = isJumping;

            if (isJumping)
            {
                Vector3 pos = rb.position;
                pos.y += animator.deltaPosition.y;
                rb.MovePosition(pos);
            }
        }

        protected virtual void Update()
        {
#if UNITY_EDITOR
            if (Keyboard.current.pKey.isPressed)
                ApplyStatusEffect(StatusEffect.Limp, 0.5f);
            if (Keyboard.current.oKey.wasPressedThisFrame)
                TakeDamage(CurrentLifeSegments);
#endif
        }

        // ── 이동 ─────────────────────────────────────────────────────────

        protected void StopMovement()
        {
            rb.linearVelocity = Vector3.zero;
            if (animator != null)
            {
                animator.SetBool(HashIsWalking,   false);
                animator.SetBool(HashIsSprinting, false);
                animator.SetBool(HashIsLimping,   false);
            }
        }

        // PlayerInputReader가 매 프레임 호출
        public virtual void Move(Vector2 input, bool sprintPressed)
        {
            if (!IsAlive()) return;
            bool isMoving    = input != Vector2.zero;
            bool isLimping   = CurrentStatusEffect == StatusEffect.Limp || CurrentStatusEffect == StatusEffect.LimpAndBleed;
            bool isSprinting = isMoving && !isLimping && !IsSitting && !IsLying && sprintPressed;

            float multiplier = GetSpeedMultiplier();
            if (isSprinting) multiplier *= runSpeedMultiplier;

            Vector3 moveDir  = GetMoveDirection(input);
            Vector3 velocity = moveDir * moveSpeed * multiplier;
            velocity.y = rb.linearVelocity.y;
            rb.linearVelocity = velocity;

            if (animator != null)
            {
                animator.SetBool(HashIsSprinting, isSprinting);
                animator.SetBool(HashIsWalking,   isMoving && !isLimping && !isSprinting);
                animator.SetBool(HashIsLimping,   isMoving && isLimping);
            }

            if (isMoving)
            {
                Quaternion toRotation = Quaternion.LookRotation(moveDir, Vector3.up);
                transform.rotation = Quaternion.Lerp(transform.rotation, toRotation, turnSpeed * Time.deltaTime);
            }
        }

        private Vector3 GetMoveDirection(Vector2 input)
        {
            if (cam == null)
                return new Vector3(input.x, 0f, input.y);

            Vector3 forward = cam.transform.forward;
            Vector3 right   = cam.transform.right;
            forward.y = 0f;
            right.y   = 0f;
            forward.Normalize();
            right.Normalize();

            return forward * input.y + right * input.x;
        }

        protected virtual float GetSpeedMultiplier()
        {
            if (IsLying)   return lyingSpeedMultiplier;
            if (IsSitting) return sittingSpeedMultiplier;
            if (IsTrapped) return trapSlowMultiplier;
            return CurrentStatusEffect switch
            {
                StatusEffect.LimpAndBleed => BleedSpeedMultiplier,
                _ => 1f
            };
        }

        public void ApplyTrapSlow(float duration)
        {
            trapSlowEndTime = Time.time + duration;
        }

        // ── 상호작용 ──────────────────────────────────────────────────────

        public void TryInteract()
        {
            if (!CanAct()) return;

            int count = Physics.OverlapSphereNonAlloc(
                transform.position, interactRadius, InteractBuffer, interactableLayer);

            IInteractable closest = null;
            float minDist = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                var hit = InteractBuffer[i];
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

        public void TakeStatusEffect(StatusEffect type, float duration)
        {
            ApplyStatusEffect(type, duration);
        }

        protected void ApplyStatusEffect(StatusEffect type, float duration)
        {
            if (CurrentStatusEffect == StatusEffect.KnockedDown && type != StatusEffect.None)
                return;

            CurrentStatusEffect = type;
            statusEffectEndTime = Time.time + duration;
            OnStatusChanged?.Invoke(type);
            OnStatusEffectApplied(type);
        }

        protected virtual void OnStatusEffectApplied(StatusEffect type)
        {
            if (type != StatusEffect.KnockedDown) return;

            IsSitting = false;
            IsLying   = false;
            if (animator != null)
            {
                animator.SetBool(HashIsSitting, false);
                animator.SetBool(HashIsLying,   false);
            }
        }

        // ── 데미지 / 생사 ─────────────────────────────────────────────────

        // 판정: 실제 적용할 데미지 계산 (방어력 등 추가 시 override)
        protected virtual int CalculateDamage(int rawAmount) => rawAmount;

        // 반영
        protected virtual void ApplyDamage(int amount)
        {
            if (!IsAlive()) return;
            CurrentLifeSegments = Mathf.Max(0, CurrentLifeSegments - amount);
            OnLifeChanged?.Invoke(CurrentLifeSegments);
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
            CurrentLifeSegments = Mathf.Min(CurrentLifeSegments + amount, maxLifeSegments);
            OnLifeChanged?.Invoke(CurrentLifeSegments);
            OnHealTaken(amount);
        }

        // 외부 호출: 판정 → 반영
        public void TakeDamage(int rawAmount)
        {
            ApplyDamage(CalculateDamage(rawAmount));
        }

        public void SetCamera(Camera camera) { cam = camera; }

        public bool IsAlive() => CurrentLifeSegments > 0;
        protected virtual bool CanAct() => IsAlive();

        protected virtual void OnDamageTaken(int amount) { }
        protected virtual void OnHealTaken(int amount) { }
        protected virtual void OnDeath()
        {
            StopMovement();
            animator?.SetBool(HashIsDead, true);
        }

        // ── 앉기 / 눕기 ───────────────────────────────────────────────────

        public virtual void SetSittingState(bool sitting)
        {
            if (!CanAct()) return;

            IsSitting = sitting;
            if (sitting)
            {
                IsLying = false;
                rb.linearVelocity = Vector3.zero;
                if (animator != null) animator.SetBool(HashIsLying, false);
            }

            if (animator != null)
                animator.SetBool(HashIsSitting, sitting);
        }

        public virtual void SetLyingState(bool lying)
        {
            if (!CanAct()) return;

            IsLying = lying;
            if (lying)
            {
                IsSitting = false;
                rb.linearVelocity = Vector3.zero;
                if (animator != null) animator.SetBool(HashIsSitting, false);
            }

            if (animator != null)
                animator.SetBool(HashIsLying, lying);
        }

        // ── 입력 진입점 (PlayerInputReader → 서브클래스 override) ─────────

        public virtual void OnAttackInput() { }
        public virtual void OnSkillInput() { }
        public virtual void OnJumpInput()
        {
            if (!CanAct()) return;
            animator?.SetTrigger(HashIsJumping);
        }
        // 인벤토리를 가진 캐릭터는 자동으로 선택 슬롯을 순회한다.
        // 서브클래스가 다른 동작을 원하면 override로 대체한다.
        public virtual void OnPreviousItem()
        {
            if (this is IInventoryOwner owner) owner.Inventory?.SelectPrevious();
        }

        public virtual void OnNextItem()
        {
            if (this is IInventoryOwner owner) owner.Inventory?.SelectNext();
        }

        // ── 에디터 Gizmo ──────────────────────────────────────────────────

        protected virtual void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, interactRadius);
        }
    }
}
