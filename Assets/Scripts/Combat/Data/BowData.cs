using UnityEngine;

namespace CampLantern.Combat.Data
{
    /// <summary>
    /// 활 정의. 발사 속도 v0 = elasticity(E) × 당김 비율(d, 0~1) − c × 화살 무게(W).
    /// c는 전역 상수 — CombatBalanceData.bowDrawConstC.
    /// Spec: 지시서 1단계 (§1-5), 계산식은 §1-4 (step-08)
    /// </summary>
    [CreateAssetMenu(menuName = "CampLantern/Combat/Bow", fileName = "Bow_")]
    public class BowData : ScriptableObject
    {
        [Tooltip("탄력성 E (최대값) — 풀 드로우 시 기본 초속")]
        public float elasticity;

        [Tooltip("내구도 최대치 — 발사 1회당 -1")]
        public int maxDurability;
    }
}
