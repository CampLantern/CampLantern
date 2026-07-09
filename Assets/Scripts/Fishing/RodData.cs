using System;

namespace CampLantern.Fishing
{
    /// <summary>
    /// 낚싯대 스탯 (§5-2). UserData/장비 시스템이 아직 없어 plain C#로만 정의 —
    /// 임시 인스턴스 하드코딩은 사용처(FishingRod, step-03)에서 한다 (구현 지침 [2]).
    /// </summary>
    [Serializable]
    public class RodData
    {
        public string rodId;
        public float power;      // 물고기가 끌려오는 속도 = 체력 감소 속도 (§7-1)
        public int durability;   // 챔질 성공마다 -1. 0 도달 시 사용 불가 — 수리대 수리 필요 (§5-2)
        public float length;     // 캐스팅 사거리 — 먼 위치 어종 접근 게이트
        public float tension;    // 낚시 시도마다 초기화되는 줄 내구도 풀의 최대값 (§7-2)
    }
}
