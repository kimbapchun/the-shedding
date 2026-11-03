using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TheShedding.Characters;
using TheShedding.InventorySystem;
using TheShedding.Network;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TheShedding.Tests
{
    public class NetworkGameplayIntegrationTests
    {
        private readonly List<NetworkManager> managers = new();
        private NetworkManager host, client;
        private GameObject itemSpawnerObject;

        private ushort port;

        // 고정 포트는 이전 테스트나 다른 프로세스의 소켓과 충돌하므로 비어 있는 포트를 받는다.
        [SetUp]
        public void AssignPort()
        {
            using var socket = new System.Net.Sockets.UdpClient(0);
            port = (ushort)((System.Net.IPEndPoint)socket.Client.LocalEndPoint).Port;
        }

        [UnityTest]
        public IEnumerator Remote_flashlight_stuns_family_and_replicates_to_late_join()
        {
            yield return Connect();
            var robberObject = Spawn("Test_Robber", client.LocalClientId, Vector3.zero);
            var motherObject = Spawn("Test_Mom", host.LocalClientId, new Vector3(0.6f, 0f, 0f));
            yield return Until(() => client.SpawnManager.SpawnedObjects.Count >= 2, "character replication");
            Stabilize();
            var robber = robberObject.GetComponent<RobberCharacterController>();
            var mother = motherObject.GetComponent<MotherController>();
            var remoteRobber = Replica(client, robberObject).GetComponent<RobberCharacterController>();
            var remoteMother = Replica(client, motherObject).GetComponent<MotherController>();
            var robberCommands = remoteRobber.GetComponent<RobberNetworkAdapter>();

            Assert.IsFalse(robber.enabled, "remote owner's controller must stay disabled on server");
            robberCommands.Skill();
            yield return Until(() => robber.IsFlashlightOn && remoteRobber.IsFlashlightOn
                && !mother.IsActionAllowed, "remote flashlight and server stun");
            yield return Until(() => !remoteMother.IsActionAllowed, "replicated family stun");

            var late = CreateManager("late observer");
            Assert.IsTrue(late.StartClient());
            yield return Until(() => late.IsConnectedClient
                && late.SpawnManager.SpawnedObjects.ContainsKey(robberObject.NetworkObjectId), "late join");
            Stabilize();
            Assert.IsTrue(Replica(late, robberObject).GetComponent<RobberCharacterController>().IsFlashlightOn);
            robberCommands.Skill();
            yield return Until(() => !robber.IsFlashlightOn && !remoteRobber.IsFlashlightOn, "flashlight off");
            yield return Until(() => mother.IsActionAllowed && remoteMother.IsActionAllowed, "stun expiration");
        }

        [UnityTest]
        public IEnumerator Remote_attack_and_heal_replicate_life()
        {
            yield return Connect();
            var robberObject = Spawn("Test_Robber", client.LocalClientId, Vector3.zero);
            var fatherObject = Spawn("Dad", client.LocalClientId, new Vector3(1f, 0f, 0f));
            yield return Until(() => client.SpawnManager.SpawnedObjects.Count >= 2, "character replication");
            Stabilize();
            var robber = robberObject.GetComponent<RobberCharacterController>();
            var remoteRobber = Replica(client, robberObject).GetComponent<RobberCharacterController>();
            var robberCommands = remoteRobber.GetComponent<RobberNetworkAdapter>();

            Replica(client, fatherObject).GetComponent<CharacterInputNetworkAdapter>().Attack();
            Replica(client, fatherObject).GetComponent<CharacterInputNetworkAdapter>().Attack(); // 쿨타임 재요청
            yield return Until(() => robber.CurrentLifeSegments == 2, "remote attack");
            robber.Inventory.AddItem(2); // Consumable
            yield return Until(() => remoteRobber.CurrentLifeSegments == 2 && remoteRobber.Inventory.Contains(2), "damage and inventory");
            robberCommands.UseHealItem();
            robberCommands.UseHealItem(); // 이미 소모된 아이템은 재사용 불가
            yield return Until(() => robber.CurrentLifeSegments == 3 && remoteRobber.CurrentLifeSegments == 3
                && !remoteRobber.Inventory.Contains(2), "remote heal");
        }

        [UnityTest]
        public IEnumerator Remote_baby_steals_and_drops_item()
        {
            yield return Connect();
            var robberObject = Spawn("Test_Robber", client.LocalClientId, Vector3.zero);
            var babyObject = Spawn("Test_Baby", client.LocalClientId, new Vector3(-0.4f, 0f, 0f));
            yield return Until(() => client.SpawnManager.SpawnedObjects.Count >= 2, "character replication");
            Stabilize();
            var robber = robberObject.GetComponent<RobberCharacterController>();
            var baby = babyObject.GetComponent<BabyController>();
            var remoteRobber = Replica(client, robberObject).GetComponent<RobberCharacterController>();
            var remoteBaby = Replica(client, babyObject).GetComponent<BabyController>();

            robber.Inventory.AddItem(3); // House
            yield return Until(() => remoteRobber.Inventory.Contains(3), "house inventory");
            remoteBaby.GetComponent<BabyNetworkAdapter>().Skill();
            yield return Until(() => baby.Inventory.Contains(3) && remoteBaby.Inventory.Contains(3)
                && !remoteRobber.Inventory.Contains(3), "remote baby steal");

            itemSpawnerObject = new GameObject("item spawner");
            itemSpawnerObject.SetActive(false);
            var spawner = itemSpawnerObject.AddComponent<NetworkItemSpawner>();
            var housePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Items/Test_House_Item.prefab");
            typeof(NetworkItemSpawner).GetField("itemPrefabs", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(spawner, new List<NetworkObject> { housePrefab.GetComponent<NetworkObject>() });
            itemSpawnerObject.SetActive(true);
            remoteBaby.GetComponent<BabyNetworkAdapter>().DropItem();
            yield return Until(() => !baby.Inventory.Contains(3) && !remoteBaby.Inventory.Contains(3), "remote drop");
            Assert.IsTrue(host.SpawnManager.SpawnedObjects.Values.Any(n => n.GetComponent<TheShedding.Interactables.ItemPickup>() != null));
        }

        [UnityTest]
        public IEnumerator Host_trap_placement_consumes_item_and_blocks_cooldown_replay()
        {
            yield return Connect();
            var motherObject = Spawn("Test_Mom", host.LocalClientId, new Vector3(0.6f, 0f, 0f));
            yield return Until(() => client.SpawnManager.SpawnedObjects.ContainsKey(motherObject.NetworkObjectId), "character replication");
            Stabilize();
            var mother = motherObject.GetComponent<MotherController>();
            var remoteMother = Replica(client, motherObject).GetComponent<MotherController>();

            mother.Inventory.AddItem(5);
            mother.Inventory.AddItem(5);
            yield return Until(() => remoteMother.Inventory.Count == 2, "trap inventory");
            mother.GetComponent<MotherNetworkAdapter>().Skill();
            yield return Until(() => remoteMother.Inventory.Count == 1, "trap placement");
            Assert.AreEqual(1, mother.Inventory.Count);
            Assert.IsFalse(mother.TryPlaceTrap(5), "server rejects cooldown replay");
            Assert.IsFalse(remoteMother.TryPlaceTrap(5), "replicated cooldown blocks preview/controller");
            Assert.IsTrue(client.SpawnManager.SpawnedObjects.Values.Any(n => n.name.StartsWith("Placed_Trap")));
        }

        [UnityTest]
        public IEnumerator Knockdown_rejects_commands_and_death_replicates()
        {
            yield return Connect();
            var robberObject = Spawn("Test_Robber", client.LocalClientId, Vector3.zero);
            yield return Until(() => client.SpawnManager.SpawnedObjects.ContainsKey(robberObject.NetworkObjectId), "character replication");
            Stabilize();
            var robber = robberObject.GetComponent<RobberCharacterController>();
            var remoteRobber = Replica(client, robberObject).GetComponent<RobberCharacterController>();

            robber.TakeStatusEffect(StatusEffect.KnockedDown, 10f);
            yield return Until(() => remoteRobber.CurrentStatusEffect == StatusEffect.KnockedDown, "knockdown replication");
            remoteRobber.GetComponent<RobberNetworkAdapter>().Skill();
            yield return new WaitForSeconds(0.2f);
            Assert.IsFalse(robber.IsFlashlightOn, "incapacitated toggle rejected");
            robber.TakeDamage(3);
            yield return Until(() => remoteRobber.CurrentLifeSegments == 0, "death replication");
        }

        [UnityTest]
        public IEnumerator Host_flashlight_stuns_remote_mother_and_trap_replicates_to_late_join()
        {
            yield return Connect();
            var hostRobberObject = Spawn("Test_Robber", host.LocalClientId, Vector3.zero);
            var remoteMotherObject = Spawn("Test_Mom", client.LocalClientId, new Vector3(0.6f, 0f, 0f));
            yield return Until(() => client.SpawnManager.SpawnedObjects.ContainsKey(remoteMotherObject.NetworkObjectId), "remote mother spawn");
            Stabilize();
            var hostRobber = hostRobberObject.GetComponent<RobberCharacterController>();
            var serverMother = remoteMotherObject.GetComponent<MotherController>();
            var ownerMother = Replica(client, remoteMotherObject).GetComponent<MotherController>();
            hostRobber.GetComponent<RobberNetworkAdapter>().Skill();
            yield return Until(() => !serverMother.IsActionAllowed && !ownerMother.IsActionAllowed, "host flashlight stun");
            hostRobber.GetComponent<RobberNetworkAdapter>().Skill();
            yield return Until(() => ownerMother.IsActionAllowed && serverMother.IsActionAllowed, "host flashlight stun expiration");
            serverMother.Inventory.AddItem(5);
            serverMother.Inventory.AddItem(5);
            yield return Until(() => ownerMother.Inventory.Count == 2, "remote mother inventory");
            ownerMother.GetComponent<MotherNetworkAdapter>().Skill();
            yield return Until(() => ownerMother.Inventory.Count == 1, "remote trap placement");
            var previewField = typeof(MotherController).GetField("trapPreviewInstance", BindingFlags.Instance | BindingFlags.NonPublic);
            var preview = (GameObject)previewField.GetValue(ownerMother);
            Assert.IsNotNull(preview);
            Assert.IsFalse(preview.activeSelf, "preview hides during replicated cooldown");
            yield return Until(() => preview.activeSelf, "preview reappears after cooldown");

            var afterTrap = CreateManager("late trap observer");
            Assert.IsTrue(afterTrap.StartClient());
            yield return Until(() => afterTrap.IsConnectedClient
                && afterTrap.SpawnManager.SpawnedObjects.Values.Count(n => n.name.StartsWith("Placed_Trap")) == 1,
                "late join receives placed traps");
            Stabilize();
        }

        [UnityTest]
        public IEnumerator Owner_camera_binds_when_camera_appears_after_spawn()
        {
            host = CreateManager("camera-test-host");
            Assert.IsTrue(host.StartHost());
            var father = Spawn("Dad", host.LocalClientId, new Vector3(20f, 1f, 20f));
            yield return null;
            var camera = new GameObject("late camera", typeof(Camera), typeof(ThirdPersonCamera))
                .GetComponent<ThirdPersonCamera>();
            yield return Until(() => camera.Target == father.GetComponent<FatherController>(), "late camera binding");
            var input = father.GetComponent<UnityEngine.InputSystem.PlayerInput>();
            Assert.IsTrue(input.currentActionMap.enabled);
            Assert.AreEqual("Player", input.currentActionMap.name);
            Assert.Less(Vector3.Distance(camera.transform.position, father.transform.position), 8f);
        }

        private IEnumerator Connect()
        {
            yield return null;
            new GameObject("test camera", typeof(Camera), typeof(ThirdPersonCamera));
            host = CreateManager("host");
            Assert.IsTrue(host.StartHost());
            client = CreateManager("remote");
            Assert.IsTrue(client.StartClient());
            yield return Until(() => client.IsConnectedClient, "remote connection");
        }

        private NetworkManager CreateManager(string name)
        {
            var go = new GameObject(name);
            var transport = go.AddComponent<UnityTransport>();
            transport.SetConnectionData("127.0.0.1", port);
            var manager = go.AddComponent<NetworkManager>();
            manager.NetworkConfig = new NetworkConfig { NetworkTransport = transport, EnableSceneManagement = false };
            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>("Assets/DefaultNetworkPrefabs.asset");
            manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(list);
            managers.Add(manager);
            return manager;
        }

        private NetworkObject Spawn(string name, ulong owner, Vector3 position)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Characters/{name}.prefab");
            var instance = Object.Instantiate(prefab, position, Quaternion.identity).GetComponent<NetworkObject>();
            instance.SpawnWithOwnership(owner);
            return instance;
        }

        private static NetworkObject Replica(NetworkManager manager, NetworkObject source)
            => manager.SpawnManager.SpawnedObjects[source.NetworkObjectId];

        // 동 프로세스 테스트의 복제본이 서버의 Physics 판정에 중복 포함되지 않도록 격리한다.
        private void Stabilize()
        {
            foreach (var manager in managers)
                foreach (var obj in manager.SpawnManager.SpawnedObjects.Values)
                {
                    if (obj.TryGetComponent<Rigidbody>(out var body))
                    {
                        body.useGravity = false;
                        body.constraints = RigidbodyConstraints.FreezeAll;
                    }
                    if (obj.TryGetComponent<PlayerInputReader>(out var input)) input.enabled = false;
                    if (manager != host)
                        foreach (var collider in obj.GetComponentsInChildren<Collider>()) collider.enabled = false;
                }
            Physics.SyncTransforms();
        }

        private static IEnumerator Until(Func<bool> condition, string description)
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(condition(), description);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            foreach (var manager in managers)
                if (manager != null) manager.Shutdown();
            // 종료 중에 파괴하면 NetworkManager.OnDestroy가 해제된 드라이버를 건드려 다음 테스트에서 예외가 난다.
            float deadline = Time.realtimeSinceStartup + 5f;
            while (managers.Any(m => m != null && (m.ShutdownInProgress || m.IsListening))
                && Time.realtimeSinceStartup < deadline)
                yield return null;
            if (itemSpawnerObject != null) Object.Destroy(itemSpawnerObject);
            foreach (var manager in managers)
                if (manager != null) Object.Destroy(manager.gameObject);
            managers.Clear();
            foreach (var camera in Object.FindObjectsByType<ThirdPersonCamera>(FindObjectsSortMode.None))
                Object.Destroy(camera.gameObject);
            yield return null;
        }
    }
}
