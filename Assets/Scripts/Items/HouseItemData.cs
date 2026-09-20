using TheShedding.Characters;
using UnityEngine;

namespace TheShedding.Items
{
    [CreateAssetMenu(menuName = "TheShedding/Items/HouseItem")]
    public class HouseItemData : ItemData
    {
        public override bool CanBePickedUpBy(BaseCharacterController interactor)
            => interactor is RobberController;
    }
}
