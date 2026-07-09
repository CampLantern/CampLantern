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
    /// 기절 오버레이 — **구조만** (지시서 3단계). 실구현(메인 타이머 정지·진행 중 공격 취소·해제 시 Chase 복귀,
    /// Exhausted 위 적용)은 step-06에서. PausesMainState=true 골격만 확정해 둔다.
    /// </summary>
    public class StunnedOverlay : IOverlayState
    {
        public string Name => "Stunned";
        public bool PausesMainState => true;

        public void Apply(MonsterController owner) { /* step-06: 공격 취소 훅 */ }
        public void Tick(MonsterController owner, float deltaTime) { /* step-06: 지속시간·해제 */ }
        public void Remove(MonsterController owner) { /* step-06: Chase 복귀 */ }
    }
}
