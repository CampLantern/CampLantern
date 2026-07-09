using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CampLantern.Core;
using CampLantern.Core.Persistence;
using CampLantern.Hunting;
using CampLantern.Networking;
using CampLantern.Networking.Voice;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CampLantern.Bootstrap
{
    /// <summary>
    /// 사냥터 — 존 분할 구조 (room-architecture.md). 이 씬은 존 하나("A")를 대표한다 —
    /// 실제로는 존마다 별도 Room·별도 씬 인스턴스가 필요하지만, 존 자동 배정(위치 기반 라우팅)은
    /// 매칭 백엔드가 필요해 P0 범위 밖 — 고정 존 하나로 협동 사냥 루프만 검증한다.
    /// 세션/더미 피어/음성 배선은 P0Harness와 동일 패턴 — 사냥 전용으로 축소.
    /// 인벤토리는 PlayerState를 통해 로컬 JSON에 저장/복원된다.
    /// </summary>
    public class HuntZoneHarness : MonoBehaviour
    {
        [SerializeField] private SessionLauncher m_launcher;
        // 존에 등장하는 사냥감 종들 + 종별 스폰 위치(인덱스 매칭). 큰뿔사슴(협동)·멧돼지(솔로)·곰(협동) 등 N종.
        [SerializeField] private NetworkObject[] m_huntPrefabs;
        [SerializeField] private Vector3[] m_huntSpawnPositions;
        [SerializeField] private string m_lobbySceneName = "Lobby";
        [SerializeField] private string m_zoneId = "a";
        [SerializeField] private int m_hitDamage = 10;

        private PlayerState m_state;
        private ContentRegistry m_registry;
        private readonly List<HuntTarget> m_huntTargetsBuffer = new List<HuntTarget>();

        private VoiceController m_voice;
        private PlayerMute m_mute;

        private readonly List<HuntTarget> m_huntTargets = new List<HuntTarget>();        // 훅한 사냥감들(사슴+멧돼지)
        private readonly HashSet<HuntLedger> m_hookedLedgers = new HashSet<HuntLedger>(); // 보상 중복 구독 방지

        private NetworkRunner m_dummyRunner;
        private bool m_dummyJoining;

        private bool m_joining;
        private string m_lastLog = "-";

        private void Awake()
        {
            m_registry = Resources.Load<ContentRegistry>("ContentRegistry");
            if (m_registry == null)
                Debug.LogError("[HuntZoneHarness] ContentRegistry 없음 — Tools > Make Assets > Content Registry 실행 필요");

            m_state = new PlayerState();
            if (m_registry != null) m_state.Load(m_registry);

            m_voice = m_launcher.GetComponent<VoiceController>();
            m_mute  = m_launcher.GetComponent<PlayerMute>();

            m_launcher.SessionStarted -= OnSessionStarted;
            m_launcher.SessionStarted += OnSessionStarted;
        }

        private void OnDestroy()
        {
            m_launcher.SessionStarted -= OnSessionStarted;
            UnhookAllHuntTargets();

            if (m_dummyRunner != null && m_dummyRunner.IsRunning)
                m_dummyRunner.Shutdown();
            m_dummyRunner = null;
        }

        private void OnApplicationQuit()
        {
            m_state.Save(); // 사냥터는 EstateManager가 없으므로 배치 목록은 디스크 값 그대로 보존됨
        }

        private void ReturnToLobby()
        {
            m_state.Save();
            SceneManager.LoadScene(m_lobbySceneName);
        }

        private void Update()
        {
            if (m_launcher.Runner == null) return;

            // 스폰된 사냥감을 모두 훅(사슴+멧돼지). 각 사냥감의 보상 이벤트를 1회씩 구독한다.
            m_huntTargetsBuffer.Clear();
            m_launcher.Runner.GetAllBehaviours(m_huntTargetsBuffer);
            foreach (HuntTarget t in m_huntTargetsBuffer)
                if (t != null && !m_huntTargets.Contains(t)) HookHuntTarget(t);
        }

        public HuntTarget FindHuntTarget(NetworkRunner runner)
        {
            if (runner == null) return null;
            m_huntTargetsBuffer.Clear();
            runner.GetAllBehaviours(m_huntTargetsBuffer);
            return m_huntTargetsBuffer.Count > 0 ? m_huntTargetsBuffer[0] : null;
        }

        // 더미(2인 협동) 테스트용 — 협동 필수(2인+) 사냥감을 우선 선택. 없으면 첫 사냥감.
        private HuntTarget FindCoopHuntTarget(NetworkRunner runner)
        {
            if (runner == null) return null;
            m_huntTargetsBuffer.Clear();
            runner.GetAllBehaviours(m_huntTargetsBuffer);
            HuntTarget fallback = null;
            foreach (HuntTarget t in m_huntTargetsBuffer)
            {
                if (t == null) continue;
                if (fallback == null) fallback = t;
                if (t.Def != null && t.Def.RequiredParticipants > 1) return t;
            }
            return fallback;
        }

        private void OnSessionStarted(NetworkRunner runner)
        {
            if (!runner.IsSharedModeMasterClient || m_huntPrefabs == null) return; // 스폰은 마스터만
            for (int i = 0; i < m_huntPrefabs.Length; i++)
            {
                if (m_huntPrefabs[i] == null) continue;
                Vector3 pos = (m_huntSpawnPositions != null && i < m_huntSpawnPositions.Length)
                    ? m_huntSpawnPositions[i] : Vector3.zero;
                runner.Spawn(m_huntPrefabs[i], pos, Quaternion.identity);
            }
        }

        private void HookHuntTarget(HuntTarget target)
        {
            m_huntTargets.Add(target);
            var ledger = target.GetComponent<HuntLedger>();
            if (ledger != null && m_hookedLedgers.Add(ledger))
            {
                ledger.RewardGranted -= OnRewardGranted;
                ledger.RewardGranted += OnRewardGranted;
            }
        }

        private void UnhookAllHuntTargets()
        {
            foreach (HuntLedger ledger in m_hookedLedgers)
                if (ledger != null) ledger.RewardGranted -= OnRewardGranted;
            m_hookedLedgers.Clear();
            m_huntTargets.Clear();
        }

        private void OnRewardGranted(HuntTargetDef def)
        {
            if (def.RewardMaterials == null) return;
            foreach (ItemDef material in def.RewardMaterials) m_state.Inventory.Add(material);
            m_lastLog = $"사냥 보상 지급: {def.DisplayName}";
        }

        private async Task JoinAsync()
        {
            m_joining = true;
            try
            {
                await m_launcher.StartHuntZone(m_zoneId, destroyCancellationToken);
                m_lastLog = "사냥터 접속 완료";
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

        private void AddDummyPeer()
        {
            if (m_dummyRunner != null || m_dummyJoining) return;
            _ = StartDummyAsync();
        }

        private void RemoveDummyPeer()
        {
            var runner = m_dummyRunner;
            m_dummyRunner = null;
            if (runner != null) _ = runner.Shutdown();
        }

        private async Task StartDummyAsync()
        {
            m_dummyJoining = true;
            try
            {
                var go = new GameObject("DummyPeer");
                var runner = go.AddComponent<NetworkRunner>();
                var sceneManager = go.AddComponent<NetworkSceneManagerDefault>();

                var args = new StartGameArgs
                {
                    GameMode                   = GameMode.Shared,
                    SessionName                = $"hunt_zone_{m_zoneId}",
                    SceneManager               = sceneManager,
                    StartGameCancellationToken = destroyCancellationToken,
                };
                NetworkSceneInfo? arenaScene = SessionLauncher.TryGetMultiPeerArenaScene();
                if (arenaScene.HasValue) args.Scene = arenaScene.Value;

                var result = await runner.StartGame(args);
                if (!result.Ok)
                {
                    Destroy(go);
                    m_lastLog = $"더미 접속 실패: {result.ShutdownReason}";
                    return;
                }

                m_dummyRunner = runner;
                m_lastLog = $"더미 접속 완료 (P{runner.LocalPlayer.PlayerId})";
            }
            catch (OperationCanceledException) { /* 파괴로 인한 취소 — 정상 */ }
            catch (Exception e)
            {
                m_lastLog = $"더미 접속 실패: {e.Message}";
                Debug.LogException(e, this);
            }
            finally
            {
                m_dummyJoining = false;
            }
        }

        private void OnGUI()
        {
            GUILayout.Label($"[사냥터_존{m_zoneId.ToUpperInvariant()}] {m_lastLog}");
            if (GUILayout.Button("로비로 복귀")) ReturnToLobby();
            GUILayout.Space(8);

            NetworkRunner runner = m_launcher.Runner;
            if (runner == null)
            {
                GUI.enabled = !m_joining;
                if (GUILayout.Button(m_joining ? "접속 중..." : "사냥터 접속"))
                    _ = JoinAsync();
                GUI.enabled = true;
                return;
            }

            GUILayout.Label($"접속: {runner.SessionInfo.Name} ({runner.SessionInfo.PlayerCount}명)");

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

            GUILayout.Space(8);
            if (m_dummyRunner == null)
            {
                GUI.enabled = !m_dummyJoining;
                if (GUILayout.Button(m_dummyJoining ? "더미 접속 중..." : "더미 플레이어 추가 (2인 테스트)"))
                    AddDummyPeer();
                GUI.enabled = true;
            }
            else
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"더미: P{m_dummyRunner.LocalPlayer.PlayerId}", GUILayout.Width(80));
                HuntTarget dummyTarget = FindCoopHuntTarget(m_dummyRunner); // 협동 대상(큰뿔사슴) 우선
                if (dummyTarget != null)
                {
                    if (GUILayout.Button("더미 타격"))
                        dummyTarget.ApplyHit(m_dummyRunner.LocalPlayer, m_hitDamage);
                    var dummyLedger = dummyTarget.GetComponent<HuntLedger>();
                    if (dummyLedger != null && GUILayout.Button("더미 유인"))
                        dummyLedger.RecordContribution(m_dummyRunner.LocalPlayer, HuntLedger.ContributionKind.Lure);
                }
                if (GUILayout.Button("더미 제거")) RemoveDummyPeer();
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(8);
            m_huntTargets.RemoveAll(t => t == null || t.Object == null); // despawn된 사냥감 정리
            if (m_huntTargets.Count == 0)
            {
                GUILayout.Label("사냥감 스폰 대기 중...");
                return;
            }

            foreach (HuntTarget target in m_huntTargets)
            {
                string targetName = target.Def != null ? target.Def.DisplayName : "사냥감";
                int need = target.Def != null ? target.Def.RequiredParticipants : 1;
                GUILayout.Label($"[{targetName}] HP: {target.CurrentHealth}  진행중: {target.HuntActive}  (필요 {need}인)");
                GUILayout.BeginHorizontal();
                if (target.Object.HasStateAuthority)
                {
                    if (GUILayout.Button("사냥 시작"))
                        m_lastLog = target.TryStartHunt() ? $"{targetName} 사냥 시작!" : $"{targetName} 시작 불가 ({need}인 미만)";
                }
                else
                {
                    GUILayout.Label("(시작은 마스터만)", GUILayout.Width(110));
                }
                if (GUILayout.Button("타격")) target.ApplyHit(runner.LocalPlayer, m_hitDamage);
                var ledger = target.GetComponent<HuntLedger>();
                if (ledger != null && GUILayout.Button("유인(기여)"))
                    ledger.RecordContribution(runner.LocalPlayer, HuntLedger.ContributionKind.Lure);
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(8);
            GUILayout.Label("── 인벤토리 ──");
            foreach (KeyValuePair<ItemDef, int> entry in new List<KeyValuePair<ItemDef, int>>(m_state.Inventory.Items))
                GUILayout.Label($"{entry.Key.DisplayName} x{entry.Value}");
        }
    }
}
