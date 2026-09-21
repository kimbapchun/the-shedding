using TheShedding.Characters;
using UnityEngine;

namespace TheShedding.Items
{
    [CreateAssetMenu(menuName = "TheShedding/Items/Trap")]
    public class TrapItemData : ItemData
    {
        public TrapType   trapType;
        public GameObject placementPrefab;
        public GameObject previewPrefab;

        public override bool CanBePickedUpBy(BaseCharacterController interactor)
            => interactor is MotherController;
    }
}
