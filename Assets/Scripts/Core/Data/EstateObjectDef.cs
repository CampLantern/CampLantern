using UnityEngine;

namespace CampLantern.Core
{
    /// <summary>
    /// 영지 배치 오브젝트 정의. 오브젝트는 제작이 아닌 구매로만 획득한다 (domain/estate-system.md).
    /// 이중 재화: 일반 등급은 코인만, 희귀 이상은 코인 + 지정 재료 (domain/economy.md).
    /// </summary>
    [CreateAssetMenu(menuName = "CampLantern/EstateObject", fileName = "Estate_")]
    public class EstateObjectDef : ScriptableObject
    {
        [Tooltip("안정 식별자 — 변경 금지")]
        public string Id;

        public string DisplayName;

        public Rarity Rarity;

        [Tooltip("구매 코인 가격")]
        public int CoinCost;

        [Tooltip("희귀 이상만 사용 — Common은 null. 재료는 코인으로 대체 구매 불가")]
        public ItemDef RequiredMaterial;

        public int RequiredMaterialCount;

        [Tooltip("캠프 수용량 가중치 — 렌더링·물리 비용 환산값. 0 금지 (성능 상한 안전장치)")]
        public int CapacityWeight = 1;

        [Tooltip("배치 시 인스턴스화되는 3D 비주얼(플레이스홀더 프리미티브). 실제 모델로 교체 대상.")]
        public GameObject Prefab;

        [Tooltip("상점 UI 아이콘. ItemIconFactory가 SVG로 생성·배선(Id 매칭).")]
        public Sprite Icon;
    }
}
