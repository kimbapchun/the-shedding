using UnityEngine;
using UnityEngine.InputSystem;
using TheShedding.Characters;

namespace TheShedding
{
    public class ThirdPersonCamera : MonoBehaviour
    {
        [SerializeField] private BaseCharacterController target;

        [Header("Orbit")]
        [SerializeField] private float distance = 5f;
        [SerializeField] private float sensitivity = 0.3f;
        [SerializeField] private float minPitch = -20f;
        [SerializeField] private float maxPitch = 60f;

        [Header("Position")]
        [SerializeField] private float standingPivotHeight = 4f;
        [SerializeField] private float sittingPivotHeight  = 2.5f;
        [SerializeField] private float lyingPivotHeight    = 1.0f;
        [SerializeField] private float smoothSpeed = 10f;
        [SerializeField] private float pivotHeightSpeed = 2f;

        private float currentPivotHeight;

        [Header("Collision")]
        [SerializeField] private float collisionRadius = 0.3f;
        [SerializeField] private LayerMask collisionMask = ~0;

        private float yaw;
        private float pitch;

        private InputAction lookAction;

        public void SetTarget(BaseCharacterController controller, PlayerInput input)
        {
            target = controller;
            lookAction = input.actions["Look"];
            currentPivotHeight = standingPivotHeight;
        }

        public void ApplySettings(CameraSettings settings)
        {
            standingPivotHeight = settings.standingPivotHeight;
            sittingPivotHeight  = settings.sittingPivotHeight;
            lyingPivotHeight    = settings.lyingPivotHeight;
            distance            = settings.distance;
            GetComponent<Camera>().fieldOfView = settings.fov;
            currentPivotHeight  = standingPivotHeight;
        }

        private void Start()
        {
            if (target == null)
                target = FindObjectOfType<BaseCharacterController>();

            if (lookAction == null && target != null)
            {
                var playerInput = target.GetComponent<PlayerInput>();
                if (playerInput != null)
                    lookAction = playerInput.actions["Look"];
            }

            yaw   = transform.eulerAngles.y;
            pitch = Mathf.DeltaAngle(0f, transform.eulerAngles.x);
            currentPivotHeight = standingPivotHeight;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible   = false;
        }

        private void LateUpdate()
        {
            if (target == null || lookAction == null) return;

            Vector2 delta = lookAction.ReadValue<Vector2>();
            yaw   += delta.x * sensitivity;
            pitch -= delta.y * sensitivity;
            pitch  = Mathf.Clamp(pitch, minPitch, maxPitch);

            float targetPivotHeight = target.IsLying   ? lyingPivotHeight
                                    : target.IsSitting ? sittingPivotHeight
                                    : standingPivotHeight;
            currentPivotHeight = Mathf.Lerp(currentPivotHeight, targetPivotHeight, pivotHeightSpeed * Time.deltaTime);

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 pivot     = target.transform.position + Vector3.up * currentPivotHeight;
            Vector3 direction  = -(rotation * Vector3.forward);
            float   actualDist = GetCollisionDistance(pivot, direction);
            Vector3 desired    = pivot + direction * actualDist;

            transform.position = Vector3.Lerp(transform.position, desired, smoothSpeed * Time.deltaTime);
            transform.LookAt(pivot);
        }

        private float GetCollisionDistance(Vector3 pivot, Vector3 direction)
        {
            if (Physics.SphereCast(pivot, collisionRadius, direction, out RaycastHit hit, distance, collisionMask))
                return Mathf.Max(hit.distance - collisionRadius, 0f);

            return distance;
        }
    }
}
