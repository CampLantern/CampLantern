using System.Collections.Generic;
using CampLantern.Networking;
using CampLantern.Networking.Voice;
using Fusion;
using TMPro;
using UnityEngine;

namespace CampLantern.UI
{
    /// <summary>
    /// 세션·음성 소셜 패널 — 접속 상태, 내 마이크 토글, 참가자별 원터치 음소거(로컬).
    /// IMGUI 하네스의 네트워크/음소거 블록의 실사용 VR 대체물 (P0 판정 항목: 음소거 토글).
    ///
    /// 프리팹은 Tools > Make Assets > Social Panel UI 로 생성(SocialPanelFactory, Resources 소재).
    /// 소유자(하네스)가 Bind로 SessionLauncher/VoiceController/PlayerMute를 Push한다.
    /// 참가자 조인/이탈은 저빈도 폴링(0.5s)으로 감지 — 러너 콜백 구독은 후속 과제(P0 최소판).
    /// </summary>
    public class SocialPanel : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI m_statusLabel;
        [SerializeField] private VRUIButton m_micButton;
        [SerializeField] private RectTransform m_rowsContent;   // 참가자 행 컨테이너(VerticalLayoutGroup)
        [SerializeField] private SocialPlayerRow m_rowPrefab;

        private const float k_refreshInterval = 0.5f;

        private SessionLauncher m_launcher;
        private VoiceController m_voice;
        private PlayerMute m_mute;

        private readonly List<SocialPlayerRow> m_rows = new List<SocialPlayerRow>();
        private readonly List<PlayerRef> m_lastPlayers = new List<PlayerRef>();
        private readonly List<PlayerRef> m_playersBuffer = new List<PlayerRef>();
        private float m_nextRefreshAt;

        /// <summary>소유자(하네스)가 참조를 주입. 재바인딩 안전.</summary>
        public void Bind(SessionLauncher launcher, VoiceController voice, PlayerMute mute)
        {
            m_launcher = launcher;
            m_voice    = voice;
            m_mute     = mute;
            RefreshMicLabel();
            RefreshStatus();
            RebuildRows();
        }

        private NetworkRunner Runner => m_launcher != null ? m_launcher.Runner : null;

        private void OnEnable()
        {
            if (m_micButton == null) return;
            m_micButton.Clicked -= OnMicClicked;
            m_micButton.Clicked += OnMicClicked;
        }

        private void OnDisable()
        {
            if (m_micButton != null) m_micButton.Clicked -= OnMicClicked;
        }

        private void OnDestroy()
        {
            foreach (SocialPlayerRow row in m_rows)
                if (row != null) row.MuteToggled -= OnRowMuteToggled;
        }

        private void Update()
        {
            if (Time.time < m_nextRefreshAt) return;
            m_nextRefreshAt = Time.time + k_refreshInterval;

            RefreshStatus();
            if (PlayersChanged()) RebuildRows();
        }

        // ── 마이크 ───────────────────────────────────────────────────

        private void OnMicClicked()
        {
            if (m_voice == null) return;
            m_voice.SetMicEnabled(!m_voice.MicEnabled);
            RefreshMicLabel();
        }

        private void RefreshMicLabel()
        {
            if (m_micButton == null) return;
            bool available = m_voice != null;
            m_micButton.SetInteractable(available);
            m_micButton.SetLabel(!available ? "음성 없음" : m_voice.MicEnabled ? "마이크 끄기" : "마이크 켜기");
        }

        // ── 세션 상태 ────────────────────────────────────────────────

        private void RefreshStatus()
        {
            if (m_statusLabel == null) return;
            NetworkRunner runner = Runner;
            m_statusLabel.text = runner == null || !runner.IsRunning
                ? "미접속"
                : $"{runner.SessionInfo.Name} · {runner.SessionInfo.PlayerCount}명";
        }

        // ── 참가자 행 ────────────────────────────────────────────────

        private bool PlayersChanged()
        {
            m_playersBuffer.Clear();
            NetworkRunner runner = Runner;
            if (runner != null && runner.IsRunning)
                foreach (PlayerRef player in runner.ActivePlayers)
                    m_playersBuffer.Add(player);

            if (m_playersBuffer.Count != m_lastPlayers.Count) return true;
            for (int i = 0; i < m_playersBuffer.Count; i++)
                if (m_playersBuffer[i] != m_lastPlayers[i]) return true;
            return false;
        }

        private void RebuildRows()
        {
            foreach (SocialPlayerRow row in m_rows)
            {
                if (row == null) continue;
                row.MuteToggled -= OnRowMuteToggled;
                Destroy(row.gameObject);
            }
            m_rows.Clear();
            m_lastPlayers.Clear();

            NetworkRunner runner = Runner;
            if (runner == null || !runner.IsRunning || m_rowPrefab == null || m_rowsContent == null) return;

            foreach (PlayerRef player in runner.ActivePlayers)
            {
                m_lastPlayers.Add(player);
                SocialPlayerRow row = Instantiate(m_rowPrefab, m_rowsContent);
                bool isLocal = player == runner.LocalPlayer;
                row.Set(player, isLocal, m_mute != null && m_mute.IsMuted(player));
                if (!isLocal)
                {
                    row.MuteToggled -= OnRowMuteToggled;
                    row.MuteToggled += OnRowMuteToggled;
                }
                m_rows.Add(row);
            }
        }

        private void OnRowMuteToggled(PlayerRef player)
        {
            if (m_mute == null) return;
            bool next = !m_mute.IsMuted(player);
            m_mute.SetMuted(player, next);
            foreach (SocialPlayerRow row in m_rows)
                if (row != null) row.UpdateMuted(player, next);
        }
    }
}
