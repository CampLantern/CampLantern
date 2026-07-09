using System.Collections;
using CampLantern.Combat.Data;
using CampLantern.Combat.Player;
using UnityEngine;

namespace CampLantern.Combat.Monsters
{
    /// <summary>
    /// 스킬 시스템 무인 검증 봇 (봇 주입 패턴). 풀 조립 몬스터(Controller+Health+Aggro+Skills+HitCheck)와
    /// PlayerHealth 플레이어로 §4-5~§4-7 리듬을 실주행 검증한다 — step-05가 미룬 FSM 통합(어그로 타겟 추적)도
    /// 이 봇의 추격 동작으로 함께 커버된다.
    ///
    /// 페이즈: 1) 추격→공격 — hit window 내 시전당 1회·정확 데미지
    ///         2) 탈진 — 쿨타임 대기 리듬 + 프리딜(피격 정상) + 재공격
    ///         3) 스턴 공격 취소 — 판정 창 전 취소 시 피격 0 + 이동 정지
    ///         4) 탈진 위 스턴 — 오버레이 적용·해제 시 Chase 복귀
    /// </summary>
    public class SkillSelfTest : MonoBehaviour
    {
        private const string k_tag = "[SkillTest]";
        private const int k_skillDamage = 15;

        private MonsterController m_monster;
        private MonsterHealth m_monsterHealth;
        private PlayerHealth m_player;
        private Transform m_playerT;
        private Vector3 m_origin;

        private bool m_failed;
        private int m_hitCount;
        private bool m_hitOutsideAttack;

        private void Start()
        {
            m_origin = new Vector3(1600f, 0f, 1600f);

            var data = ScriptableObject.CreateInstance<MonsterData>();
            data.baseHp = 100000;
            data.roamRadius = 3f;
            data.detectRadius = 6f;
            data.chaseRadius = 12f;
            data.idleMoveSpeed = 1.5f;
            data.chaseMoveSpeed = 4f;
            data.weakpointMultiplier = 1.5f;
            data.skills = new System.Collections.Generic.List<SkillItem>
            {
                new SkillItem
                {
                    displayName = "TestSwipe",
                    skillType = SkillType.Melee,
                    damage = k_skillDamage,
                    cooldown = 4f,
                    recoverTime = 0.8f,
                    animDuration = 1.0f,
                    hitWindowStart = 0.3f,
                    hitWindowEnd = 0.6f,
                    minEngage = 0f,
                    range = 2.5f,
                    arcAngle = 120f,
                },
            };

            var monsterGo = new GameObject("SkillTest_Monster");
            monsterGo.transform.position = m_origin;
            m_monster = monsterGo.AddComponent<MonsterController>();
            m_monsterHealth = monsterGo.GetComponent<MonsterHealth>();
            var aggro = monsterGo.AddComponent<AggroController>();
            var skills = monsterGo.AddComponent<SkillRunner>();
            monsterGo.AddComponent<SkillHitCheck>();
            m_monster.Configure(data);
            m_monsterHealth.Configure(data);
            aggro.Configure(data);
            skills.Configure(data);

            var playerGo = new GameObject("SkillTest_Player");
            m_playerT = playerGo.transform;
            m_playerT.position = m_origin + new Vector3(5f, 0f, 0f); // 감지(6m) 안 — 워밍업 후 즉시 추격
            m_player = playerGo.AddComponent<PlayerHealth>();
            m_player.Configure(100000);
            m_player.Damaged -= OnPlayerDamaged;
            m_player.Damaged += OnPlayerDamaged;
            // 주의: 등록은 워밍업 후(Run 내부) — Start에서 등록하면 워밍업 중 첫 공격이 끝나 카운트가 어긋난다

            Debug.Log($"{k_tag} start — 4 phases");
            StartCoroutine(Run());
        }

        private void OnDestroy()
        {
            if (m_player != null) m_player.Damaged -= OnPlayerDamaged;
            if (m_playerT != null) CombatPlayers.Unregister(m_playerT);
        }

        private void OnPlayerDamaged(HitInfo hit)
        {
            m_hitCount++;
            if (!m_monster.CurrentStateName.StartsWith("Attack"))
                m_hitOutsideAttack = true; // hit window 밖 피격 — 있어선 안 됨
            if (hit.BaseDamage != k_skillDamage)
                m_failed = true;
        }

        private IEnumerator Run()
        {
            yield return new WaitForSeconds(3f); // 히칭 회피
            CombatPlayers.Register(m_playerT);   // 여기서부터 몬스터가 플레이어를 인지 — 페이즈 타이밍 기준점

            // ── 1) 추격 → 공격 — 시전당 1회, 정확 데미지, Attack 상태 중에만 피격 ──
            yield return WaitForState("Attack", 15f);
            yield return WaitForState("Chase", 10f, allowPrefix: false, alsoAccept: "Exhausted"); // 시전+회복 종료
            bool p1 = m_hitCount == 1 && !m_hitOutsideAttack;
            Report("PHASE1_ATTACK_HIT", p1, $"hits={m_hitCount} (expected 1) outsideAttack={m_hitOutsideAttack}");

            // ── 2) 탈진 — 쿨 대기 리듬 + 프리딜 + 재공격 ──
            yield return WaitForState("Exhausted", 10f);
            int hpBefore = m_monsterHealth.CurrentHp;
            m_monsterHealth.ApplyDamage(new HitInfo { BaseDamage = 10, Attacker = gameObject, Point = m_origin });
            bool freedeal = m_monsterHealth.CurrentHp == hpBefore - 10; // 탈진 중 피격 정상
            int hitsBeforeSecond = m_hitCount;
            yield return WaitForState("Attack", 10f); // 쿨 완료 → 재공격
            yield return WaitUntilOrTimeout(() => m_hitCount > hitsBeforeSecond, 5f);
            bool p2 = freedeal && m_hitCount == hitsBeforeSecond + 1;
            Report("PHASE2_EXHAUSTED_RHYTHM", p2, $"freedeal={freedeal} hits={m_hitCount} (expected {hitsBeforeSecond + 1})");

            // ── 3) 스턴 공격 취소 — 판정 창(0.3s) 전 취소 → 피격 0 + 이동 정지 ──
            yield return WaitForState("Chase", 10f, allowPrefix: false, alsoAccept: "Exhausted");
            yield return WaitForState("Attack", 15f);
            int hitsBeforeStun = m_hitCount;
            m_monster.ApplyStun(1.5f); // Attack 진입 직후(≤1프레임) — 창 도달 전 취소
            Vector3 posAtStun = m_monster.transform.position;
            yield return new WaitForSeconds(1.0f);
            float moved = (m_monster.transform.position - posAtStun).magnitude;
            yield return new WaitForSeconds(0.8f); // 스턴 해제 대기
            bool p3 = m_hitCount == hitsBeforeStun && moved < 0.02f && m_monster.CurrentOverlay == null;
            Report("PHASE3_STUN_CANCEL", p3,
                   $"hitsDuringStunnedCast={m_hitCount - hitsBeforeStun} (expected 0) moved={moved:F3} overlayCleared={m_monster.CurrentOverlay == null}");

            // ── 4) 탈진 위 스턴 — 오버레이 적용, 해제 시 Chase 복귀 ──
            yield return WaitForState("Exhausted", 15f);
            m_monster.ApplyStun(1.0f);
            yield return null;
            bool overlayOn = m_monster.CurrentOverlay != null && m_monster.CurrentOverlay.Name == "Stunned"
                             && m_monster.CurrentStateName == "Exhausted"; // 메인 상태 유지(정지)된 채 오버레이
            yield return new WaitForSeconds(1.2f);
            bool resumed = m_monster.CurrentOverlay == null
                           && (m_monster.CurrentStateName == "Chase" || m_monster.CurrentStateName.StartsWith("Attack")
                               || m_monster.CurrentStateName == "Exhausted"); // 해제 → Chase 복귀 후 재판단 진행
            bool p4 = overlayOn && resumed;
            Report("PHASE4_STUN_OVER_EXHAUSTED", p4, $"overlayOn={overlayOn} resumedState={m_monster.CurrentStateName}");

            Debug.Log($"{k_tag} {(m_failed ? "RESULT FAIL" : "RESULT ALL PASS")}");
        }

        private IEnumerator WaitForState(string state, float timeout, bool allowPrefix = true, string alsoAccept = null)
        {
            float t = 0f;
            while (t < timeout)
            {
                string cur = m_monster.CurrentStateName;
                if (allowPrefix ? cur.StartsWith(state) : cur == state) yield break;
                if (alsoAccept != null && cur == alsoAccept) yield break;
                t += Time.deltaTime;
                yield return null;
            }
            m_failed = true;
            Debug.Log($"{k_tag} TIMEOUT waiting state '{state}' (current={m_monster.CurrentStateName})");
        }

        private IEnumerator WaitUntilOrTimeout(System.Func<bool> condition, float timeout)
        {
            float t = 0f;
            while (t < timeout && !condition())
            {
                t += Time.deltaTime;
                yield return null;
            }
        }

        private void Report(string label, bool ok, string detail)
        {
            if (!ok) m_failed = true;
            Debug.Log($"{k_tag} {label} {(ok ? "PASS" : "FAIL")} — {detail}");
        }
    }
}
