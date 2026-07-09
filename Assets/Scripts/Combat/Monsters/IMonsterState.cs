namespace CampLantern.Combat.Monsters
{
    /// <summary>
    /// 몬스터 메인 상태 — 상태 패턴 (enum+switch 금지, 지시서 3단계 명시 — step-06 Attack/Exhausted 확장 대비).
    /// Exit는 상태 중단(오버레이 개입 포함) 시에도 반드시 호출된다 — step-06 Attack의
    /// "Exit에서 판정 무조건 Disable" 안전장치(§4-6)가 이 계약 위에 선다.
    /// Spec: 지시서 3단계 (§4-4)
    /// </summary>
    public interface IMonsterState
    {
        string Name { get; }
        void Enter(MonsterController owner);
        void Tick(MonsterController owner, float deltaTime);
        void Exit(MonsterController owner);
    }
}
