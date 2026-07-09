using System.Collections.Generic;
using CampLantern.Combat.Data;
using UnityEngine;

namespace CampLantern.Combat.Monsters
{
    /// <summary>
    /// 몬스터 스킬 판정 실행기 — EnableHitCheck()/DisableHitCheck() 캡슐화 (§4-6, hit window 방식).
    /// 활성 구간 동안 매 틱 능동 검사. 애니메이션 이벤트 사용 금지 — 타이밍은 전부 데이터(hit window).
    ///
    /// [Melee 기법 — 전방 부채꼴 거리·각도 수학 판정 (지시서 권장안 채택)]
    /// 근거: 판정 규약(물리 콜백 금지) 하에서 플레이어는 소수(CombatPlayers 레지스트리)라
    /// 목록 순회 + 평면 거리·각도 계산이 콜라이더 쿼리보다 싸고(할당 0) 결정론적이다.
    /// 시전당 플레이어별 최대 1회 — Enable 시 피격 집합 초기화.
    ///
    /// Point/SelfAoe/Ranged는 골격만 (§4-6): 이들은 hitWindowStart==End 단일 시점 판정 —
    /// Point/SelfAoe는 폭발 시점 범위 내 거리 판정, 발사체는 이동 경로 기반 판정 예정. TODO 참조.
    /// Spec: 지시서 5단계 (§4-6)
    /// </summary>
    public class SkillHitCheck : MonoBehaviour
    {
        private readonly HashSet<Transform> m_hitThisCast = new HashSet<Transform>();

        private SkillItem m_activeSkill;
        private GameObject m_attacker;
        private bool m_enabled;

        public bool IsEnabled => m_enabled;

        /// <summary>판정 활성화 — Attack 타이머 경과 비율이 hitWindowStart 도달 시 호출. 피격 집합 초기화(시전당 1회 규칙).</summary>
        public void EnableHitCheck(SkillItem skill, GameObject attacker)
        {
            m_activeSkill = skill;
            m_attacker = attacker;
            m_hitThisCast.Clear();
            m_enabled = true;
        }

        /// <summary>판정 비활성화 — hitWindowEnd 도달 시, 그리고 Attack Exit에서 **무조건** 호출 (§4-6 안전장치).</summary>
        public void DisableHitCheck()
        {
            m_enabled = false;
            m_activeSkill = null;
            m_attacker = null;
        }

        private void Update()
        {
            if (!m_enabled || m_activeSkill == null) return;

            switch (m_activeSkill.skillType)
            {
                case SkillType.Melee:
                    TickMeleeArc();
                    break;
                case SkillType.Point:
                case SkillType.SelfAoe:
                    // TODO(§4-6): 단일 시점 폭발 — 기준 지점(Point: 시전 좌표 / SelfAoe: 자신) 중심
                    // aoeRadius 내 플레이어 거리 판정. Ranged 발사체와 함께 후속 단계에서 실구현.
                    Debug.LogWarning($"[SkillHitCheck] {m_activeSkill.skillType} 판정 미구현 (골격) — {name}");
                    DisableHitCheck();
                    break;
                case SkillType.Ranged:
                    // TODO(§4-6): 발사체 = 이동 경로 기반 판정(화살 step-08과 동일 계열),
                    // 레이저 = castHeight/beamWidth 빔 판정. 후속 단계에서 실구현.
                    Debug.LogWarning($"[SkillHitCheck] Ranged 판정 미구현 (골격) — {name}");
                    DisableHitCheck();
                    break;
            }
        }

        // 전방 부채꼴: 평면 거리 ≤ range AND 전방 각도 ≤ arcAngle/2
        private void TickMeleeArc()
        {
            IReadOnlyList<Transform> players = CombatPlayers.All;
            for (int i = 0; i < players.Count; i++)
            {
                Transform player = players[i];
                if (player == null || m_hitThisCast.Contains(player)) continue;

                Vector3 to = player.position - transform.position;
                to.y = 0f;
                float dist = to.magnitude;
                if (dist > m_activeSkill.range) continue;

                Vector3 forward = transform.forward;
                forward.y = 0f;
                if (dist > 1e-4f && Vector3.Angle(forward, to) > m_activeSkill.arcAngle * 0.5f) continue;

                m_hitThisCast.Add(player); // 시전당 플레이어별 최대 1회 — 피격 여부와 무관하게 판정 소모

                var damageable = player.GetComponentInParent<IDamageable>();
                if (damageable == null || !damageable.IsDamageable) continue;

                var hit = new HitInfo
                {
                    BaseDamage = m_activeSkill.damage,
                    IsWeakpoint = false,             // 플레이어에겐 약점 개념 없음
                    Speed = 0f,
                    Point = player.position,
                    Attacker = gameObject,
                };
                damageable.ApplyDamage(in hit);      // 데미지 적용 단일 지점 유지 (지시서 규칙 5)
            }
        }
    }
}
