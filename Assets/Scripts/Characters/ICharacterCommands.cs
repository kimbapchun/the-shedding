namespace TheShedding.Characters
{
    /// <summary>
    /// 현재 서버 중계가 연결된 상호작용·스킬 입력의 경계.
    /// Attack은 전투 네트워크 연동 시 서버 피격 판정과 함께 추가해야 한다.
    /// Jump는 현재 소유자 권위 이동/애니메이션 경로에 남긴다.
    /// 스킬별 중계 여부는 어댑터가 결정하며 로버 손전등은 아직 로컬 구현이다.
    /// </summary>
    public interface ICharacterCommands
    {
        void Interact();
        void Skill();
    }
}
