using CampLantern.Combat.Data;
using UnityEngine;

namespace CampLantern.Combat.Monsters
{
    /// <summary>
    /// 몬스터 스킬 쿨타임 관리 + 시전 후보 선택 (§4-7).
    ///
    /// 후보 = skills 중 (쿨타임 완료) AND (타겟 거리 ≥ minEngage) AND (Melee/Point는 사거리 내).
    /// 후보 있음 → **균등 랜덤** 1개. 중복 방지 로직 금지, weight는 필드만 읽고 로직은 균등 랜덤 유지 (지시서 명시).
    ///
    /// 쿨타임은 **시전 시작 시점**(AttackState.Enter) 기산 — Exhausted 지속(최단 잔여 쿨타임, §4-5)이
    /// 시전/회복 시간과 무관하게 일관되도록. 쿨타임은 상태와 무관하게 항상 진행된다
    /// (Stunned의 "메인 상태 타이머 정지"는 Attack 시전 타이머에만 해당 — 별도 컴포넌트라 자연 분리).
    /// Spec: 지시서 5단계 (§4-5, §4-7)
    /// </summary>
    public class SkillRunner : MonoBehaviour
    {
        [SerializeField] private MonsterData m_data;

        private float[] m_cooldownRemaining;

        public MonsterData Data => m_data;
        public int SkillCount => m_data != null && m_data.skills != null ? m_data.skills.Count : 0;

        /// <summary>Editor 팩토리·런타임 조립용 데이터 주입.</summary>
        public void Configure(MonsterData data)
        {
            m_data = data;
            EnsureCooldownArray();
        }

        private void Awake()
        {
            EnsureCooldownArray();
        }

        private void Update()
        {
            if (m_cooldownRemaining == null) return;
            float dt = Time.deltaTime;
            for (int i = 0; i < m_cooldownRemaining.Length; i++)
                if (m_cooldownRemaining[i] > 0f) m_cooldownRemaining[i] -= dt;
        }

        public SkillItem GetSkill(int index)
        {
            return m_data.skills[index];
        }

        /// <summary>시전 시작 — 쿨타임 기산 (AttackState.Enter에서 호출).</summary>
        public void StartCooldown(int index)
        {
            EnsureCooldownArray();
            m_cooldownRemaining[index] = m_data.skills[index].cooldown;
        }

        /// <summary>§4-7 후보 선택 — 후보 있으면 균등 랜덤 1개. 없으면 false.</summary>
        public bool TrySelectSkill(float targetDistance, out int skillIndex)
        {
            skillIndex = -1;
            if (SkillCount == 0) return false;
            EnsureCooldownArray();

            // 후보 인덱스 수집 (스킬 수가 적어 임시 리스트 대신 2패스 — GC 0)
            int candidates = 0;
            for (int i = 0; i < SkillCount; i++)
                if (IsCandidate(i, targetDistance)) candidates++;
            if (candidates == 0) return false;

            int pick = Random.Range(0, candidates); // 균등 랜덤
            for (int i = 0; i < SkillCount; i++)
            {
                if (!IsCandidate(i, targetDistance)) continue;
                if (pick-- == 0) { skillIndex = i; return true; }
            }
            return false;
        }

        /// <summary>쿨타임 무시하고 어떤 스킬이든 타겟에 닿는가 — "후보 없음 + 사거리 내 → Exhausted" 판정용.</summary>
        public bool AnySkillReaches(float targetDistance)
        {
            for (int i = 0; i < SkillCount; i++)
                if (Reaches(m_data.skills[i], targetDistance)) return true;
            return false;
        }

        /// <summary>가장 빨리 도는 스킬의 잔여 쿨타임 — Exhausted 지속시간 (§4-5, 런타임 계산).</summary>
        public float ShortestRemainingCooldown()
        {
            EnsureCooldownArray();
            float shortest = float.MaxValue;
            for (int i = 0; i < m_cooldownRemaining.Length; i++)
                shortest = Mathf.Min(shortest, Mathf.Max(0f, m_cooldownRemaining[i]));
            return shortest == float.MaxValue ? 0f : shortest;
        }

        private bool IsCandidate(int index, float targetDistance)
        {
            if (m_cooldownRemaining[index] > 0f) return false;
            SkillItem skill = m_data.skills[index];
            if (targetDistance < skill.minEngage) return false;

            // Melee/Point만 사거리 내 요구 (§4-7). SelfAoe/Ranged는 거리 조건이 minEngage뿐.
            switch (skill.skillType)
            {
                case SkillType.Melee: return targetDistance <= skill.range;
                case SkillType.Point: return targetDistance <= skill.maxCastRange;
                default:              return true;
            }
        }

        // 타입별 유효 도달 거리 — Exhausted 판정용 (쿨타임 무시)
        private static bool Reaches(SkillItem skill, float targetDistance)
        {
            if (targetDistance < skill.minEngage) return false;
            switch (skill.skillType)
            {
                case SkillType.Melee:   return targetDistance <= skill.range;
                case SkillType.Point:   return targetDistance <= skill.maxCastRange;
                case SkillType.SelfAoe: return targetDistance <= skill.aoeRadius;
                case SkillType.Ranged:  return true; // 원거리 — 거리 제한 없음
                default:                return false;
            }
        }

        private void EnsureCooldownArray()
        {
            int count = SkillCount;
            if (m_cooldownRemaining == null || m_cooldownRemaining.Length != count)
                m_cooldownRemaining = new float[count];
        }
    }
}
