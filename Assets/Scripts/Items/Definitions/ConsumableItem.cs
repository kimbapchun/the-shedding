using TheShedding.Characters;
using UnityEngine;

namespace TheShedding.Items
{
    [CreateAssetMenu(menuName = "TheShedding/Items/Consumable")]
    public class ConsumableItem : ItemData
    {
        public int healAmount = 1;

        public override bool CanBePickedUpBy(BaseCharacterController interactor)
            => interactor is RobberController;
    }
}
