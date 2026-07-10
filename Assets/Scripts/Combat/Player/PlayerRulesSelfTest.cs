using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CampLantern.Combat.Data;
using CampLantern.Combat.Monsters;
using UnityEngine;

namespace CampLantern.Combat.Player
{
    /// <summary>
    /// 플레이어 전투 규칙 무인 검증 봇 (§2, §4-2, §3 — step-09).
    /// **주의: 단독 주입 전용** — PartyWipeWatcher가 씬 전역 PlayerHealth.All을 보므로
    /// 다른 봇의 플레이어와 동시 주행하면 전멸 판정이 오염된다.
    ///
    /// 페이즈: 1) HP 스케일링 (2인째 첫 유효타 시 ×1.5 갱신 + 잔여 플랫 가산)
    ///         2) 기여 목록 (유효타 있는 A·B만, 참관자 C 제외)
    ///         3) 다운 (피격 면제 + 어그로 즉시 재계산 + 타겟팅 제외)
    ///         4) 소생 (홀드 3s → HP 절반, 소생자 피격에도 취소 없음)
    ///         5) 전멸 (전원 다운 → PartyWiped 이벤트, 씬 로드는 봇에서 비활성)
    /// </summary>
    public class PlayerRulesSelfTest : MonoBehaviour
    {
        private const string k_tag = "[PlayerTest]";

        private MonsterHealth m_monster;
        private AggroController m_aggro;
        private PlayerHealth m_playerA, m_playerB, m_playerC;
        private PlayerReviver m_reviverB;
        private Transform m_handB;
        private PartyWipeWatcher m_wipe;
        private CombatBalanceData m_balance;
        private Vector3 m_origin;
        private bool m_failed;
        private bool m_wipedFired;
        private IReadOnlyCollection<GameObject> m_huntContributors;

        private void Start()
        {
            m_origin = new Vector3(2000f, 0f, 2000f);

            m_balance = ScriptableObject.CreateInstance<CombatBalanceData>();
            m_balance.hpScalePerExtraPlayer = 0.5f;
            m_balance.reviveHoldSeconds = 3f;
            m_balance.reviveHpRatio = 0.5f;

            var monsterData = ScriptableObject.CreateInstance<MonsterData>();
            monsterData.baseHp = 100;
            monsterData.weakpointMultiplier = 1.5f;
            monsterData.aggroHysteresisMeters = 2f;
            monsterData.weakpointLockSeconds = 5f;
            monsterData.weakpointDecaySeconds = 1.5f;

            var monsterGo = new GameObject("PlayerTest_Monster");
            monsterGo.transform.position = m_origin;
            m_aggro = monsterGo.AddComponent<AggroController>(); // RequireComponent가 Health 선부착
            m_monster = monsterGo.GetComponent<MonsterHealth>();
            m_monster.Configure(monsterData, m_balance);
            m_aggro.Configure(monsterData);
            m_monster.HuntSucceeded += OnHuntSucceeded;

            m_playerA = CreatePlayer("PlayerTest_A", new Vector3(3f, 0f, 0f));
            m_playerB = CreatePlayer("PlayerTest_B", new Vector3(5f, 0f, 0f));
            m_playerC = CreatePlayer("PlayerTest_C", new Vector3(7f, 0f, 0f)); // 참관자 — 유효타 없음

            m_handB = new GameObject("PlayerTest_HandB").transform;
            m_handB.position = m_playerB.transform.position;
            m_reviverB = m_playerB.gameObject.AddComponent<PlayerReviver>();
            m_reviverB.Configure(m_balance, m_handB);

            var wipeGo = new GameObject("PlayerTest_Wipe");
            m_wipe = wipeGo.AddComponent<PartyWipeWatcher>();
            m_wipe.Configure("Lobby", loadSceneOnWipe: false); // 봇 — 이벤트만 검사
            m_wipe.PartyWiped += OnWiped;

            Debug.Log($"{k_tag} start — 5 phases");
            StartCoroutine(Run());
        }

        private void OnDestroy()
        {
            if (m_monster != null) m_monster.HuntSucceeded -= OnHuntSucceeded;
            if (m_wipe != null) m_wipe.PartyWiped -= OnWiped;
            if (m_playerA != null) CombatPlayers.Unregister(m_playerA.transform);
            if (m_playerB != null) CombatPlayers.Unregister(m_playerB.transform);
            if (m_playerC != null) CombatPlayers.Unregister(m_playerC.transform);
        }

        private PlayerHealth CreatePlayer(string name, Vector3 offset)
        {
            var go = new GameObject(name);
            go.transform.position = m_origin + offset;
            var health = go.AddComponent<PlayerHealth>();
            health.Configure(100);
            CombatPlayers.Register(go.transform);
            return health;
        }

        private void OnHuntSucceeded(IReadOnlyCollection<GameObject> contributors) => m_huntContributors = contributors;
        private void OnWiped() => m_wipedFired = true;

        private IEnumerator Run()
        {
            yield return new WaitForSeconds(3f);

            // ── 1) HP 스케일링 — A 유효타(기여 1, 무변) → B 첫 유효타(기여 2 → max 150·잔여 +50) ──
            HitMonster(m_playerA, 10);
            bool afterA = m_monster.MaxHp == 100 && m_monster.CurrentHp == 90;
            HitMonster(m_playerB, 10);
            bool afterB = m_monster.MaxHp == 150 && m_monster.CurrentHp == 130; // 90 - 10 + 50
            Report("PHASE1_HP_SCALING", afterA && afterB,
                   $"afterA(100/90)={afterA} afterB(150/130)={afterB} actual={m_monster.CurrentHp}/{m_monster.MaxHp}");

            // ── 2) 기여 목록 — 처치 시 A·B만, 참관자 C 제외 ──
            HitMonster(m_playerA, 1000); // 처치
            yield return null;
            bool p2 = m_huntContributors != null
                      && m_huntContributors.Contains(m_playerA.gameObject)
                      && m_huntContributors.Contains(m_playerB.gameObject)
                      && !m_huntContributors.Contains(m_playerC.gameObject)
                      && m_huntContributors.Count == 2;
            Report("PHASE2_CONTRIBUTION", p2,
                   $"contributors={(m_huntContributors != null ? m_huntContributors.Count : -1)} (expected 2 — A,B)");

            // ── 3) 다운 — 피격 면제 + 어그로 즉시 재계산(A→B) + 타겟팅 제외 ──
            m_aggro.AcquireIfNone(); // A(3m)가 최근접 타겟
            bool targetWasA = m_aggro.CurrentTarget == m_playerA.transform;
            HitPlayer(m_playerA, 100); // 다운
            bool downedOk = m_playerA.IsDowned && !m_playerA.IsDamageable;
            HitPlayer(m_playerA, 50);  // 피격 면제 — hp 0 유지·로그 없음
            bool immuneOk = m_playerA.CurrentHp == 0;
            yield return null;
            bool retargeted = m_aggro.CurrentTarget == m_playerB.transform; // AnyPlayerDowned → 즉시 재계산 (다운 제외)
            Report("PHASE3_DOWN", targetWasA && downedOk && immuneOk && retargeted,
                   $"targetWasA={targetWasA} downed={downedOk} immune={immuneOk} retargetedToB={retargeted}");

            // ── 4) 소생 — B가 A를 홀드 3s → HP 50, 홀드 중 B 피격에도 취소 없음 ──
            m_handB.position = m_playerA.transform.position; // 손 접촉
            bool began = m_reviverB.TryBeginRevive(m_playerA);
            yield return new WaitForSeconds(1f);
            HitPlayer(m_playerB, 10); // 소생자 피격 — 취소되면 안 됨 (§2-3)
            bool stillHolding = m_reviverB.CurrentTarget == m_playerA;
            yield return new WaitForSeconds(2.5f); // 총 3.5s > 3s
            bool revived = !m_playerA.IsDowned && m_playerA.CurrentHp == 50 && m_playerA.IsDamageable;
            Report("PHASE4_REVIVE", began && stillHolding && revived,
                   $"began={began} holdSurvivedHit={stillHolding} revived={revived} hp={m_playerA.CurrentHp}(expected 50)");

            // ── 5) 전멸 — 전원 다운 → PartyWiped (씬 로드 비활성) ──
            HitPlayer(m_playerA, 100);
            HitPlayer(m_playerB, 100);
            HitPlayer(m_playerC, 100);
            yield return new WaitForSeconds(1f); // 감시 주기 0.5s
            Report("PHASE5_WIPE", m_wipedFired, $"wipedFired={m_wipedFired}");

            Debug.Log($"{k_tag} {(m_failed ? "RESULT FAIL" : "RESULT ALL PASS")}");
        }

        private void HitMonster(PlayerHealth attacker, int damage)
        {
            m_monster.ApplyDamage(new HitInfo
            {
                BaseDamage = damage,
                Attacker = attacker.gameObject, // 기여자 신원 = 공격자 루트
                Point = m_monster.transform.position,
            });
        }

        private void HitPlayer(PlayerHealth target, int damage)
        {
            target.ApplyDamage(new HitInfo { BaseDamage = damage, Point = target.transform.position, Attacker = gameObject });
        }

        private void Report(string label, bool ok, string detail)
        {
            if (!ok) m_failed = true;
            Debug.Log($"{k_tag} {label} {(ok ? "PASS" : "FAIL")} — {detail}");
        }
    }
}
