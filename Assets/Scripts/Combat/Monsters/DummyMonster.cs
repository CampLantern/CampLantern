using CampLantern.Combat;
using UnityEngine;

namespace CampLantern.Combat.Monsters
{
    /// <summary>
    /// 임시 표적 — HP + 피격 로그(속도/데미지/약점 여부)만. **step-03에서 MonsterHealth + MonsterController로 대체 예정.**
    /// 약점 배율 적용 예시 포함: 배율은 수신 측이 곱한다 (HitInfo.BaseDamage는 배율 미적용 — README 아키텍처 결정).
    /// 배율 값도 step-03부터는 MonsterData.weakpointMultiplier 소관 — 여기서는 임시 하드코딩 필드.
    /// Spec: 지시서 2단계 (테스트용 DummyMonster)
    /// </summary>
    public class DummyMonster : MonoBehaviour, IDamageable
    {
        [SerializeField] private int m_maxHp = 100;
        [Tooltip("약점 배율 — 임시. step-03부터 MonsterData.weakpointMultiplier로 대체")]
        [SerializeField] private float m_weakpointMultiplier = 1.5f;

        private int m_currentHp;

        public int CurrentHp => m_currentHp;
        public bool IsDamageable => m_currentHp > 0;

        private void Awake()
        {
            m_currentHp = m_maxHp;
        }

        public void ApplyDamage(in HitInfo hit)
        {
            if (!IsDamageable) return;

            int finalDamage = hit.IsWeakpoint
                ? Mathf.RoundToInt(hit.BaseDamage * m_weakpointMultiplier)
                : hit.BaseDamage;

            m_currentHp = Mathf.Max(0, m_currentHp - finalDamage);

            // 로그 마커는 ASCII — 브릿지 로그 grep용 (unity-editor-automation.md 함정: 한글 mojibake)
            Debug.Log($"[DummyMonster] hit speed={hit.Speed:F2} base={hit.BaseDamage} final={finalDamage} " +
                      $"weakpoint={hit.IsWeakpoint} hp={m_currentHp}/{m_maxHp}");
        }

        /// <summary>테스트 리셋 (검증 봇·하네스용).</summary>
        public void ResetToFull()
        {
            m_currentHp = m_maxHp;
        }
    }
}
