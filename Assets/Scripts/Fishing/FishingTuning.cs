using System;
using UnityEngine;

namespace CampLantern.Fishing
{
    /// <summary>
    /// 낚시 튜닝 값 묶음 — 하드코딩 금지 항목 전부 인스펙터 노출 (구현 지침 [3]).
    /// Fish/FishingRod/FishingRodInput이 [SerializeField]로 소유하고 수식(FishingFormulas)에 넘긴다.
    /// </summary>
    [Serializable]
    public class FishingTuning
    {
        [Tooltip("입질 판정 윈도우(초) — 전 어종 고정 (§2, §9 권장 0.8~1.2)")]
        public float biteWindowSeconds = 1.0f;      // TODO(TUNING): 실기 테스트 후 확정

        [Tooltip("비늘털이 발동 확률 0~1 — 도망 시도를 대체 (§7-5)")]
        public float shakeChance = 0.2f;            // TODO(TUNING): 실기 테스트 후 확정

        [Tooltip("비늘털이 확률 힘 비례 옵션(P = 10% + 힘×1%) 사용 여부 (§7-5)")]
        public bool shakeChanceByPower = false;     // TODO(TUNING): 실기 테스트 후 확정

        [Tooltip("비늘털이 스윙 성공 시 유효 힘 배율 (§7-5: -20% → 0.8)")]
        public float shakeDebuffMul = 0.8f;         // TODO(TUNING): 실기 테스트 후 확정

        [Tooltip("비늘털이 스윙 성공 디버프 지속(초) (§7-5)")]
        public float shakeDebuffSeconds = 1.5f;     // TODO(TUNING): 실기 테스트 후 확정

        [Tooltip("비늘털이 체공 시간 = 스윙 판정 윈도우(초) (§7-5)")]
        public float shakeAirborneSeconds = 1.0f;   // TODO(TUNING): 실기 테스트 후 확정

        [Tooltip("비늘털이 체공 중 릴링 시 텐션 감소 배율 (§7-5: ×1.5)")]
        public float shakeReelingTensionMul = 1.5f; // TODO(TUNING): 실기 테스트 후 확정

        [Tooltip("텐션 감소 계수 — 도망 중 릴링 시 텐션 -= 물고기힘 × 이 값 × Δt (§7-2: 0.25)")]
        public float tensionDecayCoeff = 0.25f;     // TODO(TUNING): 실기 테스트 후 확정

        [Tooltip("스윙 감지 최소 각속도(deg/s) — VR 실기 확인 전 임시값 (§7-5, step-07에서 사용)")]
        public float swingMinAngularSpeed = 180f;   // TODO(TUNING): VR 실기 테스트로 확정 (오탐/미탐 균형)

        [Tooltip("스윙 감지 최소 방향 전환 횟수 (§7-5)")]
        public int swingMinDirChanges = 1;          // TODO(TUNING): VR 실기 테스트로 확정
    }
}
