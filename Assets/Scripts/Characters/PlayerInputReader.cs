using UnityEngine;
using UnityEngine.InputSystem;

namespace TheShedding.Characters
{
    [RequireComponent(typeof(BaseCharacterController))]
    [RequireComponent(typeof(PlayerInput))]
    public class PlayerInputReader : MonoBehaviour
    {
        // 스크롤 델타 축은 Button 인터랙션과 궁합이 안 맞아 InputActions로 잡지 않고
        // 여기서 직접 폴링한다. Button press point(0.5)에 못 미치는 작은 델타도 취급하고,
        // 관성/오실레이션으로 인한 중복 발화를 쿨다운으로 억제한다.
        [Header("Scroll Wheel")]
        [SerializeField] private float scrollActivationThreshold = 0.05f;
        [SerializeField] private float scrollCooldown = 0.12f;

        private BaseCharacterController controller;
        private PlayerInput playerInput;

        private Vector2 moveInput;
        private bool sprintInput;
        private float lastScrollFireTime;

        private InputAction moveAction;
        private InputAction sprintAction;
        private InputAction attackAction;
        private InputAction skillAction;
        private InputAction interactAction;
        private InputAction sitAction;
        private InputAction lieAction;
        private InputAction jumpAction;
        private InputAction previousAction;
        private InputAction nextAction;

        private void Awake()
        {
            controller = GetComponent<BaseCharacterController>();
            playerInput = GetComponent<PlayerInput>();

            moveAction     = playerInput.actions["Move"];
            sprintAction   = playerInput.actions["Sprint"];
            attackAction   = playerInput.actions["Attack"];
            skillAction    = playerInput.actions["Skill"];
            interactAction = playerInput.actions["Interact"];
            sitAction      = playerInput.actions["Sit"];
            lieAction      = playerInput.actions["Lie"];
            jumpAction     = playerInput.actions["Jump"];
            previousAction = playerInput.actions["Previous"];
            nextAction     = playerInput.actions["Next"];
        }

        private void OnEnable()
        {
            attackAction.performed   += OnAttackPerformed;
            skillAction.performed    += OnSkillPerformed;
            jumpAction.performed     += OnJumpPerformed;
            interactAction.performed += OnInteractPerformed;
            sitAction.performed      += OnSitPerformed;
            lieAction.performed      += OnLiePerformed;
            previousAction.performed += OnPreviousPerformed;
            nextAction.performed     += OnNextPerformed;
        }

        private void OnDisable()
        {
            attackAction.performed   -= OnAttackPerformed;
            skillAction.performed    -= OnSkillPerformed;
            jumpAction.performed     -= OnJumpPerformed;
            interactAction.performed -= OnInteractPerformed;
            sitAction.performed      -= OnSitPerformed;
            lieAction.performed      -= OnLiePerformed;
            previousAction.performed -= OnPreviousPerformed;
            nextAction.performed     -= OnNextPerformed;
        }

        private void Update()
        {
            moveInput  = moveAction.ReadValue<Vector2>();
            sprintInput = sprintAction.ReadValue<float>() > 0f;

            var mouse = Mouse.current;
            if (mouse == null) return;

            float scrollY = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scrollY) < scrollActivationThreshold) return;
            if (Time.unscaledTime - lastScrollFireTime < scrollCooldown) return;

            if (scrollY > 0f) controller.OnNextItem();
            else              controller.OnPreviousItem();
            lastScrollFireTime = Time.unscaledTime;
        }

        private void FixedUpdate()
        {
            controller.Move(moveInput, sprintInput);
        }

        private void OnAttackPerformed(InputAction.CallbackContext ctx)   => controller.OnAttackInput();
        private void OnSkillPerformed(InputAction.CallbackContext ctx)    => controller.OnSkillInput();
        private void OnJumpPerformed(InputAction.CallbackContext ctx)     => controller.OnJumpInput();
        private void OnInteractPerformed(InputAction.CallbackContext ctx) => controller.TryInteract();
        private void OnSitPerformed(InputAction.CallbackContext ctx)      => controller.SetSittingState(!controller.IsSitting);
        private void OnLiePerformed(InputAction.CallbackContext ctx)      => controller.SetLyingState(!controller.IsLying);
        private void OnPreviousPerformed(InputAction.CallbackContext ctx) => controller.OnPreviousItem();
        private void OnNextPerformed(InputAction.CallbackContext ctx)     => controller.OnNextItem();
    }
}
