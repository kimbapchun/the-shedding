using System;
using TheShedding.Characters;
using UnityEngine;

namespace TheShedding.Interactables
{
    /// <summary>
    /// 한쪽으로 미끄러져 열리는 미닫이문. localPosition에 <see cref="openOffset"/>을 더한 위치를
    /// 열림 상태로 삼고, <see cref="slideDuration"/> 동안 보간한다. 열림/닫힘은 네트워크 공유 값이므로
    /// 어댑터(NetworkBehaviour)가 <see cref="OnToggled"/>를 구독해 NetworkVariable로 복제하고,
    /// 원격 변경 시 <see cref="SetOpen"/>을 다시 호출한다. 좌표 자체는 각 클라이언트가 로컬 보간.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class SlidingDoor : MonoBehaviour, IInteractable
    {
        [Header("Slide")]
        [Tooltip("닫힘 상태의 localPosition에 더해지는 오프셋. 열림 상태가 된다.")]
        [SerializeField] private Vector3 openOffset = new(1f, 0f, 0f);
        [Tooltip("닫힘 ↔ 열림 전환에 걸리는 시간(초).")]
        [SerializeField] private float slideDuration = 0.4f;

        public bool IsOpen { get; private set; }

        /// <summary>서버 쪽 SlidingDoor에서 상태가 바뀔 때 발행. 어댑터가 NetworkVariable에 기록.</summary>
        public event Action<bool> OnToggled;

        private Collider col;
        private Vector3 closedLocalPosition;

        private void Awake()
        {
            col = GetComponent<Collider>();
            closedLocalPosition = transform.localPosition;

            int interactableLayer = LayerMask.NameToLayer("Interactable");
            if (interactableLayer < 0)
                Debug.LogWarning($"[SlidingDoor] 'Interactable' 레이어가 정의되지 않았습니다.", this);
            else if (gameObject.layer != interactableLayer)
                Debug.LogWarning($"[SlidingDoor] '{name}'가 Interactable 레이어가 아닙니다. 상호작용 감지 안 됨.", this);
        }

        private void Update()
        {
            Vector3 target = IsOpen ? closedLocalPosition + openOffset : closedLocalPosition;
            if (transform.localPosition == target) return;
            float step = openOffset.magnitude / Mathf.Max(0.0001f, slideDuration) * Time.deltaTime;
            transform.localPosition = Vector3.MoveTowards(transform.localPosition, target, step);
        }

        // ── IInteractable ─────────────────────────────────────────────────

        public bool CanInteract(BaseCharacterController interactor) => true;

        public void Interact(BaseCharacterController interactor)
        {
            SetOpen(!IsOpen);
        }

        public string GetInteractPrompt(BaseCharacterController interactor)
            => IsOpen ? "문 닫기" : "문 열기";

        // ── 상태 적용 ─────────────────────────────────────────────────────

        /// <summary>
        /// 열림/닫힘을 적용한다. 어댑터의 NetworkVariable 콜백에서도 이 메서드를 호출해 클라에 반영한다.
        /// 콜라이더는 열림 중에는 isTrigger=true로 바뀌어 통과 가능하지만, 닫기 상호작용은 여전히 감지된다.
        /// </summary>
        public void SetOpen(bool open)
        {
            if (IsOpen == open) return;
            IsOpen = open;
            col.isTrigger = open;
            OnToggled?.Invoke(open);
        }
    }
}
