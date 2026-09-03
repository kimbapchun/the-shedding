namespace TheShedding.Network
{
    public enum ConnectionState
    {
        Disconnected, // 연결 없음 (초기 상태, 종료 후 상태)
        Connecting,   // 연결 시도 중
        Connected     // 연결 완료
    }
}
