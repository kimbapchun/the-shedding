using UnityEngine;
using UnityEngine.InputSystem;

namespace TheShedding
{
    public class ThirdPersonCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;

        [Header("Orbit")]
        [SerializeField] private float distance = 5f;
        [SerializeField] private float sensitivity = 0.3f;
        [SerializeField] private float minPitch = -20f;
        [SerializeField] private float maxPitch = 60f;

        [Header("Position")]
        [SerializeField] private float pivotHeight = 1.5f;
        [SerializeField] private float smoothSpeed = 10f;

        [Header("Collision")]
        [SerializeField] private float collisionRadius = 0.3f;
        [SerializeField] private LayerMask collisionMask = ~0;

        private float yaw;
        private float pitch = 20f;

        private void Start()
        {
            yaw   = transform.eulerAngles.y;
            pitch = transform.eulerAngles.x;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible   = false;
        }

        private void LateUpdate()
        {
            if (target == null) return;

            Vector2 delta = Mouse.current.delta.ReadValue();
            yaw   += delta.x * sensitivity;
            pitch -= delta.y * sensitivity;
            pitch  = Mathf.Clamp(pitch, minPitch, maxPitch);

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 pivot     = target.position + Vector3.up * pivotHeight;
            Vector3 direction = -(rotation * Vector3.forward);
            float   actualDist = GetCollisionDistance(pivot, direction);
            Vector3 desired   = pivot + direction * actualDist;

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
