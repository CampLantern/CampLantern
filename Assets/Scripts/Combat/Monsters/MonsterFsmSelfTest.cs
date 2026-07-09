using CampLantern.Combat.Data;
using UnityEngine;

namespace CampLantern.Combat.Monsters
{
    /// <summary>
    /// 몬스터 FSM 무인 검증 봇 (knowledge/unity-editor-automation.md 봇 주입 패턴, MeleeSwingSelfTest 전례).
    /// 플레이 중 브릿지로 AddComponent하면 몬스터·가짜 플레이어를 코드로 조립해 4개 페이즈를 주행하고
    /// [MonsterTest] 태그로 결과를 남긴다. 씬 의존 없음.
    ///
    /// 페이즈: A) Idle 배회 (활동 반경 내 이동)  B) 감지→추격 (접근 확인)
    ///         C) 귀환 (즉시 Return·무적·원위치 HP 회복)  D) 약점 배율 + 사망 (FSM 정지·재피격 무시)
    /// </summary>
    public class MonsterFsmSelfTest : MonoBehaviour
    {
        private const string k_tag = "[MonsterTest]";

        // 테스트 전용 수치 (기대값 계산이 코드 안에서 닫히도록 에셋 미사용)
        private const int k_baseHp = 100;
        private const float k_roam = 3f, k_detect = 5f, k_chase = 8f;
        private const float k_idleSpeed = 1.5f, k_chaseSpeed = 4f;
        private const float k_weakpointMul = 1.5f;

        private MonsterController m_monster;
        private MonsterHealth m_health;
        private Transform m_fakePlayer;
        private Vector3 m_spawn;

        private int m_phase;
        private float m_phaseTime;
        private float m_warmup = 3f;
        private bool m_failed;
        private bool m_diedFired;

        // 페이즈 내부 진행 상태
        private int m_stage;
        private float m_stageTimer;
        private float m_movedDistance;
        private float m_maxDistFromSpawn;
        private Vector3 m_lastMonsterPos;
        private float m_chaseStartDist;
        private Vector3 m_deathPos;

        private void Start()
        {
            m_spawn = new Vector3(1200f, 0f, 1200f);

            var data = ScriptableObject.CreateInstance<MonsterData>();
            data.baseHp = k_baseHp;
            data.roamRadius = k_roam;
            data.detectRadius = k_detect;
            data.chaseRadius = k_chase;
            data.idleMoveSpeed = k_idleSpeed;
            data.chaseMoveSpeed = k_chaseSpeed;
            data.weakpointMultiplier = k_weakpointMul;

            var root = new GameObject("MonsterTest_Monster");
            root.transform.position = m_spawn;
            m_monster = root.AddComponent<MonsterController>(); // RequireComponent가 MonsterHealth 선부착
            m_health = root.GetComponent<MonsterHealth>();
            m_health.Configure(data);
            m_monster.Configure(data);

            m_health.Died -= OnDied;
            m_health.Died += OnDied;

            // 가짜 플레이어 — 처음엔 추격 반경 밖
            m_fakePlayer = new GameObject("MonsterTest_Player").transform;
            m_fakePlayer.position = m_spawn + new Vector3(30f, 0f, 0f);
            CombatPlayers.Register(m_fakePlayer);

            m_lastMonsterPos = root.transform.position;
            Debug.Log($"{k_tag} start — 4 phases");
        }

        private void OnDestroy()
        {
            if (m_health != null) m_health.Died -= OnDied;
            if (m_fakePlayer != null) CombatPlayers.Unregister(m_fakePlayer);
        }

        private void OnDied() => m_diedFired = true;

        private void Update()
        {
            if (m_phase > 3) return;
            if (m_warmup > 0f) { m_warmup -= Time.deltaTime; return; }
            m_phaseTime += Time.deltaTime;

            switch (m_phase)
            {
                case 0: RunIdlePhase(); break;
                case 1: RunChasePhase(); break;
                case 2: RunReturnPhase(); break;
                case 3: RunDeathPhase(); break;
            }

            if (m_phaseTime > 30f)
            {
                Report($"PHASE{m_phase + 1}", false, "timeout");
                AdvancePhase();
            }
        }

        /// <summary>A: 5초 배회 관찰 — 이동 발생 + 활동 반경 이탈 없음 + 상태 Idle.</summary>
        private void RunIdlePhase()
        {
            TrackMovement();

            if (m_phaseTime < 5f) return;

            bool ok = m_monster.CurrentStateName == "Idle"
                      && m_movedDistance > 0.5f
                      && m_maxDistFromSpawn <= k_roam + 1f;
            Report("PHASE1_IDLE_ROAM", ok,
                   $"state={m_monster.CurrentStateName} moved={m_movedDistance:F1} maxDist={m_maxDistFromSpawn:F1} (roam {k_roam})");
            AdvancePhase();
        }

        /// <summary>B: 플레이어를 감지 반경 안으로 → Chase 전환 + 접근 확인. 추격 중 선피격(hp 80)로 C 준비.</summary>
        private void RunChasePhase()
        {
            switch (m_stage)
            {
                case 0:
                    m_fakePlayer.position = m_spawn + new Vector3(4f, 0f, 0f); // 감지 반경(5) 안
                    m_stage = 1;
                    break;
                case 1:
                    if (m_monster.CurrentStateName == "Chase")
                    {
                        m_chaseStartDist = Planar(m_monster.transform.position, m_fakePlayer.position);
                        ApplyHit(baseDamage: 20, weakpoint: false); // C의 회복 검증용 선피격 → hp 80
                        m_stage = 2;
                        m_stageTimer = 1.0f;
                    }
                    break;
                case 2:
                    m_stageTimer -= Time.deltaTime;
                    if (m_stageTimer <= 0f)
                    {
                        float now = Planar(m_monster.transform.position, m_fakePlayer.position);
                        bool ok = (now < m_chaseStartDist - 0.5f || now < 1.0f) && m_health.CurrentHp == 80;
                        Report("PHASE2_DETECT_CHASE", ok,
                               $"state={m_monster.CurrentStateName} dist {m_chaseStartDist:F1}->{now:F1} hp={m_health.CurrentHp} (expected 80)");
                        AdvancePhase();
                    }
                    break;
            }
        }

        /// <summary>C: 플레이어 이탈 → 즉시 Return + 무적(피격 무시) + 원위치 도달 시 HP 전량 회복 후 Idle.</summary>
        private void RunReturnPhase()
        {
            switch (m_stage)
            {
                case 0:
                    m_fakePlayer.position = m_spawn + new Vector3(30f, 0f, 0f); // 추격 반경(8) 밖
                    m_stage = 1;
                    break;
                case 1:
                    if (m_monster.CurrentStateName == "Return")
                    {
                        ApplyHit(baseDamage: 30, weakpoint: false); // 무적 — 무시돼야 함
                        bool invulnOk = m_health.CurrentHp == 80;
                        if (!invulnOk) { Report("PHASE3_RETURN", false, $"invulnerable broken hp={m_health.CurrentHp} (expected 80)"); AdvancePhase(); return; }
                        m_stage = 2;
                    }
                    break;
                case 2:
                    if (m_monster.CurrentStateName == "Idle")
                    {
                        bool ok = m_health.CurrentHp == k_baseHp
                                  && Planar(m_monster.transform.position, m_spawn) <= 1f;
                        Report("PHASE3_RETURN", ok,
                               $"hp={m_health.CurrentHp}/{k_baseHp} distFromSpawn={Planar(m_monster.transform.position, m_spawn):F2}");
                        AdvancePhase();
                    }
                    break;
            }
        }

        /// <summary>D: 약점 배율(×1.5) 확인 후 사망 — Died 발화, FSM 정지("Dead"), 이동 정지, 재피격 무시.</summary>
        private void RunDeathPhase()
        {
            switch (m_stage)
            {
                case 0:
                    ApplyHit(baseDamage: 10, weakpoint: true); // 100 - round(10×1.5) = 85
                    bool mulOk = m_health.CurrentHp == 85;
                    if (!mulOk) { Report("PHASE4_DEATH", false, $"weakpoint mul wrong hp={m_health.CurrentHp} (expected 85)"); AdvancePhase(); return; }
                    ApplyHit(baseDamage: 200, weakpoint: false); // 즉사
                    m_deathPos = m_monster.transform.position;
                    m_stage = 1;
                    m_stageTimer = 1.0f;
                    break;
                case 1:
                    m_stageTimer -= Time.deltaTime;
                    if (m_stageTimer <= 0f)
                    {
                        ApplyHit(baseDamage: 50, weakpoint: false); // 사망 후 무시돼야 함
                        bool ok = m_diedFired
                                  && m_monster.CurrentStateName == "Dead"
                                  && !m_health.IsDamageable
                                  && m_health.CurrentHp == 0
                                  && Planar(m_monster.transform.position, m_deathPos) < 0.05f; // 이동 정지
                        Report("PHASE4_DEATH", ok,
                               $"died={m_diedFired} state={m_monster.CurrentStateName} hp={m_health.CurrentHp} moved={Planar(m_monster.transform.position, m_deathPos):F3}");
                        AdvancePhase();
                        Debug.Log($"{k_tag} {(m_failed ? "RESULT FAIL" : "RESULT ALL PASS")}");
                    }
                    break;
            }
        }

        private void ApplyHit(int baseDamage, bool weakpoint)
        {
            var hit = new HitInfo
            {
                BaseDamage = baseDamage,
                IsWeakpoint = weakpoint,
                Speed = 4f,
                Point = m_monster.transform.position,
                Attacker = gameObject,
            };
            m_health.ApplyDamage(in hit);
        }

        private void TrackMovement()
        {
            Vector3 pos = m_monster.transform.position;
            m_movedDistance += Planar(pos, m_lastMonsterPos);
            m_maxDistFromSpawn = Mathf.Max(m_maxDistFromSpawn, Planar(pos, m_spawn));
            m_lastMonsterPos = pos;
        }

        private static float Planar(Vector3 a, Vector3 b)
        {
            Vector3 to = a - b;
            to.y = 0f;
            return to.magnitude;
        }

        private void AdvancePhase()
        {
            m_phase++;
            m_phaseTime = 0f;
            m_stage = 0;
        }

        private void Report(string label, bool ok, string detail)
        {
            if (!ok) m_failed = true;
            Debug.Log($"{k_tag} {label} {(ok ? "PASS" : "FAIL")} — {detail}");
        }
    }
}
