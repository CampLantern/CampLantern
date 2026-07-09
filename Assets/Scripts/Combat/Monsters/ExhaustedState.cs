using UnityEngine;

namespace CampLantern.Combat.Monsters
{
    /// <summary>
    /// Exhausted — 탈진 (§4-5). 후보 스킬 없음 + 타겟 사거리 내일 때 진입 (ChaseState 판단 틱).
    /// 지속시간 = 가장 빨리 도는 스킬의 잔여 쿨타임(진입 시점 런타임 계산). 이동 정지, 피격 정상(프리딜 구간).
    /// 탈진 중 타겟이 추격 반경 이탈 시 탈진을 끊고 Return.
    /// Stunned가 위에 얹히면 메인 Tick이 멈춰 탈진 타이머도 정지한다 (오버레이 구조 — step-03).
    /// Spec: 지시서 5단계 (§4-5)
    /// </summary>
    public class ExhaustedState : IMonsterState
    {
        private float m_remaining;

        public string Name => "Exhausted";

        public void Enter(MonsterController owner)
        {
            m_remaining = owner.Skills != null ? owner.Skills.ShortestRemainingCooldown() : 0f;
        }

        public void Tick(MonsterController owner, float deltaTime)
        {
            // 이탈 체크 — 탈진을 끊고 Return (§4-5)
            Transform nearest = owner.FindNearestPlayer();
            if (nearest == null || owner.PlanarDistanceTo(nearest.position) > owner.Data.chaseRadius)
            {
                owner.TransitionTo(new ReturnState());
                return;
            }

            m_remaining -= deltaTime;
            if (m_remaining <= 0f)
                owner.TransitionTo(new ChaseState()); // 재판단 — 쿨 하나는 돌았으므로 보통 즉시 Attack
        }

        public void Exit(MonsterController owner) { }
    }
}
