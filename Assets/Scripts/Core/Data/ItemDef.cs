using UnityEngine;

namespace CampLantern.Core
{
    /// <summary>
    /// 판매 가능한 모든 것의 기반 정의 (물고기·재료·요리 공통).
    /// 콘텐츠 데이터는 전부 ScriptableObject로 정의하고, .asset 생성은 Editor 스크립트로만 한다 (RULE-02).
    /// </summary>
    [CreateAssetMenu(menuName = "CampLantern/Item", fileName = "Item_")]
    public class ItemDef : ScriptableObject
    {
        [Tooltip("안정 식별자 — 저장/네트워크 동기화 키. 에셋 이름과 별개이며 변경 금지.")]
        public string Id;

        public string DisplayName;

        public Rarity Rarity;

        [Tooltip("상점 판매 시 받는 코인 — 재화 소스 (domain/economy.md)")]
        public int SellPrice;

        [Tooltip("인벤토리·상점 UI 아이콘. ItemIconFactory가 SVG로 생성·배선(Id 매칭). 미할당이면 텍스트만 표시.")]
        public Sprite Icon;
    }
}
