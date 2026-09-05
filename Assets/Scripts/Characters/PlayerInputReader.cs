using UnityEngine;
using UnityEngine.InputSystem;

namespace TheShedding.Characters
{
    [RequireComponent(typeof(BaseCharacterController))]
    [RequireComponent(typeof(PlayerInput))]
    public class PlayerInputReader : MonoBehaviour
    {
        private BaseCharacterController controller;
        private PlayerInput playerInput;

        private Vector2 moveInput;
        private bool sprintInput;

        private InputAction moveAction;
        private InputAction sprintAction;
        private InputAction attackAction;
        private InputAction skillAction;
        private InputAction interactAction;
        private InputAction sitAction;
        private InputAction lieAction;
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
            previousAction = playerInput.actions["Previous"];
            nextAction     = playerInput.actions["Next"];
        }

        private void OnEnable()
        {
            attackAction.performed   += OnAttackPerformed;
            skillAction.performed    += OnSkillPerformed;
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
        }

        private void FixedUpdate()
        {
            controller.Move(moveInput, sprintInput);
        }

        private void OnAttackPerformed(InputAction.CallbackContext ctx)   => controller.OnAttackInput();
        private void OnSkillPerformed(InputAction.CallbackContext ctx)    => controller.OnSkillInput();
        private void OnInteractPerformed(InputAction.CallbackContext ctx) => controller.TryInteract();
        private void OnSitPerformed(InputAction.CallbackContext ctx)      => controller.SetSittingState(!controller.IsSitting);
        private void OnLiePerformed(InputAction.CallbackContext ctx)      => controller.SetLyingState(!controller.IsLying);
        private void OnPreviousPerformed(InputAction.CallbackContext ctx) => controller.OnPreviousItem();
        private void OnNextPerformed(InputAction.CallbackContext ctx)     => controller.OnNextItem();
    }
}
