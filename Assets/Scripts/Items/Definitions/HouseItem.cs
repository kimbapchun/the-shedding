using TheShedding.Characters;
using UnityEngine;

namespace TheShedding.Items
{
    [CreateAssetMenu(menuName = "TheShedding/Items/House")]
    public class HouseItem : ItemData
    {
        public override bool CanBePickedUpBy(BaseCharacterController interactor)
            => interactor is RobberController;
    }
}
