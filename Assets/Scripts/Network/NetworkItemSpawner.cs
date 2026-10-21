using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using TheShedding.Characters;
using TheShedding.Interactables;

namespace TheShedding.Network
{
    /// <summary>
    /// 네트워크용 IItemSpawner. 아이템 생성·제거를 서버가 Spawn/Despawn으로 처리해 모두에게 반영한다.
    /// LocalItemSpawner 대신 씬에 하나 둔다.
    /// </summary>
    public class NetworkItemSpawner : MonoBehaviour, IItemSpawner
    {
        [Tooltip("Spawn(id)에 쓸 아이템 프리팹. id는 프리팹의 ItemPickup에서 읽는다.")]
        [SerializeField] private List<NetworkObject> itemPrefabs = new();

        private readonly Dictionary<int, NetworkObject> prefabById = new();

        private static bool IsServer => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

        private void Awake()
        {
            foreach (var prefab in itemPrefabs)
            {
                if (prefab == null || !prefab.TryGetComponent(out ItemPickup pickup)) continue;
                if (!prefabById.TryAdd(pickup.ItemId, prefab))
                    Debug.LogError($"[NetworkItemSpawner] id 중복: {pickup.ItemId} ({prefab.name})", this);
            }
        }

        private void Start()
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var pickup in root.GetComponentsInChildren<ItemPickup>(true))
                    pickup.Spawner = this;
        }

        public void NotifyPickedUp(ItemPickup pickup, BaseCharacterController collector)
        {
            if (!IsServer || pickup == null) return;

            if (pickup.TryGetComponent(out NetworkObject networkObject) && networkObject.IsSpawned)
            {
                // 씬 배치 오브젝트는 파괴하면 씬 동기화가 꼬인다. 숨기기는 NetworkItemVisibility가 한다.
                networkObject.Despawn(destroy: !networkObject.InScenePlaced);
            }
            else
                Debug.LogError($"[NetworkItemSpawner] '{pickup.name}'에 스폰된 NetworkObject가 없습니다.", pickup);
        }

        public void Spawn(int itemId, Vector3 position)
        {
            if (!IsServer) return;

            if (!prefabById.TryGetValue(itemId, out var prefab))
            {
                Debug.LogError($"[NetworkItemSpawner] id {itemId} 프리팹이 등록되지 않았습니다.", this);
                return;
            }

            var instance = Instantiate(prefab, position, Quaternion.identity);
            instance.GetComponent<ItemPickup>().Spawner = this;
            instance.Spawn(destroyWithScene: true);
        }
    }
}
