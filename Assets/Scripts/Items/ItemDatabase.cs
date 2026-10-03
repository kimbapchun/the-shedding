using System.Collections.Generic;
using UnityEngine;

namespace TheShedding.Items
{
    /// <summary>
    /// id → ItemData 조회를 담당한다.
    ///
    /// Inventory는 네트워크 동기화를 위해 int id만 들고 있으므로,
    /// 이름·아이콘·효과 같은 실제 데이터가 필요할 때는 이 데이터베이스에서 꺼낸다.
    /// </summary>
    [CreateAssetMenu(menuName = "TheShedding/ItemDatabase")]
    public class ItemDatabase : ScriptableObject
    {
        [SerializeField] private List<ItemData> items = new();

        private Dictionary<int, ItemData> byId;

        private void OnEnable()
        {
            Rebuild();
        }

        // 에디터에서 리스트를 편집한 뒤에도 즉시 반영되도록.
        private void OnValidate()
        {
            Rebuild();
        }

        private void Rebuild()
        {
            byId = new Dictionary<int, ItemData>(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null) continue;

                if (byId.ContainsKey(item.id))
                {
                    Debug.LogError($"[ItemDatabase] id 중복: {item.id} ({byId[item.id].name} vs {item.name})", this);
                    continue;
                }
                byId[item.id] = item;
            }
        }

        public bool TryGet(int id, out ItemData item)
        {
            if (byId == null) Rebuild();
            return byId.TryGetValue(id, out item);
        }

        public ItemData Get(int id)
        {
            TryGet(id, out var item);
            return item;
        }
    }
}
