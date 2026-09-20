using UnityEngine;

namespace TheShedding.Items
{
    public abstract class ItemData : ScriptableObject
    {
        public int    id;
        public string displayName;
        public Sprite icon;
        public bool   countsAsStolen;
    }
}
