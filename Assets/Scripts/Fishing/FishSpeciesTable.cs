namespace CampLantern.Fishing
{
    /// <summary>
    /// 어종 하드코딩 테이블 — 힘·크기가 다른 3종 (구현 지침 [2]: 순한 소형 / 중간 / 힘센 대형).
    /// fishId는 기존 FishDef.Id(fish_crucian/fish_trout/fish_golden_carp)와 일치시켜 획득 시
    /// ContentRegistry로 기존 아이템(아이콘·판매가)을 그대로 재사용한다 (design/fishing-detailed step-08).
    /// </summary>
    public static class FishSpeciesTable
    {
        // §7-3 도망 간격 lerp 분모(힘Max). 최강 어종(황금잉어 18)에 여유를 둔 값.
        public const float PowerMax = 20f; // TODO(TUNING): 실기 테스트 후 확정

        // TODO(DATA): 실제 데이터 시스템(ScriptableObject)으로 교체
        public static readonly FishSpeciesData[] Species =
        {
            // 붕어 — 순한 소형: 도망 뜸함(간격 ~3.4초), 파이팅 짧음(체력 2.3~4.0)
            new FishSpeciesData
            {
                fishId = "fish_crucian", power = 4f,
                sizeMin = 8f,  sizeMax = 15f, lengthMin = 15f, lengthMax = 25f, weightMin = 0.2f, weightMax = 0.6f,
            },
            // 송어 — 중형: 스펙 §7-2 예시 기준점(힘 10 = 텐션10 낚싯대로 실수 예산 4초)
            new FishSpeciesData
            {
                fishId = "fish_trout", power = 10f,
                sizeMin = 15f, sizeMax = 28f, lengthMin = 30f, lengthMax = 50f, weightMin = 1.0f, weightMax = 2.5f,
            },
            // 황금 잉어 — 힘센 대형: 도망 잦음(간격 ~1.3초), 파이팅 김(체력 9.0~14.0) — 위험·시간 양축 최상
            new FishSpeciesData
            {
                fishId = "fish_golden_carp", power = 18f,
                sizeMin = 30f, sizeMax = 50f, lengthMin = 60f, lengthMax = 90f, weightMin = 3.0f, weightMax = 6.0f,
            },
        };
    }
}
