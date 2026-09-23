using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheShedding.Network
{
    /// <summary>
    /// 서버가 플레이어 캐릭터를 직접 스폰한다. NetworkManager와 같은 GameObject에 붙인다.
    ///
    /// NetworkManager의 Player Prefab과 ConnectionApproval의 CreatePlayerObject를
    /// 쓰지 않는 이유: 그 경로는 접속이 승인되는 순간, 즉 아직 로비에 있을 때 스폰한다.
    /// 캐릭터는 게임 씬의 스폰 지점 위에 나타나야 하므로 씬 로딩이 끝난 뒤로 미뤄야 한다.
    ///
    /// 역할(아빠/엄마/아기/개/강도) 배정은 아직 없다. 지금은 전원 같은 프리팹으로 스폰하고,
    /// 스폰 파이프라인이 도는 것을 확인한 뒤 역할을 얹는다.
    /// </summary>
    [RequireComponent(typeof(NetworkManager))]
    public class PlayerSpawner : MonoBehaviour
    {
        [Tooltip("스폰할 캐릭터 프리팹. NetworkObject가 있어야 하고 DefaultNetworkPrefabs에 등록되어 있어야 한다.")]
        [SerializeField] private GameObject characterPrefab;

        private NetworkManager m_NetworkManager;
        private NetworkSceneLoader m_SceneLoader;

        /// <summary>
        /// 스폰을 마친 클라이언트 → 배정된 스폰 지점 번호.
        /// 중복 스폰을 막는 근거이자, 나간 사람의 자리를 다음 사람에게 물려주는 근거다.
        /// </summary>
        private readonly Dictionary<ulong, int> m_SpawnIndexByClientId = new Dictionary<ulong, int>();

        private void Awake()
        {
            m_NetworkManager = GetComponent<NetworkManager>();

            // 싱글톤 대신 같은 오브젝트에서 가져온다. Awake 실행 순서와 무관하게 이미 존재한다.
            m_SceneLoader = GetComponent<NetworkSceneLoader>();
        }

        private void OnEnable()
        {
            if (m_SceneLoader != null)
            {
                m_SceneLoader.OnSceneLoadCompleted += HandleSceneLoadCompleted;
            }

            m_NetworkManager.OnClientConnectedCallback += HandleClientConnected;
            m_NetworkManager.OnClientDisconnectCallback += HandleClientDisconnect;
            m_NetworkManager.OnServerStopped += HandleServerStopped;
        }

        private void OnDisable()
        {
            if (m_SceneLoader != null)
            {
                m_SceneLoader.OnSceneLoadCompleted -= HandleSceneLoadCompleted;
            }

            if (m_NetworkManager == null)
            {
                return;
            }

            m_NetworkManager.OnClientConnectedCallback -= HandleClientConnected;
            m_NetworkManager.OnClientDisconnectCallback -= HandleClientDisconnect;
            m_NetworkManager.OnServerStopped -= HandleServerStopped;
        }

        private void Start()
        {
            // 셋 다 비어 있어도 컴파일은 되고 방도 열린다. 스폰 시점에야 조용히 실패하므로
            // 설정 실수는 시작할 때 드러내는 편이 낫다.
            if (m_SceneLoader == null)
            {
                Debug.LogError("[PlayerSpawner] 같은 GameObject에 NetworkSceneLoader가 없습니다. " +
                               "씬 로딩 완료를 알 수 없어 스폰이 일어나지 않습니다.");
            }

            if (characterPrefab == null)
            {
                Debug.LogError("[PlayerSpawner] Character Prefab이 비어 있습니다. Inspector에서 지정하세요.");
                return;
            }

            if (!characterPrefab.TryGetComponent<NetworkObject>(out _))
            {
                Debug.LogError($"[PlayerSpawner] '{characterPrefab.name}'에 NetworkObject가 없습니다. " +
                               "네트워크 스폰 대상이 될 수 없습니다.");
            }
        }

        // ── 이벤트 ───────────────────────────────────────────────────────

        /// <summary>
        /// 모두가 씬 로딩을 마친 시점. 캐릭터를 올릴 바닥이 생겼다는 뜻이다.
        ///
        /// 서버와 클라이언트 양쪽에서 불리므로 서버만 통과시킨다. 클라이언트가
        /// 스폰을 시도하면 NGO가 거부하고 에러만 남는다.
        /// </summary>
        private void HandleSceneLoadCompleted(string sceneName)
        {
            if (!m_NetworkManager.IsServer)
            {
                return;
            }

            if (sceneName != m_SceneLoader.GameSceneName)
            {
                // 로비로 돌아온 경우. 캐릭터는 씬과 함께 파괴되었으므로 명단만 비운다.
                // 비우지 않으면 다음 판에서 "이미 스폰됨"으로 걸러져 아무도 나타나지 않는다.
                m_SpawnIndexByClientId.Clear();
                return;
            }

            foreach (var clientId in m_NetworkManager.ConnectedClientsIds)
            {
                SpawnFor(clientId);
            }
        }

        /// <summary>
        /// 게임이 시작된 뒤 들어온 사람. NGO가 현재 씬으로 맞춰준 뒤 이 콜백이 온다.
        ///
        /// 로비에 있는 동안의 접속은 여기서 걸러진다. 그때는 씬 로딩 완료 쪽이 맡는다.
        /// </summary>
        private void HandleClientConnected(ulong clientId)
        {
            if (!m_NetworkManager.IsServer || m_SceneLoader == null)
            {
                return;
            }

            if (SceneManager.GetActiveScene().name != m_SceneLoader.GameSceneName)
            {
                return;
            }

            SpawnFor(clientId);
        }

        /// <summary>
        /// 나간 사람의 자리를 비운다. 캐릭터 오브젝트 자체는 NGO가 소유자와 함께 정리한다.
        /// (NetworkObject의 DontDestroyWithOwner가 꺼져 있을 때의 기본 동작)
        /// </summary>
        private void HandleClientDisconnect(ulong clientId)
        {
            m_SpawnIndexByClientId.Remove(clientId);
        }

        /// <summary>
        /// 방을 닫을 때 명단을 비운다. 한 번 실행한 앱에서 호스트를 여러 번 열 수 있는데,
        /// 남겨두면 다음 방에서 같은 clientId가 "이미 스폰됨"으로 걸러진다.
        /// </summary>
        private void HandleServerStopped(bool wasHost)
        {
            m_SpawnIndexByClientId.Clear();
        }

        // ── 스폰 ─────────────────────────────────────────────────────────

        private void SpawnFor(ulong clientId)
        {
            if (characterPrefab == null)
            {
                return;
            }

            // 씬 로딩 완료와 접속 콜백이 같은 클라이언트에 대해 둘 다 올 수 있다.
            if (m_SpawnIndexByClientId.ContainsKey(clientId))
            {
                return;
            }

            var spawnIndex = TakeFreeSpawnIndex();

            ResolvePose(spawnIndex, out var position, out var rotation);

            var instance = Instantiate(characterPrefab, position, rotation);

            if (!instance.TryGetComponent<NetworkObject>(out var networkObject))
            {
                Debug.LogError($"[PlayerSpawner] '{characterPrefab.name}'에 NetworkObject가 없어 스폰을 취소합니다.");
                Destroy(instance);
                return;
            }

            // destroyWithScene을 켜야 로비로 돌아갈 때 캐릭터가 따라오지 않는다.
            // SpawnAsPlayerObject는 소유권 이전에 더해 IsPlayerObject를 세워주므로,
            // 클라이언트가 NetworkManager.LocalClient.PlayerObject로 자기 캐릭터를 찾을 수 있다.
            networkObject.SpawnAsPlayerObject(clientId, destroyWithScene: true);

            m_SpawnIndexByClientId[clientId] = spawnIndex;

            Debug.Log($"[PlayerSpawner] 스폰 clientId={clientId} spawnIndex={spawnIndex} position={position}");
        }

        /// <summary>
        /// 아무도 쓰지 않는 가장 작은 번호를 집는다.
        ///
        /// 접속 인원수를 그대로 쓰지 않는 이유: 중간에 누가 나가면 번호가 비는데,
        /// 인원수 기준으로는 이미 쓰고 있는 번호를 다시 집어 두 사람이 겹쳐 스폰된다.
        /// </summary>
        private int TakeFreeSpawnIndex()
        {
            for (var index = 0; ; index++)
            {
                if (!m_SpawnIndexByClientId.ContainsValue(index))
                {
                    return index;
                }
            }
        }

        /// <summary>
        /// 스폰 지점을 찾는다. 없으면 원점 주변에 흩어놓고 이유를 남긴다.
        ///
        /// 전부 원점에 겹쳐 놓으면 Rigidbody끼리 밀어내며 사방으로 튀어 나가서,
        /// 스폰 지점을 빠뜨렸다는 사실보다 물리 버그처럼 보인다.
        /// </summary>
        private void ResolvePose(int spawnIndex, out Vector3 position, out Quaternion rotation)
        {
            var points = PlayerSpawnPoints.Instance;

            if (points != null && points.TryGetPose(spawnIndex, out position, out rotation))
            {
                return;
            }

            Debug.LogError("[PlayerSpawner] 현재 씬에서 쓸 수 있는 PlayerSpawnPoints를 찾지 못했습니다. " +
                           "임시로 원점 주변에 스폰합니다. 씬에 스폰 지점을 배치하세요.");

            const float fallbackSpacing = 2f;

            position = new Vector3(spawnIndex * fallbackSpacing, 0f, 0f);
            rotation = Quaternion.identity;
        }
    }
}
