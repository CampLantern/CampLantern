using System;
using CampLantern.Combat.Data;
using UnityEngine;

namespace CampLantern.Combat.Weapons
{
    /// <summary>
    /// 화살 소지 관리 + 박힌 화살 회수 (§1-4). 최대 CombatBalanceData.maxArrows(30).
    ///
    /// 회수: 박힌 화살과 손(m_handReference)의 평면 아닌 3D 거리 판정(arrowRetrieveRadius) —
    /// 판정 규약(거리 계산, 물리 콜백 금지). 접촉 인식 즉시: 화살 제거 + 소지 +1 + CountChanged
    /// (화살집 비주얼 갱신 훅). **소지 만땅이면 무반응** — 화살은 박힌 채 유지.
    /// Spec: 지시서 6단계 (§1-4)
    /// </summary>
    public class ArrowQuiver : MonoBehaviour
    {
        [SerializeField] private CombatBalanceData m_balance;
        [Tooltip("회수 판정 기준점 — VR 손. 배선 전엔 null(회수 비활성)")]
        [SerializeField] private Transform m_handReference;

        public int Count { get; private set; }
        public int MaxArrows => m_balance != null ? m_balance.maxArrows : 0;
        public bool IsFull => Count >= MaxArrows;

        /// <summary>소지 수량 변경 — 화살집 비주얼 갱신 훅. 구독 해제 필수.</summary>
        public event Action<int> CountChanged;

        /// <summary>Editor 팩토리·런타임 조립용 — 가득 채워 시작.</summary>
        public void Configure(CombatBalanceData balance, Transform handReference)
        {
            m_balance = balance;
            m_handReference = handReference;
            Count = MaxArrows;
            CountChanged?.Invoke(Count);
        }

        private void Awake()
        {
            if (m_balance != null && Count == 0) Count = MaxArrows;
        }

        /// <summary>발사 시 차감 — Bow.TryFire가 호출.</summary>
        public bool TryConsume()
        {
            if (Count <= 0) return false;
            Count--;
            CountChanged?.Invoke(Count);
            return true;
        }

        private void Update()
        {
            if (m_handReference == null || m_balance == null) return;

            float radius = m_balance.arrowRetrieveRadius;
            var stuck = Arrow.StuckArrows;
            for (int i = stuck.Count - 1; i >= 0; i--)
            {
                Arrow arrow = stuck[i];
                if (arrow == null) continue;
                if ((arrow.transform.position - m_handReference.position).magnitude > radius) continue;

                if (IsFull) continue; // 만땅 — 무반응 (§1-4)

                arrow.RemoveFromWorld();
                Count++;
                CountChanged?.Invoke(Count);
            }
        }
    }
}
