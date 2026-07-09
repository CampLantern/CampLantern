using UnityEngine;

namespace CampLantern.Fishing
{
    /// <summary>
    /// 캐스팅마다 생성되는 물고기 개체 (§5-3 공유 품질 롤).
    /// 크기·길이·무게를 독립 롤하지 않고 롤 하나(t)를 공유한다 — 큰 실루엣 = 긴 개체 = 높은 체력이
    /// 자동 보장되어, 캐스팅 전 실루엣 정보가 파이팅 난이도의 정직한 예고가 된다.
    /// t는 추후 보상 산출식("대물 보너스")에 그대로 재사용한다 (§5-3).
    /// </summary>
    public class FishInstance
    {
        public FishSpeciesData Species { get; }
        public float T { get; }          // 품질 롤(0~1) — 대물 보너스 입력으로 보존
        public float Size { get; }       // lerp(sizeMin, sizeMax, t) — 실루엣 스케일
        public float Length { get; }     // lerp(lengthMin, lengthMax, t) — 획득 UI 표기
        public float Weight { get; }     // lerp(weightMin, weightMax, t) — 획득 UI 표기
        public float MaxHealth { get; }  // 크기 × 0.1 + 길이 × 0.1 (§5-3, §7-1)

        private FishInstance(FishSpeciesData species, float t)
        {
            Species   = species;
            T         = t;
            Size      = Mathf.Lerp(species.sizeMin, species.sizeMax, t);
            Length    = Mathf.Lerp(species.lengthMin, species.lengthMax, t);
            Weight    = Mathf.Lerp(species.weightMin, species.weightMax, t);
            MaxHealth = Size * 0.1f + Length * 0.1f;
        }

        /// <summary>개체 생성. t는 품질 롤 1회(보통 Random.value) — 크기/길이/무게/체력이 전부 이 하나에서 파생된다.</summary>
        public static FishInstance Roll(FishSpeciesData species, float t)
        {
            return new FishInstance(species, Mathf.Clamp01(t));
        }
    }
}
