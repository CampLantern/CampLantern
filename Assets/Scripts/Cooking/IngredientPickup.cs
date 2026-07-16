using CampLantern.Core;
using UnityEngine;

namespace CampLantern.Cooking
{
    /// <summary>
    /// 물리 재료 프록시 — 인벤토리 아이템의 "손에 잡히는" 대리물.
    /// 스폰 시점에는 인벤토리를 차감하지 않는다(인벤토리가 항상 권위) — 냄비 투입 시점에
    /// PotInteractionZone이 CookingPot.TryAddIngredient로 차감하고, 재고가 없으면 튕겨낸다.
    /// 따라서 프록시가 여럿 굴러다녀도 복제 익스플로잇은 성립하지 않는다.
    /// 프리팹(그랩 배선 포함)은 Tools > Make Assets > Physical Cooking, 스폰은 IngredientShelf.
    /// </summary>
    public class IngredientPickup : MonoBehaviour
    {
        [Tooltip("등급 틴트 대상 렌더러 (프리팹 팩토리가 배선)")]
        [SerializeField] private Renderer m_visual;

        [Tooltip("아이템 아이콘 표시 (ItemDef.Icon 미할당이면 숨김)")]
        [SerializeField] private SpriteRenderer m_icon;

        private const float k_killY = -1f; // 낙사 정리 기준 높이

        private static readonly int k_baseColorId = Shader.PropertyToID("_BaseColor");

        private ItemDef m_item;

        /// <summary>이 프록시가 대리하는 아이템 정의.</summary>
        public ItemDef Item => m_item;

        /// <summary>스폰 직후 선반이 호출한다 (Push 초기화 — rules/scripts.md).</summary>
        public void Initialize(ItemDef item)
        {
            m_item = item;
            name = $"Ingredient_{item.Id}";

            if (m_icon != null)
            {
                m_icon.sprite = item.Icon;
                m_icon.enabled = item.Icon != null;
            }

            if (m_visual != null)
            {
                // 머티리얼 인스턴스 누수 없이 등급 색만 덮어쓴다 (URP Lit _BaseColor)
                var mpb = new MaterialPropertyBlock();
                mpb.SetColor(k_baseColorId, RarityColor(item.Rarity));
                m_visual.SetPropertyBlock(mpb);
            }
        }

        private static Color RarityColor(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Rare: return new Color(0.31f, 0.64f, 1f);   // 파랑
                case Rarity.Epic: return new Color(0.69f, 0.36f, 1f);   // 보라
                default:          return new Color(0.85f, 0.85f, 0.82f); // 일반 — 밝은 회백
            }
        }

        private void Update()
        {
            // 낙사 정리 — 프록시일 뿐이라 파괴해도 아이템은 사라지지 않는다(인벤토리 불변). 선반이 다시 채운다.
            if (transform.position.y < k_killY)
                Destroy(gameObject);
        }
    }
}
