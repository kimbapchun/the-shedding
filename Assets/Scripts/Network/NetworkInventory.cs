using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using TheShedding.InventorySystem;

namespace TheShedding.Network
{
    /// <summary>
    /// 서버의 Inventory를 NetworkList로 복제하고 추가·삭제 변경분만 클라이언트에 반영한다.
    /// 선택 슬롯은 각자 로컬로 유지한다. 선택한 항목이 삭제되면 Inventory의 선택 규칙을 따른다.
    /// </summary>
    [RequireComponent(typeof(Inventory))]
    public class NetworkInventory : NetworkBehaviour
    {
        private readonly NetworkList<int> itemIds = new();
        private Inventory inventory;
        private readonly List<int> buffer = new();

        private void Awake()
        {
            inventory = GetComponent<Inventory>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                itemIds.Clear();
                foreach (int id in inventory.ItemIds) itemIds.Add(id);
                inventory.OnItemAdded += HandleServerAdded;
                inventory.OnItemRemoved += HandleServerRemoved;
                return;
            }

            Pull();
            itemIds.OnListChanged += HandleListChanged;
        }

        public override void OnNetworkDespawn()
        {
            inventory.OnItemAdded -= HandleServerAdded;
            inventory.OnItemRemoved -= HandleServerRemoved;
            itemIds.OnListChanged -= HandleListChanged;
        }

        private void HandleServerAdded(int id, int index) => itemIds.Add(id);
        private void HandleServerRemoved(int id, int index) => itemIds.RemoveAt(index);

        private void HandleListChanged(NetworkListEvent<int> e)
        {
            switch (e.Type)
            {
                case NetworkListEvent<int>.EventType.Add:
                    inventory.AddItem(e.Value);
                    break;
                case NetworkListEvent<int>.EventType.Remove:
                case NetworkListEvent<int>.EventType.RemoveAt:
                    inventory.RemoveAt(e.Index);
                    break;
                case NetworkListEvent<int>.EventType.Clear:
                    for (int i = inventory.Count - 1; i >= 0; i--) inventory.RemoveAt(i);
                    break;
                default:
                    // 초기 전체 동기화 및 현재 서버에서 사용하지 않는 Insert/Value 변경.
                    Pull();
                    break;
            }
        }

        private void Pull()
        {
            buffer.Clear();
            foreach (int id in itemIds) buffer.Add(id);
            inventory.SetItems(buffer);
        }
    }
}
