using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using TheShedding.InventorySystem;

namespace TheShedding.Network
{
    /// <summary>
    /// 서버의 Inventory를 NetworkList로 복제해 클라이언트 Inventory에 통째로 옮겨 적는다.
    /// 선택 슬롯은 동기화하지 않는다(각자 로컬).
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

        private void HandleListChanged(NetworkListEvent<int> e) => Pull();

        private void Pull()
        {
            buffer.Clear();
            foreach (int id in itemIds) buffer.Add(id);
            inventory.SetItems(buffer);
        }
    }
}
