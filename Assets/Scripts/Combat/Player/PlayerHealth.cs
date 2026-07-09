using System;
using UnityEngine;

namespace CampLantern.Combat.Player
{
    /// <summary>
    /// 플레이어 체력 — **최소판** (지시서 5단계: HP + 피격 로그).
    /// 다운(HP 0 → 행동 불능·피격 면제)/소생/전멸/디버프 훅은 **step-09에서 확장** (§2) — 여기서는
    /// HP가 0이 되어도 사망 처리 없이 IsDamageable=false로 추가 피격만 막는다.
    /// 몬스터 스킬 데미지는 배율 없이 BaseDamage 그대로 (플레이어에겐 약점 개념 없음).
    /// Spec: 지시서 5단계 (임시 PlayerHealth)
    /// </summary>
    public class PlayerHealth : MonoBehaviour, IDamageable
    {
        [Tooltip("최대 HP — §2 스펙 수치 미정. TODO(TUNING)")]
        [SerializeField] private int m_maxHp = 100;

        public int MaxHp => m_maxHp;
        public int CurrentHp { get; private set; }

        /// <summary>step-09에서 다운 상태(피격 면제)로 확장 예정 — 현재는 HP 0이면 추가 피격 무시.</summary>
        public bool IsDamageable => CurrentHp > 0;

        /// <summary>피격 발생 — 검증 봇·UI 훅. 구독자는 OnDestroy/OnDisable에서 해제할 것.</summary>
        public event Action<HitInfo> Damaged;

        private void Awake()
        {
            CurrentHp = m_maxHp;
        }

        /// <summary>런타임 조립(봇/하네스) 대응 — Awake 이후 주입 시 HP 초기화 (step-02 Configure 교훈).</summary>
        public void Configure(int maxHp)
        {
            m_maxHp = maxHp;
            CurrentHp = maxHp;
        }

        public void ApplyDamage(in HitInfo hit)
        {
            if (!IsDamageable) return;

            CurrentHp = Mathf.Max(0, CurrentHp - hit.BaseDamage);
            Debug.Log($"[PlayerHealth] hit damage={hit.BaseDamage} hp={CurrentHp}/{m_maxHp} " +
                      $"attacker={(hit.Attacker != null ? hit.Attacker.name : "(null)")} ({name})");
            Damaged?.Invoke(hit);
        }

        public void ResetToFull()
        {
            CurrentHp = m_maxHp;
        }
    }
}
