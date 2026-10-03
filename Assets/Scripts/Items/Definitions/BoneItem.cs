using TheShedding.Characters;
using UnityEngine;

namespace TheShedding.Items
{
    [CreateAssetMenu(menuName = "TheShedding/Items/Bone")]
    public class BoneItem : ItemData
    {
        public override bool CanBePickedUpBy(BaseCharacterController interactor)
            => interactor is BabyController;
    }
}
