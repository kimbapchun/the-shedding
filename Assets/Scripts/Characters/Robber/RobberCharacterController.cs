using UnityEngine;

namespace TheShedding.Characters
{
    public sealed class RobberCharacterController : RobberController
    {
        [Header("Flashlight")]
        [SerializeField] private float flashRange = 5f;
        [SerializeField] private float flashStunDuration = 2f;
        [SerializeField] private LayerMask familyLayer;

        public bool IsFlashlightOn { get; private set; }

        protected override void Awake()
        {
            moveSpeed = 5f;
            BodyScale = 2;
            maxLifeSegments = 3;
            base.Awake();
        }

        protected override void Update()
        {
            base.Update();

            // 플래시라이트가 켜진 동안 범위 내 가족을 지속 스턴
            if (IsFlashlightOn)
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

        // ── 스킬 (PlayerInputReader → OnSkillInput): 플래시라이트 토글 ────

        public override void OnSkillInput()
        {
            IsFlashlightOn = !IsFlashlightOn;
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            Gizmos.color = IsFlashlightOn
                ? new Color(1f, 1f, 0f, 0.6f)
                : new Color(1f, 1f, 0f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, flashRange);
        }
    }
}
