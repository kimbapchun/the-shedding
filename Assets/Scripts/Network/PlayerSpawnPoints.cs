using System.Collections.Generic;
using UnityEngine;

namespace TheShedding.Network
{
    /// <summary>
    /// 게임 씬에 배치하는 스폰 지점 목록. 레벨 디자인을 코드에서 떼어놓기 위한 것이다.
    ///
    /// 씬 오브젝트라 씬이 로드된 뒤에야 존재한다. PlayerSpawner는 씬 로딩이 끝난
    /// 시점에만 이걸 찾으므로 실행 순서 문제는 생기지 않는다.
    /// </summary>
    public class PlayerSpawnPoints : MonoBehaviour
    {
        /// <summary>현재 씬의 인스턴스. 씬이 바뀌면 새 인스턴스로 교체된다.</summary>
        public static PlayerSpawnPoints Instance { get; private set; }

        [Tooltip("플레이어가 나타날 지점들. 순서가 곧 배정 순서다. 최소 인원수만큼 필요하다.")]
        [SerializeField] private List<Transform> spawnPoints = new List<Transform>();

        /// <summary>비어 있거나 구멍 난 슬롯을 뺀 실제 사용 가능한 지점 수.</summary>
        public int Count
        {
            get
            {
                var count = 0;

                for (var i = 0; i < spawnPoints.Count; i++)
                {
                    if (spawnPoints[i] != null)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        private void Awake()
        {
            // 씬 전환으로 새 인스턴스가 들어올 때 이전 것을 밀어내야 한다.
            // 이전 씬의 인스턴스는 곧 파괴되므로 남겨두면 파괴된 Transform을 가리키게 된다.
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// index번째 지점의 위치와 회전을 돌려준다.
        ///
        /// 인원이 지점 수보다 많으면 앞에서부터 다시 쓴다. 겹쳐서 스폰되는 편이
        /// 바닥 아래나 벽 안에 떨어뜨리는 것보다 낫다. Rigidbody가 밀어내 준다.
        /// </summary>
        public bool TryGetPose(int index, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;

            var usable = GetUsablePoints();

            if (usable.Count == 0)
            {
                return false;
            }

            var point = usable[index % usable.Count];

            position = point.position;
            rotation = point.rotation;

            return true;
        }

        /// <summary>
        /// Inspector에서 슬롯을 비워둔 채로 두는 실수가 잦아 매번 걸러낸다.
        /// 지점 수가 많아야 인원수 정도라 비용은 문제되지 않는다.
        /// </summary>
        private List<Transform> GetUsablePoints()
        {
            var usable = new List<Transform>(spawnPoints.Count);

            for (var i = 0; i < spawnPoints.Count; i++)
            {
                if (spawnPoints[i] != null)
                {
                    usable.Add(spawnPoints[i]);
                }
            }

            return usable;
        }

        private void OnDrawGizmos()
        {
            // 에디터에서 지점이 어디를 보고 있는지 알아야 캐릭터가 벽을 보고 서는 일을 막는다.
            for (var i = 0; i < spawnPoints.Count; i++)
            {
                var point = spawnPoints[i];

                if (point == null)
                {
                    continue;
                }

                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(point.position, 0.5f);

                Gizmos.color = Color.yellow;
                Gizmos.DrawRay(point.position, point.forward * 1.5f);
            }
        }
    }
}
