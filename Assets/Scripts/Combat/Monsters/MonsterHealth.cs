using System;
using CampLantern.Combat.Data;
using UnityEngine;

namespace CampLantern.Combat.Monsters
{
    /// <summary>
    /// 몬스터 체력 — IDamageable 구현 (데미지 적용 단일 지점, step-12 네트워크 분리 경계).
    /// 약점 배율 적용 지점: HitInfo.BaseDamage는 배율 미적용 값이고, 여기서
    /// MonsterData.weakpointMultiplier를 곱한다 (README 아키텍처 결정 — 배율 데이터의 소유자가 몬스터).
    /// MaxHp는 step-09 HP 스케일링(§4-2)이 갱신한다.
    /// Spec: 지시서 3단계 (§4-4 — Return 무적, HP 회복)
    /// </summary>
    public class MonsterHealth : MonoBehaviour, IDamageable
    {
        [SerializeField] private MonsterData m_data;

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

        /// <summary>
        /// Editor 팩토리·런타임 조립용 데이터 주입. 런타임 조립은 AddComponent 시 Awake가 먼저 돌아
        /// 데이터가 없으므로 여기서 HP까지 초기화한다 (step-02 MeleeWeapon.Configure와 동일 교훈).
        /// </summary>
        public void Configure(MonsterData data)
        {
            m_data = data;
            MaxHp = data != null ? data.baseHp : 0;
            CurrentHp = MaxHp;
        }

        private void Awake()
        {
            MaxHp = m_data != null ? m_data.baseHp : 0;
            CurrentHp = MaxHp;
        }

        public void ApplyDamage(in HitInfo hit)
        {
            if (!IsDamageable) return;

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
            }
        }

        /// <summary>Return 원위치 도달 시 전량 회복 (§4-4). 사망 후 부활 용도가 아님 — Died 가드는 유지된다.</summary>
        public void ResetToFull()
        {
            CurrentHp = MaxHp;
        }

        /// <summary>Return 상태 무적 토글 — ReturnState.Enter/Exit에서 호출.</summary>
        public void SetInvulnerable(bool invulnerable)
        {
            m_invulnerable = invulnerable;
        }
    }
}
