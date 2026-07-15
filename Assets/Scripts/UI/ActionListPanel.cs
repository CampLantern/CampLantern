using System;
using System.Collections.Generic;
using UnityEngine;

namespace CampLantern.UI
{
    /// <summary>
    /// 범용 액션 리스트 패널 — "라벨 + 버튼(최대 2)" 행의 세로 목록. IMGUI 하네스의
    /// 상점·배치·요리·판매·수리 블록들을 공통으로 대체하는 실사용 VR UI 토대.
    ///
    /// 프리팹은 Tools > Make Assets > Action Panel UI 로 생성(ActionPanelFactory, Resources 소재).
    /// 소유자(하네스)가 SetTitle/SetRows로 내용을 Push하고, 상태가 변할 때마다 SetRows로 전체 재구성한다
    /// (행 수가 적어 P0에선 diff 갱신 불필요).
    /// </summary>
    public class ActionListPanel : MonoBehaviour
    {
        /// <summary>행 명세 — 버튼 라벨이 null이면 해당 버튼은 숨김.</summary>
        public struct RowSpec
        {
            public string label;
            public string button1;
            public Action onButton1;
            public string button2;
            public Action onButton2;
        }

        [SerializeField] private VRUIPanel m_panel;
        [SerializeField] private RectTransform m_rowsContent; // VerticalLayoutGroup
        [SerializeField] private ActionRow m_rowPrefab;

        private readonly List<ActionRow> m_rows = new List<ActionRow>();

        public void SetTitle(string title)
        {
            if (m_panel != null) m_panel.SetTitle(title);
        }

        /// <summary>행 전체 재구성 — 소유자가 상태 변화 시마다 호출.</summary>
        public void SetRows(IReadOnlyList<RowSpec> specs)
        {
            foreach (ActionRow row in m_rows)
                if (row != null) Destroy(row.gameObject);
            m_rows.Clear();

            if (specs == null || m_rowPrefab == null || m_rowsContent == null) return;

            foreach (RowSpec spec in specs)
            {
                ActionRow row = Instantiate(m_rowPrefab, m_rowsContent);
                row.Set(spec);
                m_rows.Add(row);
            }
        }
    }
}
