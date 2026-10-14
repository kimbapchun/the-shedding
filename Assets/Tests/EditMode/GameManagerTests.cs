using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TheShedding.GameManagement;
using TheShedding.Items;
using UnityEngine;

namespace TheShedding.Tests
{
    /// <summary>
    /// GameManager 순수 로직 테스트. 네트워크 어댑터 없이 완전 격리된 환경에서
    /// 메서드 호출 → 상태 변경 → 이벤트 발행을 검증한다.
    ///
    /// GameManager가 네트워크 무지(string playerId만 사용)라서 가능한 테스트 범위.
    /// SerializeField 접근은 Reflection으로 처리 (public API 오염 없이 private 유지).
    /// </summary>
    public class GameManagerTests
    {
        private const int DefaultPlayersPerRobber = 4;
        private const float DefaultRoundDuration = 30f;
        private const int DefaultStolenGoal = 5;

        private GameObject go;
        private GameManager gm;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("GameManagerTests_GM");
            gm = go.AddComponent<GameManager>();

            // SerializeField initializer가 AddComponent 경로에서 적용되지 않을 수 있어 명시적으로 세팅.
            SetField(gm, "playersPerRobber", DefaultPlayersPerRobber);
            SetField(gm, "roundDurationSeconds", DefaultRoundDuration);
            SetField(gm, "stolenCountGoal", DefaultStolenGoal);
        }

        [TearDown]
        public void TearDown()
        {
            if (go != null) Object.DestroyImmediate(go);
        }

        // ═══════════════════════════════════════════════════════════════════
        //  RequiredRobbers 공식  (ceil(playerCount / playersPerRobber), 최소 1)
        // ═══════════════════════════════════════════════════════════════════

        [TestCase(0, 1)]
        [TestCase(1, 1)]
        [TestCase(2, 1)]
        [TestCase(3, 1)]
        [TestCase(4, 1)]
        [TestCase(5, 2)]
        [TestCase(6, 2)]
        [TestCase(7, 2)]
        [TestCase(8, 2)]
        [TestCase(9, 3)]
        public void RequiredRobbers_follows_ceil_formula(int playerCount, int expected)
        {
            for (int i = 1; i <= playerCount; i++) gm.RegisterPlayer($"p{i}");
            Assert.AreEqual(expected, gm.RequiredRobbers);
        }

        [Test]
        public void RequiredRobbers_updates_as_players_leave()
        {
            for (int i = 1; i <= 6; i++) gm.RegisterPlayer($"p{i}"); // 6 → 2
            Assert.AreEqual(2, gm.RequiredRobbers);

            gm.UnregisterPlayer("p6");
            gm.UnregisterPlayer("p5"); // 4명 남음 → 1
            Assert.AreEqual(1, gm.RequiredRobbers);
        }

        [Test]
        public void RequiredRobbers_respects_playersPerRobber_change()
        {
            for (int i = 1; i <= 5; i++) gm.RegisterPlayer($"p{i}");
            SetField(gm, "playersPerRobber", 2);
            // ceil(5/2) = 3
            Assert.AreEqual(3, gm.RequiredRobbers);
        }

        // ═══════════════════════════════════════════════════════════════════
        //  TrySelectRole  — 선점 / 교체 / 캐퍼시티
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void TrySelectRole_succeeds_for_registered_player()
        {
            gm.RegisterPlayer("p1");
            Assert.IsTrue(gm.TrySelectRole("p1", Role.Father));
            Assert.AreEqual(Role.Father, gm.GetRole("p1"));
        }

        [Test]
        public void TrySelectRole_rejects_unregistered_player()
        {
            Assert.IsFalse(gm.TrySelectRole("ghost", Role.Father));
            Assert.AreEqual(Role.Unassigned, gm.GetRole("ghost"));
        }

        [Test]
        public void TrySelectRole_rejects_null_or_empty_playerId()
        {
            Assert.IsFalse(gm.TrySelectRole("", Role.Father));
            Assert.IsFalse(gm.TrySelectRole(null, Role.Father));
        }

        [Test]
        public void TrySelectRole_rejects_Unassigned_role_input()
        {
            gm.RegisterPlayer("p1");
            Assert.IsFalse(gm.TrySelectRole("p1", Role.Unassigned));
        }

        [Test]
        public void TrySelectRole_family_role_rejects_second_claimer()
        {
            gm.RegisterPlayer("p1");
            gm.RegisterPlayer("p2");
            Assert.IsTrue(gm.TrySelectRole("p1", Role.Father));
            Assert.IsFalse(gm.TrySelectRole("p2", Role.Father));
            Assert.AreEqual(Role.Father, gm.GetRole("p1"));
            Assert.AreEqual(Role.Unassigned, gm.GetRole("p2"));
        }

        [Test]
        public void TrySelectRole_robber_allows_up_to_RequiredRobbers()
        {
            // 5명 → RequiredRobbers = 2
            for (int i = 1; i <= 5; i++) gm.RegisterPlayer($"p{i}");
            Assert.IsTrue(gm.TrySelectRole("p1", Role.Robber));
            Assert.IsTrue(gm.TrySelectRole("p2", Role.Robber));
            Assert.IsFalse(gm.TrySelectRole("p3", Role.Robber));
            Assert.AreEqual(2, gm.CountByRole(Role.Robber));
        }

        [Test]
        public void TrySelectRole_switching_role_releases_previous()
        {
            gm.RegisterPlayer("p1");
            Assert.IsTrue(gm.TrySelectRole("p1", Role.Father));
            Assert.IsTrue(gm.TrySelectRole("p1", Role.Mother));

            Assert.AreEqual(Role.Mother, gm.GetRole("p1"));
            Assert.AreEqual(0, gm.CountByRole(Role.Father));
            Assert.AreEqual(1, gm.CountByRole(Role.Mother));
        }

        [Test]
        public void TrySelectRole_reselecting_same_role_is_noop_true()
        {
            gm.RegisterPlayer("p1");
            gm.TrySelectRole("p1", Role.Father);

            Assert.IsTrue(gm.TrySelectRole("p1", Role.Father));
            Assert.AreEqual(Role.Father, gm.GetRole("p1"));
        }

        [Test]
        public void TrySelectRole_rejects_outside_waiting_state()
        {
            gm.RegisterPlayer("p1");
            gm.RegisterPlayer("p2");
            gm.TrySelectRole("p1", Role.Robber);
            gm.TrySelectRole("p2", Role.Father);
            gm.BeginRound();

            Assert.AreEqual(GameState.InProgress, gm.State);
            Assert.IsFalse(gm.TrySelectRole("p1", Role.Mother));
            Assert.AreEqual(Role.Robber, gm.GetRole("p1"));
        }

        // ═══════════════════════════════════════════════════════════════════
        //  AreAllPlayersReady  — 가드
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void AreAllPlayersReady_false_with_zero_players()
        {
            Assert.IsFalse(gm.AreAllPlayersReady());
        }

        [Test]
        public void AreAllPlayersReady_false_when_someone_unassigned()
        {
            gm.RegisterPlayer("p1");
            gm.RegisterPlayer("p2");
            gm.TrySelectRole("p1", Role.Robber);
            // p2는 역할 없음
            Assert.IsFalse(gm.AreAllPlayersReady());
        }

        [Test]
        public void AreAllPlayersReady_false_when_robber_count_below_required()
        {
            // 5명 → Required = 2
            for (int i = 1; i <= 5; i++) gm.RegisterPlayer($"p{i}");
            gm.TrySelectRole("p1", Role.Father);
            gm.TrySelectRole("p2", Role.Mother);
            gm.TrySelectRole("p3", Role.Baby);
            gm.TrySelectRole("p4", Role.Dog);
            gm.TrySelectRole("p5", Role.Robber); // 강도 1명뿐

            Assert.IsFalse(gm.AreAllPlayersReady());
        }

        [Test]
        public void AreAllPlayersReady_true_when_roles_exactly_match()
        {
            // 4명 → Required = 1
            for (int i = 1; i <= 4; i++) gm.RegisterPlayer($"p{i}");
            gm.TrySelectRole("p1", Role.Father);
            gm.TrySelectRole("p2", Role.Mother);
            gm.TrySelectRole("p3", Role.Baby);
            gm.TrySelectRole("p4", Role.Robber);

            Assert.IsTrue(gm.AreAllPlayersReady());
        }

        // ═══════════════════════════════════════════════════════════════════
        //  CheckVictory  — 다중 강도 승패
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void FamilyWin_when_single_robber_dies()
        {
            gm.RegisterPlayer("p1");
            gm.TrySelectRole("p1", Role.Robber);
            gm.BeginRound();

            gm.ReportPlayerDied("p1");
            Assert.AreEqual(GameState.FamilyWin, gm.State);
        }

        [Test]
        public void FamilyWin_waits_until_all_robbers_dead()
        {
            // 5명 → Required = 2, Robber 두 명
            for (int i = 1; i <= 5; i++) gm.RegisterPlayer($"p{i}");
            gm.TrySelectRole("p1", Role.Father);
            gm.TrySelectRole("p2", Role.Mother);
            gm.TrySelectRole("p3", Role.Baby);
            gm.TrySelectRole("p4", Role.Robber);
            gm.TrySelectRole("p5", Role.Robber);
            gm.BeginRound();

            gm.ReportPlayerDied("p4");
            Assert.AreEqual(GameState.InProgress, gm.State);

            gm.ReportPlayerDied("p5");
            Assert.AreEqual(GameState.FamilyWin, gm.State);
        }

        [Test]
        public void ReportPlayerDied_ignores_non_robber()
        {
            gm.RegisterPlayer("p1");
            gm.RegisterPlayer("p2");
            gm.TrySelectRole("p1", Role.Father);
            gm.TrySelectRole("p2", Role.Robber);
            gm.BeginRound();

            gm.ReportPlayerDied("p1"); // 가족 사망은 집계 안 됨
            Assert.AreEqual(GameState.InProgress, gm.State);
        }

        [Test]
        public void RobberWin_when_StolenCount_reaches_goal()
        {
            var db = MakeDatabase((100, true));
            SetField(gm, "itemDatabase", db);
            SetField(gm, "stolenCountGoal", 2);

            gm.RegisterPlayer("p1");
            gm.TrySelectRole("p1", Role.Robber);
            gm.BeginRound();

            gm.ReportItemCountChanged("p1", 100, +1);
            Assert.AreEqual(GameState.InProgress, gm.State);
            Assert.AreEqual(1, gm.StolenCount);

            gm.ReportItemCountChanged("p1", 100, +1);
            Assert.AreEqual(GameState.RobberWin, gm.State);
            Assert.AreEqual(2, gm.StolenCount);
        }

        [Test]
        public void ReportItemCountChanged_ignores_non_robber()
        {
            var db = MakeDatabase((100, true));
            SetField(gm, "itemDatabase", db);

            gm.RegisterPlayer("p1");
            gm.RegisterPlayer("p2");
            gm.TrySelectRole("p1", Role.Father);
            gm.TrySelectRole("p2", Role.Robber);
            gm.BeginRound();

            gm.ReportItemCountChanged("p1", 100, +1); // Father가 보고 → 무시
            Assert.AreEqual(0, gm.StolenCount);
        }

        [Test]
        public void ReportItemCountChanged_ignores_item_without_countsAsStolen()
        {
            var db = MakeDatabase((100, false)); // countsAsStolen=false
            SetField(gm, "itemDatabase", db);

            gm.RegisterPlayer("p1");
            gm.TrySelectRole("p1", Role.Robber);
            gm.BeginRound();

            gm.ReportItemCountChanged("p1", 100, +1);
            Assert.AreEqual(0, gm.StolenCount);
        }

        [Test]
        public void ReportItemCountChanged_negative_delta_decreases_StolenCount()
        {
            var db = MakeDatabase((100, true));
            SetField(gm, "itemDatabase", db);
            SetField(gm, "stolenCountGoal", 10);

            gm.RegisterPlayer("p1");
            gm.TrySelectRole("p1", Role.Robber);
            gm.BeginRound();

            gm.ReportItemCountChanged("p1", 100, +3);
            gm.ReportItemCountChanged("p1", 100, -1);
            Assert.AreEqual(2, gm.StolenCount);
        }

        [Test]
        public void ReportItemCountChanged_clamps_StolenCount_to_zero()
        {
            var db = MakeDatabase((100, true));
            SetField(gm, "itemDatabase", db);
            SetField(gm, "stolenCountGoal", 10);

            gm.RegisterPlayer("p1");
            gm.TrySelectRole("p1", Role.Robber);
            gm.BeginRound();

            gm.ReportItemCountChanged("p1", 100, -5);
            Assert.AreEqual(0, gm.StolenCount);
        }

        [Test]
        public void Reports_ignored_outside_InProgress()
        {
            var db = MakeDatabase((100, true));
            SetField(gm, "itemDatabase", db);

            gm.RegisterPlayer("p1");
            gm.TrySelectRole("p1", Role.Robber);
            // BeginRound 호출 안 함 → State = Waiting

            gm.ReportPlayerDied("p1");
            gm.ReportItemCountChanged("p1", 100, +1);

            Assert.AreEqual(GameState.Waiting, gm.State);
            Assert.AreEqual(0, gm.StolenCount);
        }

        // ═══════════════════════════════════════════════════════════════════
        //  타임아웃 → FamilyWin
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void Timeout_triggers_FamilyWin()
        {
            gm.RegisterPlayer("p1");
            gm.TrySelectRole("p1", Role.Robber);
            gm.BeginRound();

            // RoundEndTime을 과거로 당김 (private setter를 Reflection으로)
            SetProperty(gm, "RoundEndTime", Time.time - 1f);
            // Update()는 private — Reflection으로 호출
            InvokePrivate(gm, "Update");

            Assert.AreEqual(GameState.FamilyWin, gm.State);
        }

        [Test]
        public void Timeout_does_nothing_outside_InProgress()
        {
            SetProperty(gm, "RoundEndTime", Time.time - 1f);
            InvokePrivate(gm, "Update");
            Assert.AreEqual(GameState.Waiting, gm.State);
        }

        // ═══════════════════════════════════════════════════════════════════
        //  이벤트 발행 순서 / 시그니처
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void OnStateChanged_fires_once_per_actual_transition()
        {
            var events = new List<GameState>();
            gm.OnStateChanged += s => events.Add(s);

            gm.RegisterPlayer("p1");
            gm.TrySelectRole("p1", Role.Robber);
            gm.BeginRound();     // Waiting → InProgress
            gm.ReportPlayerDied("p1"); // InProgress → FamilyWin

            CollectionAssert.AreEqual(
                new[] { GameState.InProgress, GameState.FamilyWin },
                events);
        }

        [Test]
        public void OnStateChanged_not_fired_for_same_value()
        {
            int count = 0;
            gm.OnStateChanged += _ => count++;

            gm.ResetForNewRound(); // 이미 Waiting에서 Waiting → no fire
            Assert.AreEqual(0, count);
        }

        [Test]
        public void OnRoleAssigned_fires_on_select()
        {
            var assigned = new List<(string pid, Role role)>();
            gm.OnRoleAssigned += (pid, r) => assigned.Add((pid, r));

            gm.RegisterPlayer("p1");
            gm.TrySelectRole("p1", Role.Father);

            Assert.AreEqual(1, assigned.Count);
            Assert.AreEqual(("p1", Role.Father), assigned[0]);
        }

        [Test]
        public void OnRoleUnassigned_then_Assigned_on_role_switch()
        {
            var log = new List<string>();
            gm.OnRoleAssigned   += (pid, r) => log.Add($"A:{pid}:{r}");
            gm.OnRoleUnassigned += (pid, r) => log.Add($"U:{pid}:{r}");

            gm.RegisterPlayer("p1");
            gm.TrySelectRole("p1", Role.Father);
            gm.TrySelectRole("p1", Role.Mother);

            // 순서: Assigned(Father) → Unassigned(Father) → Assigned(Mother)
            CollectionAssert.AreEqual(
                new[] { "A:p1:Father", "U:p1:Father", "A:p1:Mother" },
                log);
        }

        [Test]
        public void OnRoleUnassigned_fires_on_Unregister()
        {
            var unassigned = new List<(string pid, Role role)>();
            gm.OnRoleUnassigned += (pid, r) => unassigned.Add((pid, r));

            gm.RegisterPlayer("p1");
            gm.TrySelectRole("p1", Role.Robber);
            gm.UnregisterPlayer("p1");

            Assert.AreEqual(1, unassigned.Count);
            Assert.AreEqual(("p1", Role.Robber), unassigned[0]);
        }

        [Test]
        public void OnStolenCountChanged_fires_only_on_actual_change()
        {
            var db = MakeDatabase((100, true));
            SetField(gm, "itemDatabase", db);
            SetField(gm, "stolenCountGoal", 10);

            var changes = new List<int>();
            gm.OnStolenCountChanged += c => changes.Add(c);

            gm.RegisterPlayer("p1");
            gm.TrySelectRole("p1", Role.Robber);
            gm.BeginRound();
            gm.ReportItemCountChanged("p1", 100, +1); // 0 → 1
            gm.ReportItemCountChanged("p1", 100, +1); // 1 → 2
            gm.ReportItemCountChanged("p1", 100, -1); // 2 → 1

            CollectionAssert.AreEqual(new[] { 1, 2, 1 }, changes);
        }

        [Test]
        public void OnRoundEndTimeChanged_fires_on_BeginRound()
        {
            float? captured = null;
            gm.OnRoundEndTimeChanged += t => captured = t;

            gm.RegisterPlayer("p1");
            gm.TrySelectRole("p1", Role.Robber);
            gm.BeginRound();

            Assert.IsTrue(captured.HasValue);
            Assert.AreEqual(Time.time + DefaultRoundDuration, captured.Value, 0.1f);
        }

        // ═══════════════════════════════════════════════════════════════════
        //  라운드 리셋 / 플레이어 등록 해제
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void ResetForNewRound_releases_all_roles_and_sets_Waiting()
        {
            gm.RegisterPlayer("p1");
            gm.RegisterPlayer("p2");
            gm.TrySelectRole("p1", Role.Robber);
            gm.TrySelectRole("p2", Role.Father);
            gm.BeginRound();

            var unassigned = new List<(string, Role)>();
            gm.OnRoleUnassigned += (pid, r) => unassigned.Add((pid, r));

            gm.ResetForNewRound();

            Assert.AreEqual(GameState.Waiting, gm.State);
            Assert.AreEqual(Role.Unassigned, gm.GetRole("p1"));
            Assert.AreEqual(Role.Unassigned, gm.GetRole("p2"));
            Assert.AreEqual(2, unassigned.Count);
        }

        [Test]
        public void UnregisterPlayer_releases_role_and_updates_RequiredRobbers()
        {
            for (int i = 1; i <= 5; i++) gm.RegisterPlayer($"p{i}");
            gm.TrySelectRole("p1", Role.Robber);
            Assert.AreEqual(2, gm.RequiredRobbers);

            gm.UnregisterPlayer("p1");

            Assert.AreEqual(Role.Unassigned, gm.GetRole("p1"));
            Assert.AreEqual(0, gm.CountByRole(Role.Robber));
            // 4명 남음 → Required = 1
            Assert.AreEqual(1, gm.RequiredRobbers);
        }

        [Test]
        public void BeginRound_clears_previous_round_state()
        {
            var db = MakeDatabase((100, true));
            SetField(gm, "itemDatabase", db);
            SetField(gm, "stolenCountGoal", 10);

            gm.RegisterPlayer("p1");
            gm.TrySelectRole("p1", Role.Robber);
            gm.BeginRound();
            gm.ReportItemCountChanged("p1", 100, +3);
            Assert.AreEqual(3, gm.StolenCount);

            gm.ResetForNewRound();
            gm.RegisterPlayer("p1"); // 재등록
            gm.TrySelectRole("p1", Role.Robber);
            gm.BeginRound();

            // 새 라운드 시작 시 StolenCount 리셋 (OnStolenCountChanged 3→0 발행)
            Assert.AreEqual(0, gm.StolenCount);
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Helpers
        // ═══════════════════════════════════════════════════════════════════

        private static void SetField<T>(GameManager target, string fieldName, T value)
        {
            var f = typeof(GameManager).GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(f, $"Private field '{fieldName}' not found on GameManager");
            f.SetValue(target, value);
        }

        private static void SetProperty<T>(GameManager target, string propName, T value)
        {
            var p = typeof(GameManager).GetProperty(propName,
                BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(p, $"Property '{propName}' not found on GameManager");
            p.SetValue(target, value); // Reflection은 private setter도 호출 가능
        }

        private static void InvokePrivate(GameManager target, string methodName)
        {
            var m = typeof(GameManager).GetMethod(methodName,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(m, $"Private method '{methodName}' not found on GameManager");
            m.Invoke(target, null);
        }

        /// <summary>테스트용 ItemDatabase를 ScriptableObject로 즉석 생성.</summary>
        private static ItemDatabase MakeDatabase(params (int id, bool countsAsStolen)[] entries)
        {
            var db = ScriptableObject.CreateInstance<ItemDatabase>();
            var items = new List<ItemData>();
            foreach (var (id, countsAsStolen) in entries)
            {
                var item = ScriptableObject.CreateInstance<HouseItem>();
                item.id = id;
                item.countsAsStolen = countsAsStolen;
                items.Add(item);
            }

            typeof(ItemDatabase)
                .GetField("items", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(db, items);
            typeof(ItemDatabase)
                .GetMethod("Rebuild", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(db, null);
            return db;
        }
    }
}
