using UnityEngine;

namespace CampLantern.Combat.Data
{
    /// <summary>
    /// 화살 정의. 무게 W는 초속을 깎고(v0 식) 데미지를 올린다(1 + b×W 보너스).
    /// Spec: 지시서 1단계 (§1-5), 계산식은 §1-4 (step-08)
    /// </summary>
    [CreateAssetMenu(menuName = "CampLantern/Combat/Arrow", fileName = "Arrow_")]
    public class ArrowData : ScriptableObject
    {
        [Tooltip("무게 W")]
        public float weight;

        [Tooltip("가격 — TODO(SPEC): 미정, 임시 1 (지시서 명시)")]
        public int price = 1;
    }
}
