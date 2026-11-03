using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TheShedding.Characters;
using TheShedding.GameManagement;
using UnityEngine;

namespace TheShedding.Tests
{
    public class NetworkGameplayRulesTests
    {
        private static readonly Role[] AllRoles = { Role.Robber, Role.Father, Role.Mother, Role.Baby, Role.Dog };

        [TestCase(2, 1)]
        [TestCase(4, 1)]
        [TestCase(5, 2)]
        public void Role_assignment_respects_capacity_and_duplicate_requests(int count, int robbers)
        {
            var roster = new RoleRoster();
            var roles = Enumerable.Range(0, count).Select(i => roster.Assign(i.ToString(), count, 4, AllRoles)).ToArray();
            Assert.AreEqual(robbers, roles.Count(r => r == Role.Robber));
            Assert.IsFalse(roles.Contains(Role.Unassigned));
            Assert.AreEqual(count - robbers, roles.Where(r => r != Role.Robber).Distinct().Count());
            Assert.AreEqual(roles[0], roster.Assign("0", count, 4, AllRoles));
            roster.Release("0");
            Assert.AreEqual(roles[0], roster.Assign("replacement", count, 4, AllRoles));
            roster.Clear();
            Assert.AreEqual(Role.Robber, roster.Assign("new-round", 2, 4, AllRoles));
        }

        [Test]
        public void Missing_prefabs_do_not_allocate_an_unspawnable_role()
        {
            var roster = new RoleRoster();
            Assert.AreEqual(Role.Unassigned, roster.Assign("a", 2, 4, Array.Empty<Role>()));
            Assert.AreEqual(Role.Robber, roster.Assign("a", 2, 4, AllRoles));
        }

        [Test]
        public void Selected_role_takes_priority_and_counts_toward_auto_assignment()
        {
            var roster = new RoleRoster();
            Assert.AreEqual(Role.Robber, roster.Assign("a", 2, 4, AllRoles, Role.Robber));
            Assert.AreEqual(Role.Father, roster.Assign("b", 2, 4, AllRoles));
            Assert.AreEqual(Role.Mother, roster.Assign("c", 3, 4, new[] { Role.Mother }, Role.Dog));
        }

        [Test]
        public void Replicated_death_raises_died_event_once()
        {
            var go = new GameObject("replicated robber");
            try
            {
                var robber = go.AddComponent<RobberCharacterController>();
                Awake(robber);
                int died = 0;
                robber.OnDied += () => died++;
                robber.ApplyReplicatedState(0, StatusEffect.None, 0f, 0f);
                robber.ApplyReplicatedState(0, StatusEffect.None, 0f, 0f);
                Assert.AreEqual(1, died);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Repeated_flashlight_stun_refreshes_end_time_only_at_interval()
        {
            var go = new GameObject("stunned father");
            try
            {
                var father = go.AddComponent<FatherController>();
                Awake(father);
                father.ApplyFlashlightStun(2f);
                float end = father.FlashlightStunEndTime;
                father.SynchronizeFlashlightStun(1.8f);
                float kept = father.FlashlightStunEndTime;
                father.ApplyFlashlightStun(2f);
                Assert.AreEqual(kept, father.FlashlightStunEndTime);
                father.SynchronizeFlashlightStun(1f);
                father.ApplyFlashlightStun(2f);
                Assert.AreEqual(end, father.FlashlightStunEndTime, 0.1f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Disabled_server_controller_expires_status_and_blocks_dead_actions()
        {
            var go = new GameObject("server robber");
            try
            {
                var robber = go.AddComponent<RobberCharacterController>();
                Awake(robber);
                robber.enabled = false;
                robber.NetworkSimulation = true;
                robber.TakeStatusEffect(StatusEffect.LimpAndBleed, -1f);
                robber.TickStatusEffects();
                Assert.AreEqual(StatusEffect.None, robber.CurrentStatusEffect);
                robber.TakeStatusEffect(StatusEffect.KnockedDown, -1f);
                robber.TickStatusEffects();
                Assert.AreEqual(StatusEffect.KnockedDown, robber.CurrentStatusEffect);
                robber.OnSkillInput();
                Assert.IsFalse(robber.IsFlashlightOn);
                robber.ApplyReplicatedState(0, StatusEffect.None, 0f, 0f);
                robber.OnSkillInput();
                Assert.IsFalse(robber.IsFlashlightOn);
                Assert.IsFalse(robber.IsActionAllowed);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void Flashlight_runs_with_disabled_controller_and_blocks_family_actions()
        {
            var source = new GameObject("robber");
            var target = new GameObject("mother");
            try
            {
                var robber = source.AddComponent<RobberCharacterController>();
                var mother = target.AddComponent<MotherController>();
                Awake(robber); Awake(mother);
                target.AddComponent<BoxCollider>();
                target.AddComponent<SphereCollider>();
                typeof(RobberCharacterController).GetField("familyLayer", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(robber, (LayerMask)~0);
                robber.enabled = false;
                robber.NetworkSimulation = true;
                robber.OnSkillInput();
                Physics.SyncTransforms();
                robber.EvaluateFlashlight();
                Assert.IsFalse(mother.IsActionAllowed);
                mother.SynchronizeFlashlightStun(0f);
                robber.OnSkillInput();
                robber.EvaluateFlashlight();
                Assert.IsTrue(mother.IsActionAllowed);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static void Awake(BaseCharacterController controller)
            => controller.GetType().GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(controller, null);
    }
}
