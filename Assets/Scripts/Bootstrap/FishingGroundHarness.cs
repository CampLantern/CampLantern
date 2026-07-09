using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CampLantern.Core;
using CampLantern.Core.Persistence;
using CampLantern.Fishing;
using CampLantern.Networking;
using CampLantern.Networking.Voice;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CampLantern.Bootstrap
{
    /// <summary>
    /// 낚시터 — 싱글/멀티 선택, 상시 열림, 파티 구성 없이 드롭인 (room-architecture.md).
    /// P0는 실제 매칭/샤딩 없이 고정 샤드 이름("fishing_{m_shardId}")으로만 합류한다 —
    /// "정원 도달 시 새 인스턴스로 샤딩"은 매칭 백엔드가 필요해 P0 범위 밖.
    /// 인벤토리는 PlayerState를 통해 로컬 JSON에 저장/복원된다 — 다른 공간(영지 등)과 이어짐
    /// (오프라인 방문 같은 멀티유저 공유는 여전히 서버 필요, tech-stack-decisions.md).
    /// </summary>
    public class FishingGroundHarness : MonoBehaviour
    {
        [SerializeField] private FishingRod m_rod;
        [SerializeField] private FishingSpot m_spot;
        [SerializeField] private SessionLauncher m_launcher;
        [SerializeField] private string m_lobbySceneName = "Lobby";
        [SerializeField] private string m_shardId = "shard0";

        private PlayerState m_state;
        private ContentRegistry m_registry;
        private bool m_joining;
        private string m_lastLog = "-";

        private VoiceController m_voice;
        private PlayerMute m_mute;

        private void Awake()
        {
            m_registry = Resources.Load<ContentRegistry>("ContentRegistry");
            if (m_registry == null)
                Debug.LogError("[FishingGroundHarness] ContentRegistry 없음 — Tools > Make Assets > Content Registry 실행 필요");

            m_state = new PlayerState();
            if (m_registry != null) m_state.Load(m_registry);

            // 근접 음성 — Network 오브젝트(SessionLauncher와 같은 GO)에 배선됨. HuntZone과 동일 패턴.
            m_voice = m_launcher.GetComponent<VoiceController>();
            m_mute  = m_launcher.GetComponent<PlayerMute>();

            m_rod.FishCaught -= OnFishCaught;
            m_rod.FishCaught += OnFishCaught;
        }

        private void OnDestroy()
        {
            m_rod.FishCaught -= OnFishCaught;
        }

        private void OnApplicationQuit()
        {
            m_state.Save(); // 낚시터는 EstateManager가 없으므로 배치 목록은 디스크 값 그대로 보존됨
        }

        private void OnFishCaught(FishDef fish)
        {
            m_state.Inventory.Add(fish);
            m_lastLog = $"낚음: {fish.DisplayName}";
        }

        private void ReturnToLobby()
        {
            m_state.Save();
            SceneManager.LoadScene(m_lobbySceneName);
        }

        private async Task JoinAsync()
        {
            m_joining = true;
            try
            {
                await m_launcher.StartSession($"fishing_{m_shardId}", destroyCancellationToken);
                m_lastLog = "낚시터 접속 완료";
            }
            catch (OperationCanceledException) { /* 파괴로 인한 취소 — 정상 */ }
            catch (Exception e)
            {
                m_lastLog = $"접속 실패: {e.Message}";
                Debug.LogException(e, this);
            }
            finally
            {
                m_joining = false;
            }
        }

        private void OnGUI()
        {
            GUILayout.Label($"[낚시터] {m_lastLog}");
            if (GUILayout.Button("로비로 복귀")) ReturnToLobby();
            GUILayout.Space(8);

            var runner = m_launcher.Runner;
            if (runner == null)
            {
                GUI.enabled = !m_joining;
                if (GUILayout.Button(m_joining ? "접속 중..." : "낚시터 접속 (드롭인)"))
                    _ = JoinAsync();
                GUI.enabled = true;
            }
            else
            {
                GUILayout.Label($"접속: {runner.SessionInfo.Name} ({runner.SessionInfo.PlayerCount}명) — 고정 샤드, 실제 매칭/샤딩 TBD");

                if (m_voice != null && GUILayout.Button($"마이크 {(m_voice.MicEnabled ? "끄기" : "켜기")}"))
                    m_voice.SetMicEnabled(!m_voice.MicEnabled);
                if (m_mute != null)
                {
                    foreach (PlayerRef player in runner.ActivePlayers)
                    {
                        if (player == runner.LocalPlayer) continue;
                        bool muted = m_mute.IsMuted(player);
                        if (GUILayout.Button($"P{player.PlayerId} 음소거 {(muted ? "해제" : "")}"))
                            m_mute.SetMuted(player, !muted);
                    }
                }
            }

            GUILayout.Space(8);
            GUILayout.Label($"── 낚시 ── 상태: {m_rod.State}");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("캐스팅")) m_rod.Cast(m_spot);
            if (GUILayout.Button("챔질")) m_rod.Reel();
            GUILayout.EndHorizontal();

            GUILayout.Space(8);
            GUILayout.Label("── 인벤토리 ──");
            foreach (KeyValuePair<ItemDef, int> entry in new List<KeyValuePair<ItemDef, int>>(m_state.Inventory.Items))
                GUILayout.Label($"{entry.Key.DisplayName} x{entry.Value}");
        }
    }
}
