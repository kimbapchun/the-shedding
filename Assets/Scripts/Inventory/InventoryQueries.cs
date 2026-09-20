using TheShedding.Items;

namespace TheShedding.InventorySystem
{
    /// <summary>
    /// 타입 기반 조회처럼 Inventory + ItemDatabase가 함께 있어야 성립하는 연산을 모은다.
    /// 목록 조작은 반드시 Inventory.AddItem/RemoveItem을 통해서만 수행한다.
    /// </summary>
    public static class InventoryQueries
    {
        public static bool HasOfType<T>(this Inventory inventory, ItemDatabase database) where T : ItemData
        {
            foreach (int id in inventory.ItemIds)
                if (database.Get(id) is T) return true;
            return false;
        }

        public static bool TryRemoveFirstOfType<T>(this Inventory inventory, ItemDatabase database, out T item)
            where T : ItemData
        {
            foreach (int id in inventory.ItemIds)
            {
                if (database.Get(id) is not T match) continue;
                inventory.RemoveItem(id);
                item = match;
                return true;
            }
            item = null;
            return false;
        }
    }
}
