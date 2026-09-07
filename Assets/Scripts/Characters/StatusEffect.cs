namespace TheShedding.Characters
{
    public enum StatusEffect
    {
        None,
        Limp,         // 절뚝: 발자국 흔적만 남김, 이속 정상
        LimpAndBleed, // 절뚝&피: 이속 0.5배 + 핏자국 흔적
        KnockedDown   // 넘어짐: 행동 불가, 버둥버둥 게이지로 탈출
    }
}
