using System;
using CampLantern.UI;
using TMPro;
using UnityEngine;

namespace CampLantern.Fishing
{
    /// <summary>
    /// 획득 결과 UI (§2-5): 물고기 종류·길이·무게·획득 XP·코인 표시 + 확인 버튼으로 닫기.
    /// 표시 전용 — 소유자(하네스)가 Show로 값을 Push한다 (rules/scripts.md UI 초기화 원칙).
    /// 초기 비활성은 소유 매니저가 관리한다 — 자신의 Awake에서 SetActive(false) 금지.
    /// 프리팹은 Tools > Make Assets > Fish Result Panel (FishResultPanelFactory, Resources 소재).
    /// </summary>
    public class FishResultPanel : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI m_title;
        [SerializeField] private TextMeshProUGUI m_body;
        [SerializeField] private VRUIButton m_confirm;

        /// <summary>확인 버튼 — 소유자가 구독해 Hide 등 후처리.</summary>
        public event Action Confirmed;

        /// <summary>결과 표시. displayName이 null이면 fishId로 대체(어종 표시명은 하네스가 FishDef에서 해석).</summary>
        public void Show(FishInstance fish, FishReward reward, string displayName = null)
        {
            if (fish == null) return;

            gameObject.SetActive(true);
            if (m_title != null) m_title.text = "획득!";
            if (m_body != null)
            {
                string name = string.IsNullOrEmpty(displayName) ? fish.Species.fishId : displayName;
                m_body.text = $"{name}\n길이 {fish.Length:F1}cm · 무게 {fish.Weight:F2}kg\nXP +{reward.xp} · 코인 +{reward.coin}";
            }
        }

        public void Hide() => gameObject.SetActive(false);

        private void OnEnable()
        {
            if (m_confirm == null) return;
            m_confirm.Clicked -= OnConfirm; // 중복 구독 방지 (rules/scripts.md)
            m_confirm.Clicked += OnConfirm;
        }

        private void OnDisable()
        {
            if (m_confirm != null) m_confirm.Clicked -= OnConfirm;
        }

        private void OnConfirm() => Confirmed?.Invoke();
    }
}
