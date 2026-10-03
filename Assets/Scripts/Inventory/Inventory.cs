using System;
using System.Collections.Generic;
using UnityEngine;

namespace TheShedding.InventorySystem
{
    /// <summary>
    /// 아이템 ID 목록을 관리한다. 이름·아이콘·효과 같은 실체는 알지 못한다.
    ///
    /// 리스트를 변경하는 진입점은 <see cref="AddItem"/>과 <see cref="RemoveItem"/> 두 개뿐이다.
    /// 타입 기반 조회 같은 부가 기능은 InventoryQueries 확장 메서드에서 이 두 진입점만
    /// 사용해 구현한다.
    /// </summary>
    public class Inventory : MonoBehaviour
    {
        /// <summary>선택 슬롯이 비어 있음을 나타내는 sentinel. ItemData.id는 이 값과 달라야 한다.</summary>
        public const int NoSelection = -1;

        [SerializeField] private int capacity = 5;

        private readonly List<int> itemIds = new();
        private int selectedIndex = NoSelection;

        public event Action<int, int> OnItemAdded;             // (id, index)
        public event Action<int, int> OnItemRemoved;           // (id, index)
        public event Action<int>      OnSelectedItemChanged;   // 현재 선택된 아이템 id (없으면 NoSelection)

        public IReadOnlyList<int> ItemIds        => itemIds;
        public int                SelectedIndex  => selectedIndex;
        public int                SelectedItemId => HasSelection ? itemIds[selectedIndex] : NoSelection;
        public bool               HasSelection   => selectedIndex >= 0 && selectedIndex < itemIds.Count;
        public int                Count          => itemIds.Count;
        public int                Capacity       => capacity;
        public bool               IsFull         => itemIds.Count >= capacity;

        public bool Contains(int id) => itemIds.Contains(id);

        // ── 유일한 변경 진입점 ────────────────────────────────────────────

        public bool AddItem(int id)
        {
            if (id == NoSelection) return false;   // sentinel과 겹치는 id는 목록 정합성을 해친다
            if (IsFull) return false;
            int index = itemIds.Count;
            itemIds.Add(id);
            OnItemAdded?.Invoke(id, index);
            // 비어 있던 인벤이면 방금 추가한 항목을 자동 선택
            if (selectedIndex == NoSelection)
                SetSelectedIndex(0);
            return true;
        }

        public bool RemoveItem(int id)
        {
            int idx = itemIds.IndexOf(id);
            if (idx < 0) return false;

            int prevIndex = selectedIndex;
            int prevId = SelectedItemId;
            itemIds.RemoveAt(idx);
            OnItemRemoved?.Invoke(id, idx);
            AdjustSelectionAfterRemoval(idx);
            if (prevIndex != selectedIndex || prevId != SelectedItemId)
                OnSelectedItemChanged?.Invoke(SelectedItemId);
            return true;
        }

        // ── 선택 슬롯 ─────────────────────────────────────────────────────

        public void SelectNext()
        {
            if (itemIds.Count == 0) return;
            int next = selectedIndex < 0 ? 0 : (selectedIndex + 1) % itemIds.Count;
            SetSelectedIndex(next);
        }

        public void SelectPrevious()
        {
            if (itemIds.Count == 0) return;
            int prev = selectedIndex < 0
                ? itemIds.Count - 1
                : (selectedIndex - 1 + itemIds.Count) % itemIds.Count;
            SetSelectedIndex(prev);
        }

        public bool Select(int id)
        {
            int idx = itemIds.IndexOf(id);
            if (idx < 0) return false;
            SetSelectedIndex(idx);
            return true;
        }

        public bool SelectAt(int index)
        {
            if (index < 0 || index >= itemIds.Count) return false;
            SetSelectedIndex(index);
            return true;
        }

        private void SetSelectedIndex(int index)
        {
            if (selectedIndex == index) return;
            selectedIndex = index;
            OnSelectedItemChanged?.Invoke(SelectedItemId);
        }

        // 제거 위치에 따라 선택 인덱스만 조정 (이벤트 발화는 RemoveItem이 책임진다).
        // - 앞이 삭제되면 한 칸 밀림 (id는 유지될 수 있음)
        // - 선택 위치가 삭제되면 그 자리에 다음 아이템이 옴 (id가 바뀔 수 있음)
        // - 마지막 자리를 삭제했으면 목록 끝으로 클램프
        // - 뒤가 삭제되면 무영향
        private void AdjustSelectionAfterRemoval(int removedIndex)
        {
            if (itemIds.Count == 0)
            {
                selectedIndex = NoSelection;
                return;
            }
            if (removedIndex < selectedIndex)
                selectedIndex--;
            else if (removedIndex == selectedIndex && selectedIndex >= itemIds.Count)
                selectedIndex = itemIds.Count - 1;
        }
    }
}
