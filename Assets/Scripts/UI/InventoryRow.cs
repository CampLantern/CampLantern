using CampLantern.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CampLantern.UI
{
    /// <summary>
    /// 인벤토리 한 줄 — 아이템 아이콘(Sprite) + "이름 x수량" 라벨.
    /// 데이터는 소유자(InventoryPanel)가 Set으로 Push한다 — 스스로 조회하지 않는다(rules/scripts.md).
    /// </summary>
    public class InventoryRow : MonoBehaviour
    {
        [SerializeField] private Image m_icon;
        [SerializeField] private TextMeshProUGUI m_label;

        public void Set(ItemDef item, int count)
        {
            if (m_icon != null)
            {
                m_icon.sprite  = item != null ? item.Icon : null;
                m_icon.enabled = m_icon.sprite != null; // 아이콘 없으면 빈 칸 대신 숨김
            }
            if (m_label != null)
                m_label.text = item != null ? $"{item.DisplayName}  x{count}" : string.Empty;
        }

        private void Awake()
        {
            // 코드로 참조 확정 (rules/scripts.md) — 팩토리가 배선하지만 누락 대비.
            if (m_icon == null)  m_icon  = GetComponentInChildren<Image>(true);
            if (m_label == null) m_label = GetComponentInChildren<TextMeshProUGUI>(true);
        }
    }
}
