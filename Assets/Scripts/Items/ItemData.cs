using TheShedding.Characters;
using UnityEngine;

namespace TheShedding.Items
{
    public abstract class ItemData : ScriptableObject
    {
        public int    id;
        public string displayName;
        public Sprite icon;
        public bool   countsAsStolen;

        /// <summary>
        /// 씬에 배치된 이 아이템을 <paramref name="interactor"/>가 주울 수 있는지 판정한다.
        /// 기본은 아무도 못 줍는다(false). 픽업 가능한 파생 클래스에서 override한다.
        /// </summary>
        public virtual bool CanBePickedUpBy(BaseCharacterController interactor) => false;
    }
}
