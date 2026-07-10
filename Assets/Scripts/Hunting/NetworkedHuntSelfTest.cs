using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CampLantern.Combat;
using CampLantern.Combat.Monsters;
using CampLantern.Core;
using CampLantern.Networking;
using Fusion;
using UnityEngine;

namespace CampLantern.Hunting
{
    /// <summary>
    /// 네트워크 전투 무인 검증 봇 (combat-detailed-network step-05, 지시서 8단계 완료 판정).
    /// HuntZone_A 플레이 중 주입 — 스스로 세션 접속 + 더미 피어(2인, tech-stack-decisions 3종 세트 전제)를
    /// 만들어 다음을 검증한다:
    ///  1) GATE_SOLO   — 1인일 때 TryStartHunt 거부 (2인 협동 게이트)
    ///  2) GATE_COOP   — 2인 시작 + HuntActive 양 클라 복제
    ///  3) DUAL_HIT    — 양 클라 동시 타격 후 HP 정합 (기대값: 300+150(2인 스케일링)−100 = 350/450,
    ///                   합산이 순서 무관이라 결정론적)
    ///  4) KILL_REWARD — 처치 시 양 클라 각각 RewardGranted(Hunt_Bear) 발화 (참여자 전원 동일 보상)
    ///  5) GATING      — 권한자(마스터)만 FSM 활성, 더미(비권한)는 비활성 + NetStateName 수신
    /// 예외는 전부 로깅 (fire-and-forget 삼킴 방지 — step-02 교훈). 태그: [NetHuntTest]
    /// </summary>
    public class NetworkedHuntSelfTest : MonoBehaviour
    {
        private const string k_tag = "[NetHuntTest]";

        private NetworkRunner m_dummyRunner;
        private bool m_failed;
        private bool m_mainReward;
        private bool m_dummyReward;

        private void Start()
        {
            RunAsync();
        }

        private void OnDestroy()
        {
            if (m_dummyRunner != null && m_dummyRunner.IsRunning)
                m_dummyRunner.Shutdown();
        }

        private async void RunAsync()
        {
            try { await Run(); }
            catch (Exception e)
            {
                Debug.LogException(e);
                Debug.Log($"{k_tag} RESULT FAIL (exception)");
            }
        }

        private async Task Run()
        {
            Debug.Log($"{k_tag} start — 5 phases");

            // ── 세션 접속 (메인) ──
            var launcher = FindFirstObjectByType<SessionLauncher>();
            if (launcher == null) { Fail("SessionLauncher 없음 — HuntZone_A에서 주입할 것"); return; }
            if (launcher.Runner == null)
                await launcher.StartHuntZone("a", CancellationToken.None);
            NetworkRunner main = launcher.Runner;

            NetworkedHuntMonster mainMonster = await WaitForMonster(main, 20f);
            if (mainMonster == null) { Fail("메인 피어에서 HuntMonster_Bear 스폰 대기 초과"); return; }

            // ── 1) 1인 게이트 ──
            bool soloRejected = !mainMonster.TryStartHunt();
            Report("PHASE1_GATE_SOLO", soloRejected && main.SessionInfo.PlayerCount == 1,
                   $"rejected={soloRejected} players={main.SessionInfo.PlayerCount}");

            // ── 더미 피어 (HuntZoneHarness.StartDummyAsync 패턴) ──
            var dummyGo = new GameObject("NetHuntTest_DummyPeer");
            var dummy = dummyGo.AddComponent<NetworkRunner>();
            var sceneManager = dummyGo.AddComponent<NetworkSceneManagerDefault>();
            var args = new StartGameArgs
            {
                GameMode = GameMode.Shared,
                SessionName = "hunt_zone_a",
                SceneManager = sceneManager,
            };
            NetworkSceneInfo? arena = SessionLauncher.TryGetMultiPeerArenaScene();
            if (arena.HasValue) args.Scene = arena.Value;
            var result = await dummy.StartGame(args);
            if (!result.Ok) { Fail($"더미 접속 실패: {result.ShutdownReason}"); return; }
            m_dummyRunner = dummy;

            NetworkedHuntMonster dummyMonster = await WaitForMonster(dummy, 20f);
            if (dummyMonster == null) { Fail("더미 피어에서 몬스터 복제 대기 초과"); return; }

            // ── 2) 2인 시작 + HuntActive 복제 ──
            bool coopStarted = mainMonster.TryStartHunt();
            bool replicated = await WaitUntil(() => dummyMonster.HuntActive, 5f);
            Report("PHASE2_GATE_COOP", coopStarted && replicated,
                   $"started={coopStarted} dummySeesActive={replicated} players={main.SessionInfo.PlayerCount}");

            // ── 3) 양 클라 동시 타격 — HP 정합 ──
            // 기대값: baseHp 300, P2 첫 유효타 시 2인 스케일링 → Max 450·잔여 +150, 총데미지 100 → 350/450 (순서 무관)
            for (int i = 0; i < 5; i++)
            {
                mainMonster.ApplyDamage(new HitInfo { BaseDamage = 10, Attacker = gameObject, Point = mainMonster.transform.position });
                dummyMonster.ApplyDamage(new HitInfo { BaseDamage = 10, Attacker = dummyGo, Point = dummyMonster.transform.position });
                await Task.Delay(60);
            }
            await Task.Delay(1500); // RPC/복제 정착

            bool hpConsistent = mainMonster.NetCurrentHp == dummyMonster.NetCurrentHp
                                && mainMonster.NetMaxHp == dummyMonster.NetMaxHp;
            bool hpExpected = mainMonster.NetCurrentHp == 350 && mainMonster.NetMaxHp == 450;
            Report("PHASE3_DUAL_HIT", hpConsistent && hpExpected,
                   $"main={mainMonster.NetCurrentHp}/{mainMonster.NetMaxHp} dummy={dummyMonster.NetCurrentHp}/{dummyMonster.NetMaxHp} (expected 350/450)");

            // ── 4) 처치 — 양 클라 보상 (참여자 전원) ──
            mainMonster.RewardGranted += OnMainReward;
            dummyMonster.RewardGranted += OnDummyReward;
            mainMonster.ApplyDamage(new HitInfo { BaseDamage = 1000000, Attacker = gameObject, Point = mainMonster.transform.position });
            bool bothRewarded = await WaitUntil(() => m_mainReward && m_dummyReward, 5f);
            mainMonster.RewardGranted -= OnMainReward;
            dummyMonster.RewardGranted -= OnDummyReward;
            Report("PHASE4_KILL_REWARD", bothRewarded && !mainMonster.HuntActive,
                   $"mainReward={m_mainReward} dummyReward={m_dummyReward} huntActive={(bool)mainMonster.HuntActive}");

            // ── 5) 권한 게이팅 — 마스터만 FSM 활성, 더미는 표현 입력(NetStateName)만 ──
            var mainCtrl = mainMonster.GetComponent<MonsterController>();
            var dummyCtrl = dummyMonster.GetComponent<MonsterController>();
            bool gating = mainCtrl != null && mainCtrl.enabled && dummyCtrl != null && !dummyCtrl.enabled;
            bool stateSynced = !string.IsNullOrEmpty(dummyMonster.NetStateName);
            Report("PHASE5_GATING", gating && stateSynced,
                   $"mainFSM={mainCtrl?.enabled} dummyFSM={dummyCtrl?.enabled} dummyNetState='{dummyMonster.NetStateName}'");

            Debug.Log($"{k_tag} {(m_failed ? "RESULT FAIL" : "RESULT ALL PASS")}");

            await m_dummyRunner.Shutdown();
            m_dummyRunner = null;
        }

        private void OnMainReward(HuntTargetDef def) => m_mainReward = def != null;
        private void OnDummyReward(HuntTargetDef def) => m_dummyReward = def != null;

        private static async Task<NetworkedHuntMonster> WaitForMonster(NetworkRunner runner, float timeout)
        {
            var buffer = new List<NetworkedHuntMonster>();
            float elapsed = 0f;
            while (elapsed < timeout)
            {
                buffer.Clear();
                runner.GetAllBehaviours(buffer);
                if (buffer.Count > 0 && buffer[0] != null) return buffer[0];
                await Task.Delay(250);
                elapsed += 0.25f;
            }
            return null;
        }

        private static async Task<bool> WaitUntil(Func<bool> condition, float timeout)
        {
            float elapsed = 0f;
            while (elapsed < timeout)
            {
                if (condition()) return true;
                await Task.Delay(100);
                elapsed += 0.1f;
            }
            return condition();
        }

        private void Report(string label, bool ok, string detail)
        {
            if (!ok) m_failed = true;
            Debug.Log($"{k_tag} {label} {(ok ? "PASS" : "FAIL")} — {detail}");
        }

        private void Fail(string reason)
        {
            m_failed = true;
            Debug.Log($"{k_tag} FAIL — {reason}");
            Debug.Log($"{k_tag} RESULT FAIL");
        }
    }
}
