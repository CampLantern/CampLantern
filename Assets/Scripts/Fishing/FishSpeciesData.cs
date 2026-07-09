using System;

namespace CampLantern.Fishing
{
    /// <summary>
    /// 어종 정적 스탯 (낚시 지시서 §5-1). 체력은 테이블 필드가 아니라 크기·길이 파생 계산값(§7-1)
    /// — 개체 생성 시 <see cref="FishInstance"/>가 계산한다.
    /// 아직 데이터 시스템이 없어 plain C#로 정의하고 <see cref="FishSpeciesTable"/>에 하드코딩한다.
    /// 추후 ScriptableObject 이관이 쉽도록 public 필드 구조 유지 (구현 지침 [2]).
    /// </summary>
    [Serializable]
    public class FishSpeciesData
    {
        public string fishId;               // 기존 FishDef.Id와 일치 — 획득 시 ContentRegistry 조회 키 (step-08 연결 고리)
        public float power;                 // 도망 시도 빈도·지속시간, 텐션 감소율에 관여 — 위험 축 (§6)
        public float sizeMin, sizeMax;      // 개체 크기 롤 범위. 실루엣 스케일 반영
        public float lengthMin, lengthMax;  // 개체 길이 롤 범위. 획득 UI 표기값
        public float weightMin, weightMax;  // 개체 무게 롤 범위. 획득 UI 표기값, 추후 보상 산출 입력
    }
}
