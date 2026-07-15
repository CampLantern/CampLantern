using System;
using TMPro;
using UnityEngine;

namespace CampLantern.UI
{
    /// <summary>
    /// 액션 리스트 한 줄 — 라벨 + 버튼 최대 2개(라벨이 null이면 숨김).
    /// 데이터는 소유자(ActionListPanel)가 Set으로 Push한다 — 스스로 조회하지 않는다(rules/scripts.md).
    /// </summary>
    public class ActionRow : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI m_label;
        [SerializeField] private VRUIButton m_button1;
        [SerializeField] private VRUIButton m_button2;

        private Action m_action1;
        private Action m_action2;

        public void Set(ActionListPanel.RowSpec spec)
        {
            if (m_label != null) m_label.text = spec.label ?? string.Empty;

            Configure(m_button1, spec.button1, spec.onButton1, out m_action1);
            Configure(m_button2, spec.button2, spec.onButton2, out m_action2);
        }

        private static void Configure(VRUIButton button, string label, Action action, out Action stored)
        {
            stored = action;
            if (button == null) return;
            bool visible = !string.IsNullOrEmpty(label);
            button.gameObject.SetActive(visible);
            if (visible) button.SetLabel(label);
        }

        private void OnEnable()
        {
            if (m_button1 != null) { m_button1.Clicked -= OnClick1; m_button1.Clicked += OnClick1; }
            if (m_button2 != null) { m_button2.Clicked -= OnClick2; m_button2.Clicked += OnClick2; }
        }

        private void OnDisable()
        {
            if (m_button1 != null) m_button1.Clicked -= OnClick1;
            if (m_button2 != null) m_button2.Clicked -= OnClick2;
        }

        private void OnClick1() => m_action1?.Invoke();
        private void OnClick2() => m_action2?.Invoke();
    }
}
