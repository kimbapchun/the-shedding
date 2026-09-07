using UnityEngine;

namespace TheShedding
{
    public class ThirdPersonCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 3f, -5f);
        [SerializeField] private float lookAtHeight = 1.5f;
        [SerializeField] private float smoothSpeed = 10f;

        private void LateUpdate()
        {
            if (target == null) return;

            Vector3 desired = target.position + offset;
            transform.position = Vector3.Lerp(transform.position, desired, smoothSpeed * Time.deltaTime);
            transform.LookAt(target.position + Vector3.up * lookAtHeight);
        }
    }
}
