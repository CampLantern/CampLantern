using CampLantern.Combat.Data;
using UnityEngine;

namespace CampLantern.Combat.Monsters
{
    /// <summary>
    /// Attack — 스킬 시전 상태 (§4-5, §4-6).
    /// animDuration 동안 시전(타이머 기반 — 애니메이션 없어도 동작) → 종료 후 recoverTime 동안
    /// Recover(무방비·정지) → Chase 복귀(재판단). 이동 없음.
    ///
    /// hit window: 시전 경과 비율이 hitWindowStart 도달 시 EnableHitCheck, hitWindowEnd에서 Disable.
    /// Melee는 start~end 구간, 나머지 타입은 start==end 단일 시점(도달 프레임에 Enable — 골격은 1틱 후 자체 Disable).
    /// **안전장치 (§4-6)**: Exit에서 판정을 무조건 Disable — Stunned 취소 시 판정 잔류 방지.
    /// (MonsterController.TransitionTo/HaltMainState가 중단 경로 포함 Exit를 보장 — step-03 계약.)
    ///
    /// Stunned의 "메인 상태 타이머 일시정지"는 오버레이가 메인 Tick을 막는 구조로 자동 충족 —
    /// 이 상태의 m_timer는 Tick에서만 진행된다.
    /// Spec: 지시서 5단계 (§4-5, §4-6)
    /// </summary>
    public class AttackState : IMonsterState
    {
        private readonly int m_skillIndex;
        private SkillItem m_skill;
        private float m_timer;
        private bool m_recovering;
        private bool m_windowOpened;

        public string Name => m_recovering ? "Attack(Recover)" : "Attack";

        /// <summary>시전 중인 스킬 인덱스 — 애니 드라이버(step-07)가 Attack1~N 매핑에 사용.</summary>
        public int SkillIndex => m_skillIndex;

        public AttackState(int skillIndex)
        {
            m_skillIndex = skillIndex;
        }

        public void Enter(MonsterController owner)
        {
            m_skill = owner.Skills.GetSkill(m_skillIndex);
            owner.Skills.StartCooldown(m_skillIndex); // 쿨타임은 시전 시작 기산 (SkillRunner 주석 참조)
            m_timer = 0f;
            m_recovering = false;
            m_windowOpened = false;
        }

        public void Tick(MonsterController owner, float deltaTime)
        {
            m_timer += deltaTime;

            if (!m_recovering)
            {
                float duration = Mathf.Max(0.01f, m_skill.animDuration);
                float progress = m_timer / duration;

                if (owner.HitCheck != null)
                {
                    // 판정 창 개폐 — 데이터(hit window)로만. 단일 시점(start==end)도 도달 프레임에 Enable된다.
                    if (!m_windowOpened && progress >= m_skill.hitWindowStart)
                    {
                        owner.HitCheck.EnableHitCheck(m_skill, owner.gameObject);
                        m_windowOpened = true;
                    }
                    if (m_windowOpened && owner.HitCheck.IsEnabled && progress >= m_skill.hitWindowEnd
                        && m_skill.hitWindowEnd > m_skill.hitWindowStart)
                    {
                        owner.HitCheck.DisableHitCheck();
                    }
                }

                if (m_timer >= duration)
                {
                    owner.HitCheck?.DisableHitCheck(); // 시전 종료 — 창 잔류 방지
                    m_recovering = true;
                    m_timer = 0f;
                }
                return;
            }

            // Recover — 무방비 정지. 종료 후 Chase 복귀(다음 판단 틱에서 재판단).
            if (m_timer >= m_skill.recoverTime)
                owner.TransitionTo(new ChaseState());
        }

        public void Exit(MonsterController owner)
        {
            owner.HitCheck?.DisableHitCheck(); // §4-6 안전장치 — 어떤 경로로 나가도 무조건 Disable
        }
    }
}
