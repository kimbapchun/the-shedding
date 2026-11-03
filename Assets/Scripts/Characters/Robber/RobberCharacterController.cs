using System;
using System.Collections.Generic;
using TheShedding.InventorySystem;
using TheShedding.Items;
using UnityEngine;

namespace TheShedding.Characters
{
    [RequireComponent(typeof(Inventory))]
    public sealed class RobberCharacterController : RobberController, IInventoryOwner
    {
        [Header("Flashlight")]
        [SerializeField] private float flashRange = 5f;
        [SerializeField] private float flashStunDuration = 2f;
        [SerializeField] private LayerMask familyLayer;

        [Header("Inventory")]
        [SerializeField] private ItemDatabase itemDatabase;

        public bool IsFlashlightOn { get; private set; }
        public Inventory Inventory { get; private set; }

        private static readonly Collider[] FlashlightBuffer = new Collider[16];

        protected override void Awake()
        {
            moveSpeed = 5f;
            BodyScale = 2;
            maxLifeSegments = 3;
            Inventory = GetComponent<Inventory>();
            base.Awake();
        }

        protected override void Update()
        {
            base.Update();

            if (!NetworkSimulation) EvaluateFlashlight();
        }

        private readonly HashSet<FamilyController> flashlightTargets = new();

        public void SynchronizeFlashlight(bool enabled) => IsFlashlightOn = enabled;

        // 서버 어댑터에서 호출하므로 컴포넌트 enabled 값과 무관하게 판정한다.
        public void EvaluateFlashlight()
        {
            if (!CanAct() || !IsFlashlightOn) return;
            flashlightTargets.Clear();
            int count = Physics.OverlapSphereNonAlloc(
                transform.position, flashRange, FlashlightBuffer, familyLayer);
            for (int i = 0; i < count; i++)
            {
                var family = FlashlightBuffer[i].GetComponentInParent<FamilyController>();
                if (family != null && family.IsAlive() && flashlightTargets.Add(family))
                    family.ApplyFlashlightStun(flashStunDuration);
            }
        }

        public override void OnSkillInput()
        {
            if (!CanAct()) return;
            IsFlashlightOn = !IsFlashlightOn;
        }

        public void UseHealItem()
        {
            if (!CanAct() || Inventory == null || itemDatabase == null) return;
            if (!Inventory.TryRemoveFirstOfType<ConsumableItem>(itemDatabase, out var item)) return;
            Recover(item.healAmount);
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
