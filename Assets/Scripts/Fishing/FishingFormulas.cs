using UnityEngine;

namespace CampLantern.Fishing
{
    /// <summary>
    /// 낚시 수식 모음 (§7). 호출부는 반드시 이 함수들만 경유한다 — 추후 함수 내용만 채우면 되는 구조 (구현 지침 [1]).
    /// - 확정 수식(§7-1 체력, §7-2 텐션, §7-3 도망 간격): 스펙 그대로 구현
    /// - 제안 계수(§7-4 도망 지속, §7-5 비늘털이 확률): 스펙 제안값으로 구현 — 튜닝 대상
    /// - 별도 작성 예정(§9 — XP/코인/미끼가/수리가): 스텁(return 1)
    /// effectiveFishPower = 물고기 힘에 비늘털이 디버프(§7-5)를 반영한 유효 힘 — 호출부(Fish)가 계산해 넘긴다.
    /// </summary>
    public static class FishingFormulas
    {
        /// <summary>§7-1 확정: 흰색 + 트리거 홀드 중 체력 감소량/초 = 낚싯대 힘.
        /// (§7-1은 낚싯대 힘만 사용 — effectiveFishPower는 시그니처 통일용으로 현재 미사용)</summary>
        public static float HealthDecayPerSecond(RodData rod, float effectiveFishPower)
        {
            return rod.power;
        }

        /// <summary>§7-2 확정: 도망(빨강) + 트리거 홀드 중 텐션 감소량/초 = 물고기힘 × 계수(0.25).
        /// 비늘털이 체공 중 홀드는 ×1.5 (§7-5).</summary>
        public static float TensionDecayPerSecond(float effectiveFishPower, bool isShake, FishingTuning tuning)
        {
            float decay = effectiveFishPower * tuning.tensionDecayCoeff;
            return isShake ? decay * tuning.shakeReelingTensionMul : decay;
        }

        /// <summary>§7-3 확정 + 소프트락 보정: 다음 도망까지 간격 = lerp(4, 1, 힘/힘Max) + rand(±0.3) 초.
        /// 간격 기산점은 이전 도망 종료 시점 — 힘이 최대여도 흰색(릴링 기회)이 최소 약 1초 보장된다.</summary>
        public static float NextEscapeInterval(float effectiveFishPower, float powerMax)
        {
            return Mathf.Lerp(4f, 1f, effectiveFishPower / powerMax) + Random.Range(-0.3f, 0.3f);
        }

        /// <summary>§7-4 제안 계수: 도망 지속 = clamp(rand(1,3) × (0.8 + 0.02×힘), 1, 3) 초.
        /// 힘 10일 때 계수 ×1.0 — 힘 스탯의 삼중 중첩 난이도 급증 방지.</summary>
        // TODO(TUNING): 실기 테스트 후 계수 확정 (§9)
        public static float EscapeDuration(float effectiveFishPower)
        {
            return Mathf.Clamp(Random.Range(1f, 3f) * (0.8f + 0.02f * effectiveFishPower), 1f, 3f);
        }

        /// <summary>§7-5: 도망 시도가 비늘털이(점프 QTE)로 대체될 확률.
        /// 기본 고정 20%, 옵션 사용 시 힘 비례(10% + 힘×1%).</summary>
        public static float ShakeChance(float effectiveFishPower, FishingTuning tuning)
        {
            float p = tuning.shakeChanceByPower
                ? 0.10f + effectiveFishPower * 0.01f
                : tuning.shakeChance;
            return Mathf.Clamp01(p);
        }

        // ── §9 미결 항목 — 산출식 미정, 함수 분리만 (구현 지침 [1]) ─────────────

        /// <summary>포획 성공 XP. §7-6: 기능 테스트 단계 고정 지급.</summary>
        public static int RewardXp(FishInstance fish)
        {
            // TODO(FORMULA): XP 산출식 미정 (§9 — 대물 보너스에 fish.T 재사용 예정)
            return 1;
        }

        /// <summary>포획 성공 코인. §7-6: 기능 테스트 단계 고정 지급.</summary>
        public static int RewardCoin(FishInstance fish)
        {
            // TODO(FORMULA): 코인 산출식 미정 (§9)
            return 1;
        }

        /// <summary>미끼(갯지렁이) 1개 가격 — 시간당 코인 밸런스 레버 (§9).</summary>
        public static int BaitPrice()
        {
            // TODO(FORMULA): 미끼 가격 미정 (§9)
            return 1;
        }

        /// <summary>낚싯대 수리 가격 — 내구도 기준, 코인 싱크 (§9).</summary>
        public static int RepairPrice(int durability)
        {
            // TODO(FORMULA): 수리 가격 미정 (§9)
            return 1;
        }
    }
}
