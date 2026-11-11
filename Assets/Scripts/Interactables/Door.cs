using System;
using TheShedding.Characters;
using UnityEngine;

namespace TheShedding.Interactables
{
    /// <summary>
    /// 상호작용으로 열고 닫는 문. Animator의 bool 파라미터를 토글해 애니메이션을 재생한다.
    /// 열림/닫힘 상태는 네트워크 공유 값이므로 어댑터(NetworkBehaviour)가 <see cref="OnToggled"/>를
    /// 구독해 NetworkVariable로 복제하고, 원격 변경 시 <see cref="SetOpen"/>을 다시 호출한다.
    /// </summary>
    [RequireComponent(typeof(Animator), typeof(Collider))]
    public class Door : MonoBehaviour, IInteractable
    {
        [Header("Animator")]
        [Tooltip("AnimatorController에 선언된 열림/닫힘 bool 파라미터 이름.")]
        [SerializeField] private string isOpenParameter = "isOpen";

        public bool IsOpen { get; private set; }

        /// <summary>서버 쪽 Door에서 상태가 바뀔 때 발행. 어댑터가 NetworkVariable에 기록.</summary>
        public event Action<bool> OnToggled;

        private Animator animator;
        private Collider col;
        private int isOpenHash;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            col = GetComponent<Collider>();
            isOpenHash = Animator.StringToHash(isOpenParameter);

            // TryInteract의 OverlapSphere가 Interactable 레이어로만 검색한다.
            int interactableLayer = LayerMask.NameToLayer("Interactable");
            if (interactableLayer < 0)
                Debug.LogWarning($"[Door] 'Interactable' 레이어가 정의되지 않았습니다.", this);
            else if (gameObject.layer != interactableLayer)
                Debug.LogWarning($"[Door] '{name}'가 Interactable 레이어가 아닙니다. 상호작용 감지 안 됨.", this);
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
            animator.SetBool(isOpenHash, open);
            col.isTrigger = open;
            OnToggled?.Invoke(open);
            Debug.Log($"[Door] '{name}' SetOpen({open}) — pos={transform.position} scale={transform.localScale}", this);
        }
    }
}
