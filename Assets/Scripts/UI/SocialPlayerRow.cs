using System;
using Fusion;
using TMPro;
using UnityEngine;

namespace CampLantern.UI
{
    /// <summary>
    /// 소셜 패널 한 줄 — "P{id}" 라벨 + 음소거 토글 버튼(원격 플레이어만).
    /// 데이터는 소유자(SocialPanel)가 Set으로 Push한다 — 스스로 조회하지 않는다(rules/scripts.md).
    /// </summary>
    public class SocialPlayerRow : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI m_label;
        [SerializeField] private VRUIButton m_muteButton;

        /// <summary>음소거 버튼이 눌렸을 때 — 해당 PlayerRef를 넘긴다. SocialPanel이 구독.</summary>
        public event Action<PlayerRef> MuteToggled;

        private PlayerRef m_player;

        public void Set(PlayerRef player, bool isLocal, bool muted)
        {
            m_player = player;
            if (m_label != null)
                m_label.text = isLocal ? $"P{player.PlayerId} (나)" : $"P{player.PlayerId}";
            if (m_muteButton != null)
            {
                m_muteButton.gameObject.SetActive(!isLocal); // 자기 자신은 음소거 대상 아님
                m_muteButton.SetLabel(muted ? "해제" : "음소거");
            }
        }

        /// <summary>음소거 상태 변경 반영 — 해당 플레이어의 행일 때만.</summary>
        public void UpdateMuted(PlayerRef player, bool muted)
        {
            if (player != m_player || m_muteButton == null) return;
            m_muteButton.SetLabel(muted ? "해제" : "음소거");
        }

        private void OnEnable()
        {
            if (m_muteButton == null) return;
            m_muteButton.Clicked -= HandleClick;
            m_muteButton.Clicked += HandleClick;
        }

        private void OnDisable()
        {
            if (m_muteButton != null) m_muteButton.Clicked -= HandleClick;
        }

        private void HandleClick() => MuteToggled?.Invoke(m_player);
    }
}
