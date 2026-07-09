using UnityEngine;

namespace CampLantern.Combat.Data
{
    /// <summary>
    /// 전투 전역 밸런스 상수 — 특정 무기/몬스터에 속하지 않는 계수 모음.
    /// 수치 튜닝은 코드가 아니라 이 에셋에서만 (지시서 규칙 4·운영 팁).
    /// Spec: 지시서 1단계, 소비처 — 활/화살 step-08 (§1-4), HP 스케일링 step-09 (§4-2), 소생 step-09 (§2-3)
    /// </summary>
    [CreateAssetMenu(menuName = "CampLantern/Combat/Balance", fileName = "CombatBalance")]
    public class CombatBalanceData : ScriptableObject
    {
        [Header("활/화살 (§1-4)")]
        [Tooltip("v0 = E×d − c×W 의 c. TODO(TUNING)")]
        public float bowDrawConstC;

        [Tooltip("화살 데미지 계수 K — clamp(v, vMin, vMax) × K × (1 + b×W). TODO(TUNING)")]
        public float arrowDamageK;

        [Tooltip("무게 보너스 계수 b. TODO(TUNING)")]
        public float arrowWeightBonusB;

        [Tooltip("화살 데미지 계산 속도 하한(m/s). TODO(TUNING)")]
        public float arrowVMin;

        [Tooltip("화살 데미지 계산 속도 상한(m/s). TODO(TUNING)")]
        public float arrowVMax;

        [Tooltip("화살 최대 소지 수")]
        public int maxArrows = 30;

        [Tooltip("박힌 화살 회수 상호작용 반경(m). TODO(TUNING)")]
        public float arrowRetrieveRadius;

        [Header("HP 스케일링 (§4-2)")]
        [Tooltip("MaxHp = baseHp × (1 + 계수 × (기여 인원 − 1)). 증가분은 잔여 HP에 플랫 가산, 감소 없음")]
        public float hpScalePerExtraPlayer = 0.5f;

        [Header("다운/소생 (§2)")]
        [Tooltip("소생 버튼 홀드 시간(초)")]
        public float reviveHoldSeconds = 3f;

        [Tooltip("부활 시 HP 비율")]
        [Range(0f, 1f)] public float reviveHpRatio = 0.5f;
    }
}
