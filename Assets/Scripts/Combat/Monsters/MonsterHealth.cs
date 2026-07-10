using System;
using System.Collections.Generic;
using CampLantern.Combat.Data;
using UnityEngine;

namespace CampLantern.Combat.Monsters
{
    /// <summary>
    /// 몬스터 체력 — IDamageable 구현 (데미지 적용 단일 지점, step-12 네트워크 분리 경계).
    /// 약점 배율 적용 지점: HitInfo.BaseDamage는 배율 미적용 값이고, 여기서
    /// MonsterData.weakpointMultiplier를 곱한다 (README 아키텍처 결정 — 배율 데이터의 소유자가 몬스터).
    ///
    /// [HP 스케일링 §4-2 + 기여 §3 — step-09] 기여자 Set(유효타 1회 이상 = 기여) 관리.
    /// 신규 기여자의 **첫 유효타 시점**에 MaxHp = baseHp × (1 + 계수 × (인원−1))로 갱신하고
    /// 증가분을 잔여 HP에 플랫 가산. **감소 없음**. 사망 시 기여자 목록 이벤트만 발행 —
    /// 보상 지급은 경제/HuntLedger 소관(step-12 통합).
    /// Return 회복(ResetToFull)은 새 교전으로 간주 — 기여·스케일 초기화 (HuntLedger.ResetForNewHunt와 동일 취지).
    /// 기여자 신원은 현 단계 HitInfo.Attacker(무기/봇 루트) — 플레이어 매핑 정밀화는 step-10/12.
    /// Spec: 지시서 3단계 (§4-4) + 7단계 (§4-2, §3)
    /// </summary>
    public class MonsterHealth : MonoBehaviour, IDamageable
    {
        [SerializeField] private MonsterData m_data;
        [SerializeField] private CombatBalanceData m_balance; // hpScalePerExtraPlayer (§4-2)

        private readonly HashSet<GameObject> m_contributors = new HashSet<GameObject>();
        private bool m_invulnerable;
        private bool m_diedFired;

        public MonsterData Data => m_data;
        public int MaxHp { get; private set; }
        public int CurrentHp { get; private set; }

        /// <summary>Return 무적(SetInvulnerable) 또는 사망 시 false — 공격 측은 판정 스킵 가능.</summary>
        public bool IsDamageable => CurrentHp > 0 && !m_invulnerable;

        /// <summary>피격(유효타) 발생 — 약점 여부 포함. step-05 어그로가 구독. 구독자는 OnDestroy/OnDisable에서 해제할 것.</summary>
        public event Action<HitInfo> Damaged;

        /// <summary>HP 0 도달 시 1회 발화. MonsterController가 DeathOverlay 적용에 사용.</summary>
        public event Action Died;

        /// <summary>사냥 성공(사망) 시 기여자 목록 발행 (§3 — 유효타 1회 이상 = 기여). 보상 지급은 구독 측 몫.</summary>
        public event Action<IReadOnlyCollection<GameObject>> HuntSucceeded;

        public IReadOnlyCollection<GameObject> Contributors => m_contributors;

        /// <summary>
        /// Editor 팩토리·런타임 조립용 데이터 주입. 런타임 조립은 AddComponent 시 Awake가 먼저 돌아
        /// 데이터가 없으므로 여기서 HP까지 초기화한다 (step-02 MeleeWeapon.Configure와 동일 교훈).
        /// balance는 HP 스케일링(§4-2) 계수 — 미주입 시 스케일링 없이 동작(기존 봇 호환).
        /// </summary>
        public void Configure(MonsterData data, CombatBalanceData balance = null)
        {
            m_data = data;
            if (balance != null) m_balance = balance;
            MaxHp = data != null ? data.baseHp : 0;
            CurrentHp = MaxHp;
            m_contributors.Clear();
        }

        private void Awake()
        {
            MaxHp = m_data != null ? m_data.baseHp : 0;
            CurrentHp = MaxHp;
        }

        public void ApplyDamage(in HitInfo hit)
        {
            if (!IsDamageable) return;

            RegisterContributor(hit.Attacker); // 신규 기여자의 첫 유효타 시점에 스케일링 (§4-2) — 데미지 적용 전

            float multiplier = hit.IsWeakpoint && m_data != null ? m_data.weakpointMultiplier : 1f;
            int finalDamage = Mathf.RoundToInt(hit.BaseDamage * multiplier);

            CurrentHp = Mathf.Max(0, CurrentHp - finalDamage);

            // ASCII 마커 — 브릿지 로그 grep용
            Debug.Log($"[MonsterHealth] hit speed={hit.Speed:F2} base={hit.BaseDamage} final={finalDamage} " +
                      $"weakpoint={hit.IsWeakpoint} hp={CurrentHp}/{MaxHp} ({name})");

            Damaged?.Invoke(hit);

            if (CurrentHp <= 0 && !m_diedFired)
            {
                m_diedFired = true;
                Died?.Invoke();
                HuntSucceeded?.Invoke(m_contributors); // §3 — 기여자 전원에게 (보상은 구독 측)
            }
        }

        // 기여자 Set 갱신 + HP 스케일링: MaxHp = baseHp × (1 + 계수 × (인원−1)), 증가분 잔여 플랫 가산 (§4-2)
        private void RegisterContributor(GameObject attacker)
        {
            if (attacker == null || !m_contributors.Add(attacker)) return;
            if (m_balance == null || m_data == null) return;

            int newMax = Mathf.RoundToInt(m_data.baseHp * (1f + m_balance.hpScalePerExtraPlayer * (m_contributors.Count - 1)));
            int delta = newMax - MaxHp;
            if (delta <= 0) return; // 감소 없음 (§4-2)

            MaxHp = newMax;
            CurrentHp += delta;
            Debug.Log($"[MonsterHealth] hp scaled: contributors={m_contributors.Count} max={MaxHp} hp={CurrentHp} ({name})");
        }

        /// <summary>
        /// Return 원위치 도달 시 전량 회복 (§4-4). 새 교전으로 간주 — 기여·스케일 초기화
        /// (이전 교전 참여자가 새 교전에 무임 기여로 남는 것 방지, HuntLedger.ResetForNewHunt와 동일 취지).
        /// 사망 후 부활 용도가 아님 — Died 가드는 유지된다.
        /// </summary>
        public void ResetToFull()
        {
            m_contributors.Clear();
            MaxHp = m_data != null ? m_data.baseHp : MaxHp;
            CurrentHp = MaxHp;
        }

        /// <summary>Return 상태 무적 토글 — ReturnState.Enter/Exit에서 호출.</summary>
        public void SetInvulnerable(bool invulnerable)
        {
            m_invulnerable = invulnerable;
        }
    }
}
