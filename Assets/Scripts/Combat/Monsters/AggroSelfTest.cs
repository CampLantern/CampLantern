using System.Collections;
using CampLantern.Combat.Data;
using UnityEngine;

namespace CampLantern.Combat.Monsters
{
    /// <summary>
    /// 어그로 6규칙 무인 검증 봇 (봇 주입 패턴). 이동 변수를 제거하기 위해 몬스터는 **정적**
    /// (MonsterHealth + AggroController만, MonsterController 없음)으로 조립 — 거리가 고정되어
    /// 히스테리시스/고정/감쇄 판정이 결정론적이다. FSM 통합(Chase 위임·Return 리셋)은
    /// step-06 스킬 봇이 실주행으로 커버한다.
    ///
    /// 페이즈: 1) 초기 획득(근접)  2) 히스테리시스(2m 미만 차이 무시 / 이상 전환)
    ///         3) 약점 고정(5s, 고정 중 근접 재계산 무시)  4) 감쇄(-1.5s)
    ///         5) 감쇄 소진 → 타겟 스틸(신규 5s)  6) 리셋 + 다운 즉시 재계산
    /// </summary>
    public class AggroSelfTest : MonoBehaviour
    {
        private const string k_tag = "[AggroTest]";
        private const float k_hysteresis = 2f, k_lock = 5f, k_decay = 1.5f;

        private MonsterHealth m_health;
        private AggroController m_aggro;
        private Transform m_monster;
        private Transform m_playerA;
        private Transform m_playerB;
        private Vector3 m_origin;
        private bool m_failed;

        private void Start()
        {
            m_origin = new Vector3(1400f, 0f, 1400f);

            var data = ScriptableObject.CreateInstance<MonsterData>();
            data.baseHp = 100000; // 검증 중 사망 방지
            data.weakpointMultiplier = 1.5f;
            data.aggroHysteresisMeters = k_hysteresis;
            data.weakpointLockSeconds = k_lock;
            data.weakpointDecaySeconds = k_decay;

            var monsterGo = new GameObject("AggroTest_Monster");
            monsterGo.transform.position = m_origin;
            m_monster = monsterGo.transform;
            m_aggro = monsterGo.AddComponent<AggroController>(); // RequireComponent가 MonsterHealth 선부착
            m_health = monsterGo.GetComponent<MonsterHealth>();
            m_health.Configure(data);
            m_aggro.Configure(data);

            m_playerA = new GameObject("AggroTest_PlayerA").transform;
            m_playerB = new GameObject("AggroTest_PlayerB").transform;
            ResetPositions();
            CombatPlayers.Register(m_playerA);
            CombatPlayers.Register(m_playerB);

            Debug.Log($"{k_tag} start — 6 phases");
            StartCoroutine(Run());
        }

        private void OnDestroy()
        {
            if (m_playerA != null) CombatPlayers.Unregister(m_playerA);
            if (m_playerB != null) CombatPlayers.Unregister(m_playerB);
        }

        private void ResetPositions()
        {
            m_playerA.position = m_origin + new Vector3(4f, 0f, 0f); // 근접
            m_playerB.position = m_origin + new Vector3(6f, 0f, 0f); // 원거리
        }

        private IEnumerator Run()
        {
            yield return new WaitForSeconds(2f); // 플레이 진입 히칭 회피

            // 1) 초기 획득 — 가장 가까운 A
            m_aggro.AcquireIfNone();
            Report("PHASE1_ACQUIRE", m_aggro.CurrentTarget == m_playerA, $"target={Name(m_aggro.CurrentTarget)} (expected A)");
            yield return null;

            // 2) 히스테리시스 — B가 1m만 더 가까움(차이<2m) → 유지, 2.5m 더 가까움 → 전환
            m_playerB.position = m_origin + new Vector3(3f, 0f, 0f); // A=4, B=3
            Hit(m_playerB, weakpoint: false);
            bool stay = m_aggro.CurrentTarget == m_playerA;
            m_playerB.position = m_origin + new Vector3(1.5f, 0f, 0f); // A=4, B=1.5 (차이 2.5 ≥ 2)
            Hit(m_playerB, weakpoint: false);
            bool switched = m_aggro.CurrentTarget == m_playerB;
            Report("PHASE2_HYSTERESIS", stay && switched, $"stay={stay} switched={switched}");
            ResetPositions();
            m_aggro.ResetAggro();
            yield return null;

            // 3) 약점 고정 — A 약점타 → 5s 고정, 고정 중 B(더 가까움)의 일반타 재계산 무시
            Hit(m_playerA, weakpoint: true);
            bool locked = m_aggro.CurrentTarget == m_playerA && m_aggro.LockRemainingSeconds > k_lock - 0.6f;
            m_playerB.position = m_origin + new Vector3(1f, 0f, 0f); // B가 훨씬 가까움
            Hit(m_playerB, weakpoint: false);
            bool held = m_aggro.CurrentTarget == m_playerA;
            Report("PHASE3_WEAKPOINT_LOCK", locked && held,
                   $"locked={locked} heldAgainstCloserB={held} remaining={m_aggro.LockRemainingSeconds:F2}");
            yield return null;

            // 4) 감쇄 — B 약점타 → 잔여 -1.5s (동일 프레임 전후 비교)
            float before = m_aggro.LockRemainingSeconds;
            Hit(m_playerB, weakpoint: true);
            float after = m_aggro.LockRemainingSeconds;
            bool decayOk = m_aggro.CurrentTarget == m_playerA && Mathf.Abs((before - after) - k_decay) < 0.1f;
            Report("PHASE4_DECAY", decayOk, $"remaining {before:F2}->{after:F2} (expected -{k_decay})");
            yield return null;

            // 5) 감쇄 소진 — B 약점타 반복 → 잔여 ≤0 순간 B에게 신규 5s 고정(타겟 스틸)
            int guard = 0;
            while (m_aggro.CurrentTarget == m_playerA && guard++ < 10)
            {
                Hit(m_playerB, weakpoint: true);
                yield return null;
            }
            bool stolen = m_aggro.CurrentTarget == m_playerB && m_aggro.LockRemainingSeconds > k_lock - 0.6f;
            Report("PHASE5_DECAY_STEAL", stolen,
                   $"target={Name(m_aggro.CurrentTarget)} remaining={m_aggro.LockRemainingSeconds:F2} hits={guard}");
            yield return null;

            // 6) 리셋 + 다운 즉시 재계산
            m_aggro.ResetAggro();
            bool resetOk = m_aggro.CurrentTarget == null && !m_aggro.IsLocked;
            ResetPositions();                       // A=4(근접), B=6
            Hit(m_playerB, weakpoint: true);        // B에게 고정
            m_aggro.NotifyTargetDowned(m_playerB.gameObject);
            bool downedOk = m_aggro.CurrentTarget == m_playerA && !m_aggro.IsLocked;
            Report("PHASE6_RESET_DOWNED", resetOk && downedOk,
                   $"reset={resetOk} afterDowned={Name(m_aggro.CurrentTarget)} locked={m_aggro.IsLocked}");

            Debug.Log($"{k_tag} {(m_failed ? "RESULT FAIL" : "RESULT ALL PASS")}");
        }

        private void Hit(Transform attacker, bool weakpoint)
        {
            var hit = new HitInfo
            {
                BaseDamage = 1,
                IsWeakpoint = weakpoint,
                Speed = 4f,
                Point = m_monster.position,
                Attacker = attacker.gameObject,
            };
            m_health.ApplyDamage(in hit);
        }

        private static string Name(Transform t) => t != null ? t.name : "(null)";

        private void Report(string label, bool ok, string detail)
        {
            if (!ok) m_failed = true;
            Debug.Log($"{k_tag} {label} {(ok ? "PASS" : "FAIL")} — {detail}");
        }
    }
}
