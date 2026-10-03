using TheShedding.InventorySystem;
using TheShedding.Items;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheShedding.UI
{
    /// <summary>
    /// 인벤토리 동작을 눈으로 확인하기 위한 임시 디버그 UI.
    /// OnGUI로 그리므로 별도의 Canvas/프리팹 세팅이 필요 없다.
    ///
    /// 배치: 캐릭터(Inventory 컴포넌트를 가진 GameObject)에 함께 붙인다.
    /// 조작: 마우스 휠로 슬롯 이동. 숫자키 1~4 = 해당 id 아이템 넣기, X = 선택 아이템 제거.
    /// </summary>
    public class InventoryTestUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Inventory inventory;
        [SerializeField] private ItemDatabase itemDatabase;

        [Header("Layout")]
        [SerializeField] private Vector2 origin = new(20f, 20f);
        [SerializeField] private Vector2 slotSize = new(90f, 90f);
        [SerializeField] private float slotGap = 8f;

        [Header("Colors")]
        [SerializeField] private Color emptyColor    = new(0.15f, 0.15f, 0.15f, 0.85f);
        [SerializeField] private Color filledColor   = new(0.25f, 0.55f, 0.85f, 0.95f);
        [SerializeField] private Color selectedColor = new(1f,    0.85f, 0.20f, 1f);
        [SerializeField] private float selectedBorderThickness = 4f;

        [Header("Debug Hotkeys")]
        [SerializeField] private bool enableHotkeys = true;

        private static Texture2D whiteTex;

        private void Awake()
        {
            if (inventory == null) inventory = GetComponent<Inventory>();
        }

        private void Update()
        {
            if (!enableHotkeys || inventory == null) return;

            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.digit1Key.wasPressedThisFrame) inventory.AddItem(1);
            if (kb.digit2Key.wasPressedThisFrame) inventory.AddItem(2);
            if (kb.digit3Key.wasPressedThisFrame) inventory.AddItem(3);
            if (kb.digit4Key.wasPressedThisFrame) inventory.AddItem(4);
            if (kb.xKey.wasPressedThisFrame && inventory.HasSelection)
                inventory.RemoveItem(inventory.SelectedItemId);
        }

        private void OnGUI()
        {
            if (inventory == null) return;

            int capacity = inventory.Capacity;
            for (int i = 0; i < capacity; i++)
            {
                Rect rect = new(
                    origin.x + i * (slotSize.x + slotGap),
                    origin.y,
                    slotSize.x,
                    slotSize.y);

                bool hasItem   = i < inventory.Count;
                bool isSelected = i == inventory.SelectedIndex;

                DrawRect(rect, hasItem ? filledColor : emptyColor);

                if (isSelected)
                    DrawBorder(rect, selectedColor, selectedBorderThickness);

                var indexStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.UpperLeft,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.white },
                };
                GUI.Label(new Rect(rect.x + 6f, rect.y + 4f, 20f, 20f), i.ToString(), indexStyle);

                if (hasItem)
                {
                    int id = inventory.ItemIds[i];
                    string label = id.ToString();
                    if (itemDatabase != null && itemDatabase.TryGet(id, out var data) && data != null)
                        label = string.IsNullOrEmpty(data.displayName) ? label : data.displayName;

                    var labelStyle = new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontStyle = FontStyle.Bold,
                        normal = { textColor = Color.white },
                    };
                    GUI.Label(rect, label, labelStyle);
                }
            }

            Rect infoRect = new(
                origin.x,
                origin.y + slotSize.y + 12f,
                (slotSize.x + slotGap) * capacity + 200f,
                24f);
            var infoStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            GUI.Label(infoRect,
                $"Count: {inventory.Count}/{capacity}   " +
                $"SelectedIndex: {inventory.SelectedIndex}   " +
                $"SelectedItemId: {inventory.SelectedItemId}",
                infoStyle);

            if (enableHotkeys)
            {
                Rect hintRect = new(origin.x, infoRect.yMax + 4f, infoRect.width, 20f);
                GUI.Label(hintRect, "Hotkeys: 1~4 = add item, X = remove selected, Wheel = select");
            }
        }

        private static Texture2D White
        {
            get
            {
                if (whiteTex == null)
                {
                    whiteTex = new Texture2D(1, 1);
                    whiteTex.SetPixel(0, 0, Color.white);
                    whiteTex.Apply();
                }
                return whiteTex;
            }
        }

        private static void DrawRect(Rect rect, Color color)
        {
            var prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, White);
            GUI.color = prev;
        }

        private static void DrawBorder(Rect rect, Color color, float thickness)
        {
            DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            DrawRect(new Rect(rect.x, rect.y + rect.height - thickness, rect.width, thickness), color);
            DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            DrawRect(new Rect(rect.x + rect.width - thickness, rect.y, thickness, rect.height), color);
        }
    }
}
