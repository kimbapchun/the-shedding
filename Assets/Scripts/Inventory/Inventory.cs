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
        [SerializeField] private int capacity = 5;

        private readonly List<int> itemIds = new();
        private int selectedId;

        public event Action<int> OnItemAdded;
        public event Action<int> OnItemRemoved;
        public event Action<int> OnSelectedItemChanged;

        public IReadOnlyList<int> ItemIds => itemIds;
        public int SelectedItemId => selectedId;
        public int Count           => itemIds.Count;
        public int Capacity        => capacity;
        public bool IsFull         => itemIds.Count >= capacity;

        public bool Contains(int id) => itemIds.Contains(id);

        // ── 유일한 변경 진입점 ────────────────────────────────────────────

        public bool AddItem(int id)
        {
            if (IsFull) return false;
            itemIds.Add(id);
            OnItemAdded?.Invoke(id);
            EnsureSelectionValid();
            return true;
        }

        public bool RemoveItem(int id)
        {
            int idx = itemIds.IndexOf(id);
            if (idx < 0) return false;
            itemIds.RemoveAt(idx);
            OnItemRemoved?.Invoke(id);
            EnsureSelectionValid();
            return true;
        }

        // ── 선택 슬롯 ─────────────────────────────────────────────────────

        public void SelectNext()
        {
            if (itemIds.Count == 0) return;
            int idx = Mathf.Max(0, itemIds.IndexOf(selectedId));
            idx = (idx + 1) % itemIds.Count;
            SetSelected(itemIds[idx]);
        }

        public void SelectPrevious()
        {
            if (itemIds.Count == 0) return;
            int idx = Mathf.Max(0, itemIds.IndexOf(selectedId));
            idx = (idx - 1 + itemIds.Count) % itemIds.Count;
            SetSelected(itemIds[idx]);
        }

        public bool Select(int id)
        {
            if (!itemIds.Contains(id)) return false;
            SetSelected(id);
            return true;
        }

        private void SetSelected(int id)
        {
            if (selectedId == id) return;
            selectedId = id;
            OnSelectedItemChanged?.Invoke(id);
        }

        // 목록이 바뀌었을 때 선택 값이 여전히 목록에 있는지 확인한다.
        // 없으면 목록의 첫 항목으로, 목록이 비면 0으로 초기화한다.
        private void EnsureSelectionValid()
        {
            if (itemIds.Count == 0)
            {
                SetSelected(0);
                return;
            }
            if (!itemIds.Contains(selectedId))
                SetSelected(itemIds[0]);
        }
    }
}
