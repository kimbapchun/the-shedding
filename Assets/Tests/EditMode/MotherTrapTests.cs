using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TheShedding.Characters;
using TheShedding.Items;
using UnityEngine;

namespace TheShedding.Tests
{
    public class MotherTrapTests
    {
        [Test]
        public void Explicit_trap_request_preserves_selection_and_validates_ownership_and_cooldown()
        {
            var go = new GameObject("MotherTrapTests");
            var database = ScriptableObject.CreateInstance<ItemDatabase>();
            var trap = ScriptableObject.CreateInstance<TrapItem>();
            try
            {
                var mother = go.AddComponent<MotherController>();
                // EditMode에서는 MonoBehaviour Awake를 직접 호출한다.
                typeof(MotherController).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(mother, null);
                trap.id = 20;
                typeof(ItemDatabase).GetField("items", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(database, new List<ItemData> { trap });
                typeof(ItemDatabase).GetMethod("Rebuild", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(database, null);
                typeof(MotherController).GetField("itemDatabase", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(mother, database);
                mother.Inventory.AddItem(10);
                int selectedEvents = 0, placed = 0, cooldowns = 0;
                mother.OnTrapCooldownStarted += duration =>
                {
                    Assert.AreEqual(5f, duration);
                    cooldowns++;
                };
                mother.Inventory.OnSelectedItemChanged += _ => selectedEvents++;
                mother.OnTrapPlaced += (_, _) => placed++;
                Assert.IsFalse(mother.TryPlaceTrap(20)); // 정의는 있어도 소유하지 않음
                mother.Inventory.AddItem(20);
                mother.Inventory.AddItem(20);
                mother.TrapPlacement = (_, _) => false;
                Assert.IsFalse(mother.TryPlaceTrap(20)); // 미등록 프리팹 등 설치 실패 시 보존
                CollectionAssert.AreEqual(new[] { 10, 20, 20 }, mother.Inventory.ItemIds);
                mother.TrapPlacement = (_, _) => true;
                Assert.IsFalse(mother.TryPlaceTrap(10)); // 함정이 아닌 ID
                Assert.IsTrue(mother.TryPlaceTrap(20));
                Assert.AreEqual(10, mother.Inventory.SelectedItemId);
                Assert.AreEqual(0, selectedEvents);
                Assert.AreEqual(1, placed);
                Assert.IsFalse(mother.TryPlaceTrap(20)); // 재요청은 쿨타임으로 차단
                Assert.AreEqual(1, cooldowns); // 실패 요청은 복제 이벤트를 발생시키지 않음
                CollectionAssert.AreEqual(new[] { 10, 20 }, mother.Inventory.ItemIds);

                mother.SynchronizeTrapCooldown(10f);
                Assert.IsFalse(mother.TryPlaceTrap(20));
                mother.SynchronizeTrapCooldown(0f);
                mother.Inventory.SelectNext();
                mother.OnSkillInput(); // 호스트/로컬 입력도 같은 검증·이벤트 경로
                Assert.AreEqual(2, placed);
                Assert.AreEqual(2, cooldowns);
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(database);
                Object.DestroyImmediate(trap);
            }
        }
    }
}
