using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CampLantern.Combat;
using CampLantern.Combat.Player;
using CampLantern.Core;
using CampLantern.Core.Persistence;
using CampLantern.Hunting;
using CampLantern.Networking;
using CampLantern.Networking.Voice;
using CampLantern.UI;
using Fusion;
using Meta.XR.MultiplayerBlocks.Fusion;
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
        private WristHud m_wristHud; // 리그(DontDestroyOnLoad)에 붙어서 씬 이탈 시 직접 파괴해야 함
        private ToastHud m_toast;    // 시야 하단 알림 — 리그 부착이라 하네스가 파괴 책임
        private PlayerHealth m_localHealth; // 손목 HUD 전투 줄용 — 스폰이 늦어 지연 해석

        private readonly List<HuntTarget> m_huntTargets = new List<HuntTarget>();        // 훅한 사냥감들(사슴+멧돼지)
        private readonly HashSet<HuntLedger> m_hookedLedgers = new HashSet<HuntLedger>(); // 보상 중복 구독 방지

        // 네트워크 전투 몬스터(신 곰 — NetworkedHuntMonster) 훅. HuntTarget 라인과 별개 이벤트라 따로 구독한다.
        private readonly List<NetworkedHuntMonster> m_netMonstersBuffer = new List<NetworkedHuntMonster>();
        private readonly List<NetworkedHuntMonster> m_netMonsters = new List<NetworkedHuntMonster>();
        private readonly HashSet<NetworkedHuntMonster> m_hookedMonsters = new HashSet<NetworkedHuntMonster>();

        private NetworkRunner m_dummyRunner;
        private bool m_dummyJoining;
        // 더미 러너는 실제 몸체 없이 세션에만 합류하지만, Fusion Shared Mode 복제 규칙상
        // 이미 스폰된(실플레이어) 아바타·전투 존재가 더미 러너 시점에도 프록시로 재생성된다.
        // 같은 프로세스·같은 리그(카메라)라 그 프록시가 실플레이어와 같은 좌표로 따라붙어 보이는 것 —
        // 더미 자체가 아니라 "내 아바타의 유령 사본"이 렌더되는 것이므로 렌더러만 꺼서 감춘다.
        private readonly List<AvatarBehaviourFusion> m_dummyAvatarBuffer = new List<AvatarBehaviourFusion>();

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

            // 세션·음소거 소셜 패널 (P0 판정: 음소거 토글) — Resources 로드라 씬 배선 불필요
            var socialPrefab = Resources.Load<SocialPanel>("SocialPanel");
            if (socialPrefab != null)
            {
                var social = Instantiate(socialPrefab);
                social.transform.position = new Vector3(-2f, 1.4f, -1.5f); // 스폰 왼편 (빌보드라 회전 불필요)
                social.Bind(m_launcher, m_voice, m_mute);
            }

            // 손목 HUD — 코인 + 전투 상태(내 HP·다운). 리그(DontDestroyOnLoad)에 붙으므로 파괴는 하네스 책임
            m_wristHud = WristHud.Spawn(m_state.Wallet, CombatStatusLine);
            m_toast = ToastHud.Spawn(); // 사냥 보상 알림

            m_launcher.SessionStarted -= OnSessionStarted;
            m_launcher.SessionStarted += OnSessionStarted;
        }

        private void OnDestroy()
        {
            m_launcher.SessionStarted -= OnSessionStarted;
            UnhookAllHuntTargets();

            if (m_wristHud != null) Destroy(m_wristHud.gameObject); // 리그에 붙어 있어 씬 언로드로 안 죽는다
            if (m_toast != null) Destroy(m_toast.gameObject);

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

            // 네트워크 전투 몬스터(신 곰)도 훅 — RewardGranted가 HuntLedger와 별개 이벤트라 미구독 시 보상 유실
            m_netMonstersBuffer.Clear();
            m_launcher.Runner.GetAllBehaviours(m_netMonstersBuffer);
            foreach (NetworkedHuntMonster m in m_netMonstersBuffer)
                if (m != null && m_hookedMonsters.Add(m))
                {
                    m_netMonsters.Add(m);
                    m.RewardGranted -= OnRewardGranted;
                    m.RewardGranted += OnRewardGranted;

                    // 머리 위 HP 게이지 — HuntTarget과 동일 UI, 값 소스만 다름
                    HuntTargetDef monsterDef = m.Health != null && m.Health.Data != null ? m.Health.Data.huntDef : null;
                    HuntHealthGauge.Attach(m,
                        monsterDef != null ? monsterDef.DisplayName : "전투 몬스터",
                        () => m.NetCurrentHp,
                        () => Mathf.Max(1, m.NetMaxHp),
                        () => m.HuntActive,
                        monsterDef != null ? monsterDef.RequiredParticipants : 1);
                }

            HideDummyAvatarProxies();
        }

        // 더미 러너 시점에 복제된 아바타 프록시(=내 아바타의 유령 사본)는 통째로 비활성화한다.
        // 더미는 스포너가 없어 자기 아바타를 스폰하지 않으므로(HuntZoneHarness는 AvatarController를
        // 더미 러너에 배선하지 않음), 더미 러너 스코프에서 발견되는 AvatarBehaviourFusion은 전부
        // 실플레이어 아바타의 복제본이다 — 상태 동기화(NetworkTransform 등)는 필요 없다(화면에만 안 보이면 됨).
        //
        // **Renderer만 껐던 1차 시도는 매 프레임 다시 켜지는 경합에 졌다** — Meta Avatar SDK가 LOD/스트리밍
        // 완료 시 RefreshAllActives()로 렌더러 활성 상태를 자체적으로 재구성하는데, 이게 우리 Update()보다
        // 늦게(LateUpdate 등) 실행되면 그 프레임에 다시 켜져 버리고, 이후에도 반복돼 "꺼지지 않는 것처럼"
        // 보인다(실측 — 사용자 재확인으로 드러남). GameObject 자체를 꺼버리면 그 아래 어떤 컴포넌트의
        // Update/LateUpdate도 통째로 안 돌아서 이 경합 자체가 사라진다 — 더 확실한 해결.
        private void HideDummyAvatarProxies()
        {
            if (m_dummyRunner == null) return;

            m_dummyAvatarBuffer.Clear();
            m_dummyRunner.GetAllBehaviours(m_dummyAvatarBuffer);
            foreach (AvatarBehaviourFusion avatar in m_dummyAvatarBuffer)
            {
                if (avatar == null || !avatar.gameObject.activeSelf) continue;
                avatar.gameObject.SetActive(false);
            }
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

            // 머리 위 HP 게이지 (IMGUI HP 라벨의 VR 대체) — 대상 despawn 시 게이지가 자멸한다
            HuntHealthGauge.Attach(target,
                target.Def != null ? target.Def.DisplayName : "사냥감",
                () => target.CurrentHealth,
                () => target.Def != null ? target.Def.MaxHealth : 100,
                () => target.HuntActive,
                target.Def != null ? target.Def.RequiredParticipants : 1);
        }

        private void UnhookAllHuntTargets()
        {
            foreach (HuntLedger ledger in m_hookedLedgers)
                if (ledger != null) ledger.RewardGranted -= OnRewardGranted;
            m_hookedLedgers.Clear();
            m_huntTargets.Clear();

            foreach (NetworkedHuntMonster monster in m_hookedMonsters)
                if (monster != null) monster.RewardGranted -= OnRewardGranted;
            m_hookedMonsters.Clear();
            m_netMonsters.Clear();
        }

        private void OnRewardGranted(HuntTargetDef def)
        {
            if (def.RewardMaterials == null) return;
            foreach (ItemDef material in def.RewardMaterials) m_state.Inventory.Add(material);
            m_lastLog = $"사냥 보상 지급: {def.DisplayName}";
            if (m_toast != null) m_toast.Show(m_lastLog);
            m_state.Save(); // OS 강제종료 대비 — 보상 즉시 저장 (낚시 포획 저장과 동일 정책)
        }

        // 손목 HUD 둘째 줄 — 전투 존재(PlayerHealth)는 세션 시작 후 스폰되므로 찾을 때까지 지연 해석
        private string CombatStatusLine()
        {
            if (m_localHealth == null) m_localHealth = FindFirstObjectByType<PlayerHealth>();
            if (m_localHealth == null) return "존A · 숲";
            return m_localHealth.IsDowned
                ? "다운! 소생 대기"
                : $"HP {m_localHealth.CurrentHp}/{m_localHealth.MaxHp}";
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
            m_netMonsters.RemoveAll(m => m == null || m.Object == null);
            if (m_huntTargets.Count == 0 && m_netMonsters.Count == 0)
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

            // 네트워크 전투 몬스터(신 곰) — 실무기(HitVolume) 경로 외에 디버그 타격/시작 버튼 제공
            foreach (NetworkedHuntMonster monster in m_netMonsters)
            {
                HuntTargetDef huntDef = monster.Health != null && monster.Health.Data != null
                    ? monster.Health.Data.huntDef : null;
                string monsterName = huntDef != null ? huntDef.DisplayName : "전투 몬스터";
                int needPlayers = huntDef != null ? huntDef.RequiredParticipants : 1;
                GUILayout.Label($"[{monsterName}] HP: {monster.NetCurrentHp}/{monster.NetMaxHp}  진행중: {(bool)monster.HuntActive}  (필요 {needPlayers}인)");
                GUILayout.BeginHorizontal();
                if (monster.Object.HasStateAuthority)
                {
                    if (GUILayout.Button("사냥 시작"))
                        m_lastLog = monster.TryStartHunt() ? $"{monsterName} 사냥 시작!" : $"{monsterName} 시작 불가 ({needPlayers}인 미만)";
                }
                else
                {
                    GUILayout.Label("(시작은 마스터만)", GUILayout.Width(110));
                }
                if (GUILayout.Button("타격"))
                    monster.ApplyDamage(new HitInfo
                    {
                        BaseDamage = m_hitDamage,
                        Attacker = gameObject,
                        Point = monster.transform.position,
                    });
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(8);
            GUILayout.Label("── 인벤토리 ──");
            foreach (KeyValuePair<ItemDef, int> entry in new List<KeyValuePair<ItemDef, int>>(m_state.Inventory.Items))
                GUILayout.Label($"{entry.Key.DisplayName} x{entry.Value}");
        }
    }
}
