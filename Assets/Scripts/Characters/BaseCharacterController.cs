using UnityEngine;
using UnityEngine.InputSystem;

namespace TheShedding.Characters
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(PlayerInput))]
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
        protected float statusEffectTimer;
        public bool isSitting { get; protected set; }
        public bool isLying { get; protected set; }

        // ── 컴포넌트 레퍼런스 ────────────────────────────────────────────

        protected Rigidbody rb;
        protected Animator animator;
        protected PlayerInput playerInput;

        private InputAction moveAction;
        private InputAction sprintAction;
        private InputAction interactAction;
        private InputAction sitAction;
        private InputAction lieAction;
        private InputAction previousAction;
        private InputAction nextAction;

        // ── 상수 ─────────────────────────────────────────────────────────

        private const float LIMP_SPEED_MULTIPLIER  = 0.75f;
        private const float BLEED_SPEED_MULTIPLIER = 0.5f;

        // ── Unity 생명주기 ────────────────────────────────────────────────

        protected virtual void Awake()
        {
            rb = GetComponent<Rigidbody>();
            animator = GetComponent<Animator>();
            playerInput = GetComponent<PlayerInput>();

            rb.constraints = RigidbodyConstraints.FreezeRotation;

            if (animator != null)
                animator.applyRootMotion = false;

            currentLifeSegments = maxLifeSegments;
            canPassNarrowPath = bodyScale <= 2;

            moveAction     = playerInput.actions["Move"];
            sprintAction   = playerInput.actions["Sprint"];
            interactAction = playerInput.actions["Interact"];
            sitAction      = playerInput.actions["Sit"];
            lieAction      = playerInput.actions["Lie"];
            previousAction = playerInput.actions["Previous"];
            nextAction     = playerInput.actions["Next"];
        }

        protected virtual void OnEnable()
        {
            interactAction.performed += OnInteractPerformed;
            sitAction.performed      += OnSitPerformed;
            lieAction.performed      += OnLiePerformed;
            previousAction.performed += OnPreviousPerformed;
            nextAction.performed     += OnNextPerformed;
        }

        protected virtual void OnDisable()
        {
            interactAction.performed -= OnInteractPerformed;
            sitAction.performed      -= OnSitPerformed;
            lieAction.performed      -= OnLiePerformed;
            previousAction.performed -= OnPreviousPerformed;
            nextAction.performed     -= OnNextPerformed;
        }

        protected virtual void Update()
        {
            Vector2 input = moveAction.ReadValue<Vector2>();
            Move(input);
        }

        // ── 이동 ─────────────────────────────────────────────────────────

        // input은 XZ 평면 이동 — Vector2(x, y) → Vector3(x, 0, y)
        public virtual void Move(Vector2 input)
        {
            bool isMoving   = input != Vector2.zero;
            bool isLimping  = currentStatusEffect == StatusEffect.Limp || currentStatusEffect == StatusEffect.LimpAndBleed;
            bool isSprinting = isMoving && !isLimping && sprintAction.ReadValue<float>() > 0f;

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

        private void OnInteractPerformed(InputAction.CallbackContext ctx) => TryInteract();

        // ── 상태이상 ──────────────────────────────────────────────────────

        public void ApplyStatusEffect(StatusEffect type, float duration)
        {
            if (currentStatusEffect == StatusEffect.KnockedDown && type != StatusEffect.None)
                return;

            currentStatusEffect = type;
            statusEffectTimer = duration;
            OnStatusEffectApplied(type);
        }

        protected virtual void OnStatusEffectApplied(StatusEffect type) { }

        // ── 데미지 / 생사 ─────────────────────────────────────────────────

        public virtual void TakeDamage(int amount)
        {
            if (!IsAlive()) return;
            currentLifeSegments = Mathf.Max(0, currentLifeSegments - amount);
            OnDamageTaken(amount);
            if (!IsAlive()) OnDeath();
        }

        public bool IsAlive() => currentLifeSegments > 0;

        protected virtual void OnDamageTaken(int amount) { }
        protected virtual void OnDeath() { }

        // ── 앉기 ──────────────────────────────────────────────────────────

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

        private void OnSitPerformed(InputAction.CallbackContext ctx) => SetSittingState(!isSitting);
        private void OnLiePerformed(InputAction.CallbackContext ctx) => SetLyingState(!isLying);
        private void OnPreviousPerformed(InputAction.CallbackContext ctx) => OnPreviousItem();
        private void OnNextPerformed(InputAction.CallbackContext ctx) => OnNextItem();

        protected virtual void OnPreviousItem() { }
        protected virtual void OnNextItem() { }

        // ── 에디터 Gizmo ──────────────────────────────────────────────────

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, interactRadius);
        }
    }
}
