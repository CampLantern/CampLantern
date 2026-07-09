using System.Collections.Generic;
using CampLantern.Core;
using UnityEngine;

namespace CampLantern.UI
{
    /// <summary>
    /// VR 월드스페이스 인벤토리 패널 — <see cref="Inventory"/>를 바인딩해 아이템을 아이콘+수량 행으로 표시한다.
    /// IMGUI 텍스트 인벤토리(개발용)의 실사용 UI 대체물. 만든 아이템 아이콘(ItemDef.Icon)이 실제로 보이는 지점.
    ///
    /// 프리팹은 Tools > Make Assets > Inventory UI 로 생성(InventoryUIFactory). 소유자가 Bind로 데이터를 주입하고,
    /// Inventory.Changed에 반응해 자동 갱신한다. 구독 해제는 OnDestroy에서(rules/scripts.md).
    /// </summary>
    public class InventoryPanel : MonoBehaviour
    {
        [SerializeField] private RectTransform m_content;   // 행을 담는 영역(VerticalLayoutGroup)
        [SerializeField] private InventoryRow m_rowPrefab;  // 행 프리팹

        private Inventory m_inventory;
        private readonly List<InventoryRow> m_rows = new List<InventoryRow>();

        /// <summary>표시할 인벤토리를 연결. 재바인딩 안전. null이면 비운다.</summary>
        public void Bind(Inventory inventory)
        {
            Unbind();
            m_inventory = inventory;
            if (m_inventory != null)
            {
                m_inventory.Changed += Refresh;
                Refresh();
            }
        }

        private void Unbind()
        {
            if (m_inventory != null) m_inventory.Changed -= Refresh;
            m_inventory = null;
        }

        private void OnDestroy() => Unbind();

        private void Refresh()
        {
            foreach (InventoryRow row in m_rows)
                if (row != null) Destroy(row.gameObject);
            m_rows.Clear();

            if (m_inventory == null || m_rowPrefab == null || m_content == null) return;

            foreach (KeyValuePair<ItemDef, int> entry in m_inventory.Items)
            {
                InventoryRow row = Instantiate(m_rowPrefab, m_content);
                row.Set(entry.Key, entry.Value);
                m_rows.Add(row);
            }
        }
    }
}
