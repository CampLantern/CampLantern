using UnityEngine;

namespace CampLantern.Combat.Data
{
    /// <summary>
    /// 근접 무기 정의 (검/창). 데미지 = clamp(속도, vMin, vMax) × damageCoeff × damageConst.
    /// Spec: 지시서 1단계 (§1-3), 판정 규칙은 §1-2 (step-02)
    /// </summary>
    [CreateAssetMenu(menuName = "CampLantern/Combat/Melee Weapon", fileName = "Weapon_")]
    public class MeleeWeaponData : ScriptableObject
    {
        [Tooltip("홀스터 슬롯 허용 검사용 (§1-6)")]
        public WeaponKind kind = WeaponKind.Sword;

        [Tooltip("데미지 계수")]
        public float damageCoeff;

        [Tooltip("데미지 상수 — 지시서의 ×0.25f. 상수도 데이터로 (지시서 명시). TODO(TUNING)")]
        public float damageConst = 0.25f;

        [Tooltip("유효타 최소 속도(m/s) — 미달이면 무효타 (데미지 0, 피드백 훅 미호출)")]
        public float vMin;

        [Tooltip("데미지 계산 속도 상한(m/s)")]
        public float vMax;

        [Tooltip("내구도 최대치 — 유효타 1회당 -1, 0이면 WeaponBroken")]
        public int maxDurability;

        [Tooltip("수리 단가 — TODO(SPEC): 미정, 임시 0 (지시서 명시)")]
        public int repairCost = 0;
    }
}
