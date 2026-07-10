using System;
using CampLantern.Combat.Data;
using UnityEngine;

namespace CampLantern.Combat.Player
{
    /// <summary>
    /// 소생 (§2-3) — 다른 플레이어의 손 접촉 + 버튼 홀드 reviveHoldSeconds(3s) → HP reviveHpRatio(절반) 부활.
    /// **소생 중 소생자가 피격돼도 취소 없음** (지시서 명시 — 피격과 홀드는 완전 무관).
    /// 명시적 취소는 버튼 뗌(CancelRevive)과 손 접촉 이탈(스펙에 없어 자연 해석 — 손이 떨어지면 홀드 무의미)뿐.
    ///
    /// 코어는 입력 무관 — TryBeginRevive/CancelRevive 진입점만. VR 버튼 홀드 배선은 어댑터/체크리스트.
    /// 손 접촉 판정은 거리 계산 (판정 규약 — 물리 콜백 금지).
    /// Spec: 지시서 7단계 (§2-3)
    /// </summary>
    public class PlayerReviver : MonoBehaviour
    {
        [SerializeField] private CombatBalanceData m_balance;
        [Tooltip("소생자 손 기준점 — VR 손. 배선 전 null이면 자기 트랜스폼 사용")]
        [SerializeField] private Transform m_handReference;
        [Tooltip("손-다운자 접촉 인정 반경(m). TODO(TUNING)")]
        [SerializeField] private float m_contactRadius = 0.6f;

        private float m_progress;

        public PlayerHealth CurrentTarget { get; private set; }
        public float Progress => CurrentTarget != null && m_balance != null && m_balance.reviveHoldSeconds > 0f
            ? Mathf.Clamp01(m_progress / m_balance.reviveHoldSeconds) : 0f;

        /// <summary>소생 완료 — (소생자, 대상). UI/사운드 훅.</summary>
        public event Action<PlayerReviver, PlayerHealth> ReviveCompleted;

        public void Configure(CombatBalanceData balance, Transform handReference)
        {
            m_balance = balance;
            m_handReference = handReference;
        }

        /// <summary>홀드 시작 — 대상이 다운 상태이고 손이 접촉 반경 내일 때만.</summary>
        public bool TryBeginRevive(PlayerHealth target)
        {
            if (target == null || !target.IsDowned || m_balance == null) return false;
            if (!InContact(target)) return false;

            CurrentTarget = target;
            m_progress = 0f;
            return true;
        }

        /// <summary>버튼 뗌 — 명시적 취소. (소생자 피격은 취소 사유가 아님 — §2-3.)</summary>
        public void CancelRevive()
        {
            CurrentTarget = null;
            m_progress = 0f;
        }

        private void Update()
        {
            if (CurrentTarget == null) return;

            // 대상이 이미 살아났거나(다른 소생자) 손이 떨어지면 홀드 무효
            if (!CurrentTarget.IsDowned || !InContact(CurrentTarget))
            {
                CancelRevive();
                return;
            }

            m_progress += Time.deltaTime;
            if (m_progress >= m_balance.reviveHoldSeconds)
            {
                PlayerHealth target = CurrentTarget;
                target.ReviveTo(m_balance.reviveHpRatio);
                CancelRevive();
                ReviveCompleted?.Invoke(this, target);
            }
        }

        private bool InContact(PlayerHealth target)
        {
            Transform hand = m_handReference != null ? m_handReference : transform;
            return (target.transform.position - hand.position).magnitude <= m_contactRadius;
        }
    }
}
