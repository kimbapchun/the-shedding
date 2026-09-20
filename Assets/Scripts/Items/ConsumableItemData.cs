using UnityEngine;

namespace TheShedding.Items
{
    [CreateAssetMenu(menuName = "TheShedding/Items/Consumable")]
    public class ConsumableItemData : ItemData
    {
        public int healAmount = 1;
    }
}
