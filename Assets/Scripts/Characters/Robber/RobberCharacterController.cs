using UnityEngine;
using UnityEngine.InputSystem;

namespace TheShedding.Characters
{
    public sealed class RobberCharacterController : RobberController
    {
        [Header("Flashlight")]
        [SerializeField] private float flashRange = 5f;
        [SerializeField] private float flashStunDuration = 2f;
        [SerializeField] private LayerMask familyLayer;

        public bool isFlashlightOn { get; private set; }

        private InputAction flashlightToggleAction;

        protected override void Awake()
        {
            moveSpeed = 5f;
            bodyScale = 2;
            maxLifeSegments = 3;
            base.Awake();

            flashlightToggleAction = playerInput.actions["Skill"];
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            flashlightToggleAction.performed += OnFlashlightTogglePerformed;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            flashlightToggleAction.performed -= OnFlashlightTogglePerformed;
        }

        protected override void Update()
        {
            base.Update();

            // 플래시라이트가 켜진 동안 범위 내 가족을 지속 스턴
            if (isFlashlightOn)
            {
                Collider[] hits = Physics.OverlapSphere(
                    transform.position, flashRange, familyLayer);

                foreach (var col in hits)
                {
                    if (col.TryGetComponent<FamilyController>(out var family))
                        family.ApplyFlashlightStun(flashStunDuration);
                }
            }
        }

        private void OnFlashlightTogglePerformed(InputAction.CallbackContext ctx)
        {
            isFlashlightOn = !isFlashlightOn;
        }

        protected override void OnDeath()
        {
            // TODO: GameManager.Instance.OnRobberDied(this)
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = isFlashlightOn
                ? new Color(1f, 1f, 0f, 0.6f)
                : new Color(1f, 1f, 0f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, flashRange);
        }
    }
}
