using UnityEngine;

namespace CampLantern.Combat.Monsters
{
    /// <summary>
    /// 오버레이 상태 — 메인 상태와 독립적으로 적용되는 레이어 (Stunned/Death, §4-4).
    /// 메인 FSM 전이와 무관하게 위에 얹히며, PausesMainState면 메인 상태 Tick이 정지된다
    /// (타이머 기반 상태(Attack 등)의 진행이 함께 멈추는 효과 — step-06 Stunned 요구).
    /// Spec: 지시서 3단계 (§4-4 오버레이 레이어)
    /// </summary>
    public interface IOverlayState
    {
        string Name { get; }

        /// <summary>true면 오버레이 지속 중 메인 상태 Tick 정지.</summary>
        bool PausesMainState { get; }

        void Apply(MonsterController owner);
        void Tick(MonsterController owner, float deltaTime);
        void Remove(MonsterController owner);
    }

    /// <summary>
    /// 사망 오버레이 — HP 0 도달 시 모든 상태 즉시 종료 + 사망 처리. 해제되지 않는다.
    /// 주의: 사망 처리에서 자식 오브젝트(박힌 화살)를 파괴하지 않는다 — 사망 후에도 화살 회수 가능 (§1-4, step-08).
    /// 사망 연출(Death 애니 트리거)은 step-07 MonsterAnimationDriver가 Died 이벤트로 처리.
    /// </summary>
    public class DeathOverlay : IOverlayState
    {
        public string Name => "Death";
        public bool PausesMainState => true;

        public void Apply(MonsterController owner)
        {
            owner.HaltMainState(); // 현재 상태 Exit 보장 (판정 잔류 방지 규약과 정합)
            Debug.Log($"[MonsterController] dead — {owner.name}");
        }

        public void Tick(MonsterController owner, float deltaTime) { }

        public void Remove(MonsterController owner) { } // Death는 해제 없음 (리스폰은 P0 범위 밖)
    }

    /// <summary>
    /// 기절 오버레이 — 실구현 (지시서 5단계).
    /// 메인 상태 타이머 일시정지: PausesMainState=true → 메인 Tick이 멈춰 Attack 시전/Exhausted 타이머가
    /// 함께 얼어붙는다 (오버레이 구조로 자동 충족). Exhausted 위에도 그대로 적용된다.
    /// 진행 중 공격 취소: Apply 시점에 Attack이면 Chase로 전이 — Exit 경유로 판정이 무조건 Disable된다
    /// (§4-6 안전장치 — 판정 잔류 방지). 해제 시 Chase 복귀 (지시서 명시 — Exhausted에서 걸렸어도 Chase로,
    /// Chase가 즉시 재판단하므로 조건이 그대로면 다시 Exhausted).
    /// 부여 주체(몬스터 스킬/상태이상)는 아직 없음 — MonsterController.ApplyStun(디버그/봇)으로 트리거.
    /// </summary>
    public class StunnedOverlay : IOverlayState
    {
        private readonly float m_duration;
        private float m_elapsed;

        public string Name => "Stunned";
        public bool PausesMainState => true;

        public StunnedOverlay(float durationSeconds)
        {
            m_duration = durationSeconds;
        }

        public void Apply(MonsterController owner)
        {
            // 진행 중 공격 취소 — TransitionTo가 Exit를 보장하므로 판정도 함께 꺼진다
            if (owner.CurrentStateName.StartsWith("Attack"))
                owner.TransitionTo(new ChaseState());
        }

        public void Tick(MonsterController owner, float deltaTime)
        {
            m_elapsed += deltaTime;
            if (m_elapsed >= m_duration)
                owner.RemoveOverlay(); // Remove → Chase 복귀
        }

        public void Remove(MonsterController owner)
        {
            if (owner.Health != null && owner.Health.CurrentHp > 0)
                owner.TransitionTo(new ChaseState()); // 해제 시 Chase 복귀 (사망 시엔 복귀하지 않음)
        }
    }
}
