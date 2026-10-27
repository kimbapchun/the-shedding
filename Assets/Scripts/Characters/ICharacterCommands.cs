namespace TheShedding.Characters
{
    /// <summary>소유자의 게임플레이 요청을 서버 판정으로 연결한다. 이동은 소유자 권위.</summary>
    public interface ICharacterCommands
    {
        void Interact();
        void Skill();
        void Attack();
        void UseHealItem();
        void DropItem();
        void SetSitting(bool sitting);
        void SetLying(bool lying);
    }
}
