using UnityEngine;

namespace CampLantern.Combat.Monsters
{
    // 세 상태를 한 파일에 둔 근거: 상태가 작고(각 30줄 내외) 상호 전이 조건을 한눈에 볼 수 있다.
    // step-06에서 Attack/Exhausted가 커지면 그때 파일 분리를 판단한다.
    // Spec: 지시서 3단계 (§4-4)

    /// <summary>Idle — 활동 반경 내 랜덤 지점 배회, 도착하면 잠시 대기 후 다음 지점. 감지 반경 진입 시 Chase.</summary>
    public class IdleState : IMonsterState
    {
        public string Name => "Idle";

        private Vector3 m_roamPoint;
        private float m_waitTimer;
        private bool m_waiting;

        public void Enter(MonsterController owner)
        {
            PickNextRoamPoint(owner);
        }

        public void Tick(MonsterController owner, float deltaTime)
        {
            // 감지 — 플레이어가 감지 반경 진입 시 Chase 전환
            Transform nearest = owner.FindNearestPlayer();
            if (nearest != null && owner.PlanarDistanceTo(nearest.position) <= owner.Data.detectRadius)
            {
                owner.SetCurrentTarget(nearest);
                owner.TransitionTo(new ChaseState());
                return;
            }

            if (m_waiting)
            {
                m_waitTimer -= deltaTime;
                if (m_waitTimer <= 0f) PickNextRoamPoint(owner);
                return;
            }

            owner.MoveTowards(m_roamPoint, owner.Data.idleMoveSpeed);
            if (owner.HasArrived(m_roamPoint))
            {
                m_waiting = true;
                m_waitTimer = Random.Range(owner.IdleWaitMin, owner.IdleWaitMax);
            }
        }

        public void Exit(MonsterController owner) { }

        private void PickNextRoamPoint(MonsterController owner)
        {
            Vector2 offset = Random.insideUnitCircle * owner.Data.roamRadius;
            m_roamPoint = owner.SpawnPosition + new Vector3(offset.x, 0f, offset.y);
            m_waiting = false;
        }
    }

    /// <summary>
    /// Chase — 타겟 추적 (chaseMoveSpeed). 타겟은 AggroController 조회 (규칙 1~5는 어그로 소관, step-05).
    /// 추격 반경 판정은 어그로 타겟이 아니라 **가장 가까운 플레이어** 기준 — 가장 가까운 플레이어가
    /// 반경 밖이면 전원이 밖이므로 무조건 즉시 Return (§4-3 규칙 6, 약점 고정보다 우선).
    /// AggroController가 없으면 가장 가까운 플레이어 폴백 (검증 봇 등 최소 조립 호환).
    /// </summary>
    public class ChaseState : IMonsterState
    {
        // 판단 틱 주기 — 매 프레임 후보 계산 방지 (90Hz). TODO(TUNING)
        private const float k_decisionInterval = 0.25f;
        private float m_decisionTimer;

        public string Name => "Chase";

        public void Enter(MonsterController owner)
        {
            owner.Aggro?.AcquireIfNone(); // 초기 타겟 획득 — 유효타 트리거 밖의 최초 1회
            m_decisionTimer = 0f;         // 진입 즉시 첫 판단
        }

        public void Tick(MonsterController owner, float deltaTime)
        {
            Transform nearest = owner.FindNearestPlayer();

            if (nearest == null || owner.PlanarDistanceTo(nearest.position) > owner.Data.chaseRadius)
            {
                owner.TransitionTo(new ReturnState());
                return;
            }

            Transform target = owner.Aggro != null ? owner.Aggro.CurrentTarget : nearest;
            if (target == null) target = nearest;

            owner.SetCurrentTarget(target);

            // 스킬 판단 틱 (§4-7) — 후보 있음 → 균등 랜덤 시전 / 없음 + 사거리 내 → Exhausted / 밖 → 추격 유지
            m_decisionTimer -= deltaTime;
            if (m_decisionTimer <= 0f && owner.Skills != null && owner.Skills.SkillCount > 0)
            {
                m_decisionTimer = k_decisionInterval;
                float dist = owner.PlanarDistanceTo(target.position);

                if (owner.Skills.TrySelectSkill(dist, out int skillIndex))
                {
                    owner.TransitionTo(new AttackState(skillIndex));
                    return;
                }
                if (owner.Skills.AnySkillReaches(dist))
                {
                    owner.TransitionTo(new ExhaustedState());
                    return;
                }
            }

            owner.MoveTowards(target.position, owner.Data.chaseMoveSpeed);
        }

        public void Exit(MonsterController owner) { }
    }

    /// <summary>
    /// Return — 원위치(스폰 지점) 복귀. 진입 즉시 무적(모든 ApplyDamage 무시) + 타겟 해제(어그로 무시),
    /// 도달 시 HP 전량 회복 후 Idle 복귀. 귀환 속도는 chaseMoveSpeed 재사용 (지시서에 별도 수치 없음 — TODO(TUNING)).
    /// </summary>
    public class ReturnState : IMonsterState
    {
        public string Name => "Return";

        public void Enter(MonsterController owner)
        {
            owner.Aggro?.ResetAggro(); // 규칙 6 — 반경 이탈 리셋은 약점 고정보다 우선
            owner.SetCurrentTarget(null);
            owner.Health.SetInvulnerable(true);
        }

        public void Tick(MonsterController owner, float deltaTime)
        {
            owner.MoveTowards(owner.SpawnPosition, owner.Data.chaseMoveSpeed);

            if (owner.HasArrived(owner.SpawnPosition))
            {
                owner.Health.ResetToFull();
                owner.TransitionTo(new IdleState());
            }
        }

        public void Exit(MonsterController owner)
        {
            owner.Health.SetInvulnerable(false); // 중단(사망 불가·오버레이 개입) 포함 어떤 경로로 나가도 무적 해제
        }
    }
}
