namespace CampLantern.Fishing
{
    /// <summary>
    /// 물고기 FSM 상태 (낚시 지시서 §3-1). 정교화 낚시의 진행 주체는 낚싯대가 아니라 물고기 개체다.
    /// 구 FishingRod의 타이밍 FSM(FishingState)과 별개 — 혼용 금지 (design/fishing-detailed step-02).
    /// </summary>
    public enum FishState
    {
        Idle,        // 스폰/실패 복귀 — 원래 위치 기준 자유 유영, 실루엣 표시
        Approach,    // 미끼 인지 → 미끼로 이동
        Bite,        // 미끼 도달 — 찌 흔들림, 고정 타이밍 윈도우. 진입 시 미끼 -1
        FightNormal, // 파이팅 평상 — 줄 흰색, 릴링 시 체력 감소
        FightEscape, // 도망 — 줄 빨간색, 릴링 시 텐션 감소
        FightShake,  // 비늘털이 — 점프 체공, 스윙 QTE 윈도우
        Hooked,      // 체력 0 — 줄 초록색, 낚아올림 대기
        Caught,      // 낚아올림 성공 — 개체 제거, 보상 지급
    }
}
