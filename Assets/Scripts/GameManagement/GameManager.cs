using System;
using System.Collections.Generic;
using TheShedding.Items;
using UnityEngine;

namespace TheShedding.GameManagement
{
    /// <summary>
    /// 라운드 상태, 역할 배정, 승패 판정의 책임자. 순수 게임 로직이며 네트워크를 모른다.
    /// 플레이어는 추상 식별자(string playerId)로 다루고, 네트워크 변환(ulong clientId 매핑,
    /// NetworkVariable, ServerRpc 등)은 별도 네트워크 어댑터가 담당한다 (local.md D-4).
    /// 어댑터는 이 클래스의 메서드를 호출하고 이벤트를 구독해 양방향 중계한다.
    /// </summary>
    public sealed class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Round")]
        [Tooltip("임시값. 기획 확정 후 조정.")]
        [SerializeField] private float roundDurationSeconds = 300f;

        [Header("Victory")]
        [Tooltip("강도가 보유해야 하는 countsAsStolen 아이템 수. 임시값.")]
        [SerializeField] private int stolenCountGoal = 5;

        [Tooltip("아이템 id → ItemData 조회. countsAsStolen 판별에 필요.")]
        [SerializeField] private ItemDatabase itemDatabase;

        [Header("Role Capacity")]
        [Tooltip("강도 1명당 플레이어 수. 공식: ceil(playerCount / playersPerRobber), 최소 1. " +
                 "기본값 4: 1~4명=1강도, 5~8명=2강도.")]
        [SerializeField, Min(1)] private int playersPerRobber = 4;

        [Header("Disconnect")]
        [Tooltip("라운드 중 접속이 끊긴 플레이어가 재접속할 수 있는 유예 시간(초). " +
                 "이 시간이 지나면 자동 사망 처리된다. 기획 임시값 60초.")]
        [SerializeField, Min(0f)] private float reconnectGraceSeconds = 60f;

        /// <summary>현재 등록된 플레이어 수에 따른 필수 강도 인원 (올림, 최소 1).</summary>
        public int RequiredRobbers =>
            Mathf.Max(1, Mathf.CeilToInt(registeredPlayerIds.Count / (float)playersPerRobber));

        // ── 상태 (외부 읽기 전용) ─────────────────────────────────────────

        public GameState State { get; private set; } = GameState.Waiting;

        /// <summary>Time.time 기준 라운드 종료 시각 (local.md B-3).</summary>
        public float RoundEndTime { get; private set; }

        public int StolenCount { get; private set; }

        public IReadOnlyCollection<string> RegisteredPlayerIds => registeredPlayerIds;

        // ── 이벤트 (어댑터가 구독해 NetworkVariable에 반영) ──────────────

        public event Action<GameState> OnStateChanged;
        public event Action<float> OnRoundEndTimeChanged;
        public event Action<int> OnStolenCountChanged;

        /// <summary>해당 플레이어가 역할을 새로 쥐었다. (playerId, role)</summary>
        public event Action<string, Role> OnRoleAssigned;

        /// <summary>해당 플레이어가 역할을 놓았다(변경·해제·언레지스터). (playerId, previousRole)</summary>
        public event Action<string, Role> OnRoleUnassigned;

        /// <summary>라운드 중 플레이어 접속이 끊김. 유예 시간 내 재접하지 않으면 사망 처리된다.
        /// (playerId, Time.time 기준 데드라인) — 어댑터가 다른 클라에 전파해 카운트다운 UI를 띄운다.</summary>
        public event Action<string, float> OnPlayerDisconnected;

        /// <summary>유예 시간 내 재접속 성공. (playerId)</summary>
        public event Action<string> OnPlayerReconnected;

        /// <summary>유예 만료로 사망 처리됨. (playerId) — 승패 상태 전이보다 먼저 발행된다.</summary>
        public event Action<string> OnPlayerDisconnectExpired;

        // ── 내부 상태 ─────────────────────────────────────────────────────

        /// <summary>플레이어 → 역할. 역방향 매핑이 자연스러워 다중 강도도 그대로 지원된다.</summary>
        private readonly Dictionary<string, Role> rolesByPlayer = new();
        private readonly HashSet<string> registeredPlayerIds = new();
        private readonly HashSet<string> deadRobbers = new();
        private readonly HashSet<string> deadFamily = new();

        /// <summary>playerId → Time.time 기준 복귀 데드라인. InProgress에서만 채워진다.</summary>
        private readonly Dictionary<string, float> disconnectDeadlines = new();

        /// <summary>Update에서 Dictionary 수정을 피하기 위한 재사용 버퍼.</summary>
        private readonly List<string> expiredBuffer = new();

        // ── Unity 생명주기 ────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (State != GameState.InProgress) return;

            // 재접속 유예 만료 처리 — 승패 상태로 전이할 수 있으니 라운드 타임아웃 체크보다 먼저.
            ProcessDisconnectTimeouts();
            if (State != GameState.InProgress) return;

            if (Time.time >= RoundEndTime)
                SetState(GameState.FamilyWin);
        }

        // ── 플레이어 등록 (어댑터가 접속/퇴장 시 호출) ────────────────────

        public void RegisterPlayer(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return;
            registeredPlayerIds.Add(playerId);
        }

        /// <summary>
        /// 완전 이탈(명시적 퇴장). 라운드 중 소켓 끊김은 <see cref="MarkDisconnected"/>를 쓸 것.
        /// 라운드 중 호출되면 승패 산정이 틀어지지 않도록 dead/유예 집합을 함께 정리하고 재평가한다.
        /// </summary>
        public void UnregisterPlayer(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return;

            registeredPlayerIds.Remove(playerId);
            disconnectDeadlines.Remove(playerId);
            deadRobbers.Remove(playerId);
            deadFamily.Remove(playerId);
            ClearRoleOf(playerId);

            if (State == GameState.InProgress) CheckVictory();
        }

        // ── 접속 끊김 / 재접속 (어댑터가 소켓 이벤트에서 호출) ────────────

        /// <summary>
        /// 라운드 중이면 <see cref="reconnectGraceSeconds"/> 유예 타이머를 건다. 만료 시 자동 사망.
        /// 라운드 외 상태에서는 즉시 완전 이탈 처리. 이미 사망한 플레이어는 노옵.
        /// 어댑터는 상태를 알 필요 없이 소켓 끊김 시점에 호출만 하면 된다.
        /// </summary>
        public void MarkDisconnected(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return;
            if (!registeredPlayerIds.Contains(playerId)) return;

            if (State != GameState.InProgress)
            {
                UnregisterPlayer(playerId);
                return;
            }

            // 이미 사망한 플레이어의 끊김은 승패에 영향 없음.
            if (deadRobbers.Contains(playerId) || deadFamily.Contains(playerId)) return;

            // 같은 사람이 두 번 끊길 수 없지만 어댑터 중복 호출 가드.
            if (disconnectDeadlines.ContainsKey(playerId)) return;

            float deadline = Time.time + reconnectGraceSeconds;
            disconnectDeadlines[playerId] = deadline;
            OnPlayerDisconnected?.Invoke(playerId, deadline);
        }

        /// <summary>
        /// 유예 중이던 플레이어가 돌아왔다. 데드라인이 이미 만료된 뒤 호출되면 노옵
        /// (만료 처리는 Update에서 이미 수행됨).
        /// </summary>
        public void MarkReconnected(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return;
            if (!disconnectDeadlines.Remove(playerId)) return;
            OnPlayerReconnected?.Invoke(playerId);
        }

        private void ProcessDisconnectTimeouts()
        {
            if (disconnectDeadlines.Count == 0) return;

            float now = Time.time;
            expiredBuffer.Clear();
            foreach (var kv in disconnectDeadlines)
                if (now >= kv.Value) expiredBuffer.Add(kv.Key);

            if (expiredBuffer.Count == 0) return;

            foreach (var pid in expiredBuffer)
            {
                disconnectDeadlines.Remove(pid);
                var role = GetRole(pid);
                if (role == Role.Unassigned) continue; // 역할 없으면 승패에 영향 없음.
                if (role == Role.Robber) deadRobbers.Add(pid);
                else                     deadFamily.Add(pid);
                OnPlayerDisconnectExpired?.Invoke(pid);
            }
            expiredBuffer.Clear();
            CheckVictory();
        }

        // ── 역할 선택 ─────────────────────────────────────────────────────

        public bool TrySelectRole(string playerId, Role role)
        {
            if (State != GameState.Waiting) return false;
            if (string.IsNullOrEmpty(playerId)) return false;
            if (role == Role.Unassigned) return false;
            if (!registeredPlayerIds.Contains(playerId)) return false;

            // 같은 역할을 다시 눌렀으면 그대로 통과.
            if (rolesByPlayer.TryGetValue(playerId, out var current) && current == role)
                return true;

            // 해당 역할 캐퍼시티가 꽉 찼으면 거절. (내가 그 역할을 쥔 경우는 위에서 통과했으므로 여기 안 옴)
            if (CountByRole(role) >= GetCapacity(role))
                return false;

            ClearRoleOf(playerId);
            rolesByPlayer[playerId] = role;
            OnRoleAssigned?.Invoke(playerId, role);
            return true;
        }

        public Role GetRole(string playerId)
            => rolesByPlayer.TryGetValue(playerId, out var r) ? r : Role.Unassigned;

        /// <summary>해당 역할을 쥔 플레이어들을 열거한다. 다중 강도 지원을 위해 복수형.</summary>
        public IEnumerable<string> GetOwners(Role role)
        {
            foreach (var kv in rolesByPlayer)
                if (kv.Value == role) yield return kv.Key;
        }

        public int CountByRole(Role role)
        {
            int c = 0;
            foreach (var r in rolesByPlayer.Values)
                if (r == role) c++;
            return c;
        }

        public bool AreAllPlayersReady()
        {
            if (registeredPlayerIds.Count == 0) return false;
            // 한 번의 iterate로 "전원 역할 쥠" + "강도 수 == RequiredRobbers"를 함께 확인한다.
            int robbers = 0;
            foreach (var pid in registeredPlayerIds)
            {
                if (!rolesByPlayer.TryGetValue(pid, out var role)) return false;
                if (role == Role.Robber) robbers++;
            }
            return robbers == RequiredRobbers;
        }

        private int GetCapacity(Role role) => role == Role.Robber ? RequiredRobbers : 1;

        private void ClearRoleOf(string playerId)
        {
            if (!rolesByPlayer.TryGetValue(playerId, out var current)) return;
            rolesByPlayer.Remove(playerId);
            OnRoleUnassigned?.Invoke(playerId, current);
        }

        // ── 라운드 흐름 (어댑터가 씬 로딩 완료에 맞춰 호출) ───────────────

        /// <summary>라운드 시작. 성공 시 <c>true</c>. State가 Waiting이 아니거나 ready가 아니면 <c>false</c>.</summary>
        public bool BeginRound()
        {
            if (State != GameState.Waiting) return false;
            if (!AreAllPlayersReady()) return false;

            deadRobbers.Clear();
            deadFamily.Clear();
            disconnectDeadlines.Clear();
            SetStolenCount(0);
            SetRoundEndTime(Time.time + roundDurationSeconds);
            SetState(GameState.InProgress);
            return true;
        }

        public void ResetForNewRound()
        {
            SetState(GameState.Waiting);
            SetStolenCount(0);
            deadRobbers.Clear();
            deadFamily.Clear();
            disconnectDeadlines.Clear();

            // Dictionary 순회 중 변경을 피하기 위해 스냅샷을 뜨고 비운다.
            if (rolesByPlayer.Count == 0) return;
            var snapshot = new List<KeyValuePair<string, Role>>(rolesByPlayer);
            rolesByPlayer.Clear();
            foreach (var kv in snapshot)
                OnRoleUnassigned?.Invoke(kv.Key, kv.Value);
        }

        // ── 집계 (어댑터가 호출) ──────────────────────────────────────────

        public void ReportPlayerDied(string playerId)
        {
            if (State != GameState.InProgress) return;
            var role = GetRole(playerId);
            if (role == Role.Unassigned) return;

            if (role == Role.Robber) deadRobbers.Add(playerId);
            else                     deadFamily.Add(playerId);

            CheckVictory();
        }

        public void ReportItemCountChanged(string playerId, int itemId, int delta)
        {
            if (State != GameState.InProgress) return;
            if (GetRole(playerId) != Role.Robber) return;

            if (itemDatabase == null)
            {
                Debug.LogWarning("[GameManager] itemDatabase 미할당 — Stolen 집계 안 됨", this);
                return;
            }

            var data = itemDatabase.Get(itemId);
            if (data == null)
            {
                Debug.LogWarning($"[GameManager] itemId={itemId}가 ItemDatabase에 없음", this);
                return;
            }
            if (!data.countsAsStolen) return; // 일반 아이템은 정상 흐름 (조용히 무시)

            // 아기가 훔쳐가면 강도 Inventory에서 Remove가 발생해 음수 delta로 자동 감산된다.
            SetStolenCount(Mathf.Max(0, StolenCount + delta));
            CheckVictory();
        }

        private void CheckVictory()
        {
            if (State != GameState.InProgress) return;

            // 강도 전원 사망 → 가족승. 강도 0명일 때 자동 가족승이 되지 않도록 >0 가드.
            int robberCount = CountByRole(Role.Robber);
            if (robberCount > 0 && deadRobbers.Count >= robberCount)
            {
                SetState(GameState.FamilyWin);
                return;
            }

            // 가족 전원 사망 → 강도승. 가족 0명일 때 자동 강도승이 되지 않도록 >0 가드.
            int familyCount = CountFamily();
            if (familyCount > 0 && deadFamily.Count >= familyCount)
            {
                SetState(GameState.RobberWin);
                return;
            }

            if (StolenCount >= stolenCountGoal)
            {
                SetState(GameState.RobberWin);
            }
        }

        private int CountFamily()
        {
            // Role.Robber 외의 모든 할당된 역할이 가족. Unassigned는 rolesByPlayer에 없음.
            int count = 0;
            foreach (var r in rolesByPlayer.Values)
                if (r != Role.Robber) count++;
            return count;
        }

        // ── 상태 변경 통로 (이벤트 발행 일원화) ────────────────────────────

        private void SetState(GameState next)
        {
            if (State == next) return;
            State = next;
            OnStateChanged?.Invoke(next);
        }

        private void SetRoundEndTime(float value)
        {
            if (Mathf.Approximately(RoundEndTime, value)) return;
            RoundEndTime = value;
            OnRoundEndTimeChanged?.Invoke(value);
        }

        private void SetStolenCount(int value)
        {
            if (StolenCount == value) return;
            StolenCount = value;
            OnStolenCountChanged?.Invoke(value);
        }
    }
}
