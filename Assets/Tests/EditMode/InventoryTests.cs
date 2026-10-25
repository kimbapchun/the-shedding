using NUnit.Framework;
using TheShedding.InventorySystem;
using UnityEngine;

namespace TheShedding.Tests
{
    public class InventoryTests
    {
        private GameObject go;
        private Inventory inventory;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("InventoryTests");
            inventory = go.AddComponent<Inventory>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(go);

        [Test]
        public void Adding_item_preserves_selected_duplicate_slot_and_emits_only_one_add()
        {
            inventory.AddItem(10);
            inventory.AddItem(10);
            inventory.SelectAt(1);
            int added = 0, removed = 0, selected = 0;
            inventory.OnItemAdded += (_, _) => added++;
            inventory.OnItemRemoved += (_, _) => removed++;
            inventory.OnSelectedItemChanged += _ => selected++;
            inventory.AddItem(20);
            Assert.AreEqual(1, inventory.SelectedIndex);
            Assert.AreEqual(1, added);
            Assert.AreEqual(0, removed);
            Assert.AreEqual(0, selected);
        }

        [Test]
        public void Removing_duplicate_by_index_removes_the_exact_slot()
        {
            inventory.AddItem(10);
            inventory.AddItem(20);
            inventory.AddItem(10);
            inventory.SelectAt(1);
            int removedIndex = -1;
            inventory.OnItemRemoved += (_, index) => removedIndex = index;
            Assert.IsTrue(inventory.RemoveAt(2));
            CollectionAssert.AreEqual(new[] { 10, 20 }, inventory.ItemIds);
            Assert.AreEqual(2, removedIndex);
            Assert.AreEqual(1, inventory.SelectedIndex);
            Assert.AreEqual(20, inventory.SelectedItemId);
        }

        [Test]
        public void Removing_before_selection_preserves_item_and_removing_selected_last_clamps()
        {
            inventory.AddItem(10);
            inventory.AddItem(20);
            inventory.AddItem(30);
            inventory.SelectAt(2);
            inventory.RemoveAt(0);
            Assert.AreEqual(30, inventory.SelectedItemId);
            Assert.AreEqual(1, inventory.SelectedIndex);
            inventory.RemoveAt(1);
            Assert.AreEqual(20, inventory.SelectedItemId);
            inventory.RemoveAt(0);
            Assert.IsFalse(inventory.HasSelection);
            Assert.AreEqual(Inventory.NoSelection, inventory.SelectedItemId);
        }

        [TestCase(-1)]
        [TestCase(1)]
        public void Invalid_removal_does_not_mutate_inventory(int index)
        {
            inventory.AddItem(10);
            Assert.IsFalse(inventory.RemoveAt(index));
            CollectionAssert.AreEqual(new[] { 10 }, inventory.ItemIds);
        }
    }
}
