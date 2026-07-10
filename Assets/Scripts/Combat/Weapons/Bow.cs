using System;
using CampLantern.Combat.Data;
using UnityEngine;

namespace CampLantern.Combat.Weapons
{
    /// <summary>
    /// 활 — 당김 기반 발사 (§1-4). 발사 속도 v0 = E(탄력성) × d(당김 비율 0~1) − c × W(화살 무게).
    /// c는 전역 밸런스 상수(CombatBalanceData.bowDrawConstC).
    ///
    /// 코어는 입력 무관 — SetDrawRatio(0~1) 진입점만 노출한다. VR에서는 시위를 잡은 손과 활 그립 간
    /// 거리로 d를 산출해 주입 (양손 인터랙션 배선은 수동 체크리스트/후속 — README 아키텍처 결정).
    /// 화살 소지 검사·차감은 ArrowQuiver 소관 (Quiver 미주입 시 발사 불가).
    /// 발사 1회당 활 내구도 -1 (§1-4). 내구도 0 → WeaponBroken (홀스터 귀환은 step-10).
    /// Spec: 지시서 6단계 (§1-4)
    /// </summary>
    public class Bow : MonoBehaviour
    {
        [SerializeField] private BowData m_data;
        [SerializeField] private CombatBalanceData m_balance;
        [Tooltip("Arrow 컴포넌트를 가진 화살 원본 (프리팹/템플릿)")]
        [SerializeField] private GameObject m_arrowPrefab;
        [Tooltip("발사 기준점 — 위치·전방(forward)")]
        [SerializeField] private Transform m_arrowOrigin;

        public BowData Data => m_data;
        public int CurrentDurability { get; private set; }
        public bool IsBroken => m_data != null && m_data.maxDurability > 0 && CurrentDurability <= 0;
        public float DrawRatio { get; private set; }

        /// <summary>화살 소지 관리 — 런타임 주입 (하네스/봇, VR 화살집 배선은 후속).</summary>
        public ArrowQuiver Quiver { get; set; }

        /// <summary>공격 주체 루트 — 기본은 활 자신. step-10/12에서 플레이어로 설정.</summary>
        public GameObject Attacker { get; set; }

        /// <summary>내구도 0 도달 — step-10 홀스터 귀환 구독 지점.</summary>
        public event Action<Bow> WeaponBroken;

        /// <summary>발사 성공 — (활, 화살, v0). 햅틱/사운드/시위 비주얼 훅.</summary>
        public event Action<Bow, Arrow, float> Fired;

        /// <summary>Editor 팩토리·런타임 조립용 (내구도 포함 초기화 — step-02 Configure 교훈).</summary>
        public void Configure(BowData data, CombatBalanceData balance, GameObject arrowPrefab, Transform arrowOrigin)
        {
            m_data = data;
            m_balance = balance;
            m_arrowPrefab = arrowPrefab;
            m_arrowOrigin = arrowOrigin;
            if (m_data != null) CurrentDurability = m_data.maxDurability;
        }

        private void Awake()
        {
            if (Attacker == null) Attacker = gameObject;
            if (m_data != null) CurrentDurability = m_data.maxDurability;
        }

        /// <summary>당김 비율 주입 (0~1) — 입력 무관 진입점.</summary>
        public void SetDrawRatio(float ratio)
        {
            DrawRatio = Mathf.Clamp01(ratio);
        }

        /// <summary>발사 — 소지 0/파손/미배선이면 false. 성공 시 소지 -1, 내구도 -1.</summary>
        public bool TryFire()
        {
            if (IsBroken || m_data == null || m_balance == null || m_arrowPrefab == null || m_arrowOrigin == null)
                return false;
            if (Quiver == null || !Quiver.TryConsume())
                return false; // 화살 소지 없음 (§1-4 발사 시 차감)

            var arrowGo = Instantiate(m_arrowPrefab);
            arrowGo.SetActive(true); // 템플릿이 비활성이어도 동작 (봇 런타임 조립)
            var arrow = arrowGo.GetComponent<Arrow>();

            float weight = arrow.Data != null ? arrow.Data.weight : 0f;
            float v0 = Mathf.Max(0.1f, m_data.elasticity * DrawRatio - m_balance.bowDrawConstC * weight);

            arrow.Launch(m_arrowOrigin.position, m_arrowOrigin.forward, v0, Attacker);
            Fired?.Invoke(this, arrow, v0);

            ConsumeDurability();
            DrawRatio = 0f; // 시위 복귀
            return true;
        }

        private void ConsumeDurability()
        {
            if (m_data.maxDurability <= 0) return;
            CurrentDurability--;
            if (CurrentDurability <= 0)
            {
                CurrentDurability = 0;
                Debug.Log($"[Bow] broken — {name}");
                WeaponBroken?.Invoke(this);
            }
        }
    }
}
