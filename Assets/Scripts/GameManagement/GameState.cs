namespace TheShedding.GameManagement
{
    public enum GameState
    {
        Waiting,     // 로비에서 역할 선택 중
        InProgress,  // 라운드 진행 중
        FamilyWin,   // 가족승 (강도 전원 사망 OR 타임 오버)
        RobberWin    // 강도승 (HouseItem 목표치 도달)
    }
}
