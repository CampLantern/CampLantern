using System.Collections;
using CampLantern.Combat.Data;
using CampLantern.Combat.Monsters;
using UnityEngine;

namespace CampLantern.Combat.Weapons
{
    /// <summary>
    /// 활/화살 무인 검증 봇 (봇 주입 패턴). 씬 의존 없음 — 표적·지면·활·화살 템플릿을 코드로 조립.
    ///
    /// 페이즈: 1) 당김-초속-데미지 (풀/하프 드로우 비교 — 중력 낙차 실측 반영)
    ///         2) 약점 배율 (머리 명중 ×1.5)
    ///         3) 빗나감 — 지면 접촉 시 소멸 (박힘 아님)
    ///         4) 회수 (+1) / 만땅 무반응 / 사망 후 화살 유지
    ///         5) 소지 소진 시 발사 불가
    /// 기대 데미지: clamp(v,5,25) × K(1) × (1 + 3×0.1) = v×1.3 — 풀 드로우 v0 24.5 → ~32, 하프 12.0 → ~16.
    /// </summary>
    public class BowSelfTest : MonoBehaviour
    {
        private const string k_tag = "[BowTest]";

        private MonsterHealth m_target;
        private Bow m_bow;
        private ArrowQuiver m_quiver;
        private Transform m_bowRoot;
        private Transform m_hand;
        private CombatBalanceData m_balance;
        private Vector3 m_origin;
        private bool m_failed;

        private void Start()
        {
            m_origin = new Vector3(1800f, 0f, 1800f);

            // 지면 — 빗나감 소멸 테스트용 (봇 사이트는 샌드박스 지면 밖)
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "BowTest_Ground";
            ground.transform.position = m_origin;
            ground.transform.localScale = new Vector3(4f, 1f, 4f);

            // 표적 — 몸통 캡슐 + 머리 구 (약점)
            var monsterData = ScriptableObject.CreateInstance<MonsterData>();
            monsterData.baseHp = 100000;
            monsterData.weakpointMultiplier = 1.5f;
            var targetGo = new GameObject("BowTest_Target");
            targetGo.transform.position = m_origin + new Vector3(4f, 0f, 0f);
            m_target = targetGo.AddComponent<MonsterHealth>();
            m_target.Configure(monsterData);

            var body = new GameObject("Body");
            body.transform.SetParent(targetGo.transform, false);
            body.transform.localPosition = new Vector3(0f, 1f, 0f);
            var bodyCol = body.AddComponent<CapsuleCollider>();
            bodyCol.height = 1.6f;
            bodyCol.radius = 0.4f;
            body.AddComponent<HitVolume>().Configure(isWeakpoint: false);

            var head = new GameObject("Head");
            head.transform.SetParent(targetGo.transform, false);
            head.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            var headCol = head.AddComponent<SphereCollider>();
            headCol.radius = 0.35f;
            head.AddComponent<HitVolume>().Configure(isWeakpoint: true);

            // 밸런스 (step-01 CombatBalance 값과 동일 — 기대값이 코드 안에서 닫히도록 인스턴스)
            m_balance = ScriptableObject.CreateInstance<CombatBalanceData>();
            m_balance.bowDrawConstC = 5f;
            m_balance.arrowDamageK = 1f;
            m_balance.arrowWeightBonusB = 3f;
            m_balance.arrowVMin = 5f;
            m_balance.arrowVMax = 25f;
            m_balance.maxArrows = 30;
            m_balance.arrowRetrieveRadius = 0.3f;

            var arrowData = ScriptableObject.CreateInstance<ArrowData>();
            arrowData.weight = 0.1f;

            var bowData = ScriptableObject.CreateInstance<BowData>();
            bowData.elasticity = 25f;
            bowData.maxDurability = 40;

            // 화살 템플릿 (비활성 — Instantiate 원본)
            var arrowTemplate = new GameObject("BowTest_ArrowTemplate");
            arrowTemplate.AddComponent<Arrow>().Configure(arrowData, m_balance);
            arrowTemplate.SetActive(false);

            // 활 + 화살집 + 손
            var bowGo = new GameObject("BowTest_Bow");
            m_bowRoot = bowGo.transform;
            var arrowOrigin = new GameObject("ArrowOrigin").transform;
            arrowOrigin.SetParent(bowGo.transform, false);
            m_bow = bowGo.AddComponent<Bow>();
            m_bow.Configure(bowData, m_balance, arrowTemplate, arrowOrigin);

            m_hand = new GameObject("BowTest_Hand").transform;
            m_hand.position = m_origin + new Vector3(0f, 1f, 5f); // 회수 대상에서 멀리
            var quiverGo = new GameObject("BowTest_Quiver");
            m_quiver = quiverGo.AddComponent<ArrowQuiver>();
            m_quiver.Configure(m_balance, m_hand);
            m_bow.Quiver = m_quiver;

            Debug.Log($"{k_tag} start — 5 phases");
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            yield return new WaitForSeconds(3f); // 히칭 회피

            // ── 1) 당김-초속-데미지 — 풀 드로우(≈32) vs 하프 드로우(≈16), 4m 낙차 반영 ──
            int fullDmg = 0, halfDmg = 0;
            yield return FireAndMeasure(1.0f, aimY: 1.0f, d => fullDmg = d);
            yield return FireAndMeasure(0.5f, aimY: 1.3f, d => halfDmg = d); // 하프는 낙차 커서 위로 조준
            bool p1 = Mathf.Abs(fullDmg - 32) <= 3 && Mathf.Abs(halfDmg - 16) <= 3 && fullDmg > halfDmg;
            Report("PHASE1_DRAW_DAMAGE", p1, $"full={fullDmg} (~32) half={halfDmg} (~16)");

            // ── 2) 약점 — 머리(y2.2) 명중 ×1.5 ──
            int headDmg = 0;
            yield return FireAndMeasure(1.0f, aimY: 2.3f, d => headDmg = d); // 낙차 ~0.13 보정
            bool p2 = Mathf.Abs(headDmg - 48) <= 5;
            Report("PHASE2_WEAKPOINT", p2, $"head={headDmg} (~48 = 32×1.5)");

            // ── 3) 빗나감 — 지면 접촉 소멸 (박힘 아님) ──
            int stuckBefore = Arrow.StuckArrows.Count;
            int hpBefore = m_target.CurrentHp;
            AimAndDraw(1.0f, aimY: 1.0f);
            m_bowRoot.rotation = Quaternion.LookRotation(new Vector3(1f, -0.6f, 0f).normalized); // 지면으로
            Arrow missArrow = null;
            m_bow.Fired += CaptureArrow;
            m_bow.TryFire();
            m_bow.Fired -= CaptureArrow;
            yield return new WaitForSeconds(1.5f);
            bool p3 = missArrow == null // 소멸됨 (Unity null)
                      && Arrow.StuckArrows.Count == stuckBefore
                      && m_target.CurrentHp == hpBefore;
            Report("PHASE3_MISS_DESTROY", p3,
                   $"destroyed={missArrow == null} stuck={Arrow.StuckArrows.Count}(={stuckBefore}) hpUnchanged={m_target.CurrentHp == hpBefore}");

            void CaptureArrow(Bow b, Arrow a, float v) => missArrow = a;

            // ── 4) 회수 / 만땅 무반응 / 사망 후 유지 ──
            // 주의: 몸통에 박힌 화살들이 서로 회수 반경(0.3m) 안에 있어 한 틱에 복수 회수될 수 있다
            // → "+N" 정확 단언 대신 "1발 이상 회수 + 박힌 수 감소"로 판정 (회수량-거리 관계는 실기 튜닝 몫)
            int countBefore = m_quiver.Count;
            int aliveBefore = Arrow.StuckArrows.Count;
            Arrow stuck = FirstStuck();
            bool retrieved = false;
            if (stuck != null)
            {
                m_hand.position = stuck.transform.position;
                yield return null; // Quiver.Update 1틱
                yield return null; // 지연 Destroy 반영
                retrieved = m_quiver.Count > countBefore && Arrow.StuckArrows.Count < aliveBefore;
            }
            // 만땅 무반응 — 화살이 박혀 있는 상태에서 소지를 가득 리셋
            Arrow stuck2 = FirstStuck();
            bool fullNoop = false;
            if (stuck2 != null)
            {
                m_quiver.Configure(m_balance, m_hand); // Count = max
                m_hand.position = stuck2.transform.position;
                yield return null;
                yield return null;
                fullNoop = m_quiver.IsFull && stuck2 != null && Arrow.StuckArrows.Count > 0;
            }
            // 사망 후 박힌 화살 유지
            m_target.ApplyDamage(new HitInfo { BaseDamage = 1000000, Attacker = gameObject });
            yield return null;
            bool persistAfterDeath = FirstStuck() != null;
            bool p4 = retrieved && fullNoop && persistAfterDeath;
            Report("PHASE4_RETRIEVE", p4, $"retrieved={retrieved} fullNoop={fullNoop} persistAfterDeath={persistAfterDeath}");

            // ── 5) 소지 소진 — 1발짜리 화살집으로 교체 후 소진 → 발사 불가 ──
            var tinyBalance = ScriptableObject.CreateInstance<CombatBalanceData>();
            tinyBalance.maxArrows = 1;
            tinyBalance.arrowRetrieveRadius = 0.3f;
            tinyBalance.bowDrawConstC = 5f; tinyBalance.arrowDamageK = 1f;
            tinyBalance.arrowWeightBonusB = 3f; tinyBalance.arrowVMin = 5f; tinyBalance.arrowVMax = 25f;
            m_hand.position = m_origin + new Vector3(0f, 50f, 0f); // 회수 차단
            var tinyQuiver = new GameObject("BowTest_TinyQuiver").AddComponent<ArrowQuiver>();
            tinyQuiver.Configure(tinyBalance, null);
            m_bow.Quiver = tinyQuiver;
            AimAndDraw(1.0f, aimY: 1.0f);
            bool first = m_bow.TryFire();
            AimAndDraw(1.0f, aimY: 1.0f);
            bool second = m_bow.TryFire();
            bool p5 = first && !second && tinyQuiver.Count == 0;
            Report("PHASE5_AMMO_EMPTY", p5, $"first={first} second={second}(expected false) count={tinyQuiver.Count}");

            Debug.Log($"{k_tag} {(m_failed ? "RESULT FAIL" : "RESULT ALL PASS")}");
        }

        /// <summary>지정 당김으로 표적을 향해 발사하고 이번 발의 데미지를 콜백으로 전달.</summary>
        private IEnumerator FireAndMeasure(float draw, float aimY, System.Action<int> onDamage)
        {
            int hpBefore = m_target.CurrentHp;
            AimAndDraw(draw, aimY);
            if (!m_bow.TryFire())
            {
                m_failed = true;
                Debug.Log($"{k_tag} TryFire failed unexpectedly");
                yield break;
            }
            float t = 0f;
            while (t < 2f && m_target.CurrentHp == hpBefore) { t += Time.deltaTime; yield return null; }
            onDamage(hpBefore - m_target.CurrentHp);
        }

        // 활을 (0, aimY, 0) 높이에서 표적 방향(+x 평면)으로 정렬하고 당김 설정
        private void AimAndDraw(float draw, float aimY)
        {
            m_bowRoot.position = m_origin + new Vector3(0f, aimY, 0f);
            m_bowRoot.rotation = Quaternion.LookRotation(Vector3.right);
            m_bow.SetDrawRatio(draw);
        }

        private static Arrow FirstStuck()
        {
            var stuck = Arrow.StuckArrows;
            for (int i = 0; i < stuck.Count; i++)
                if (stuck[i] != null) return stuck[i];
            return null;
        }

        private void Report(string label, bool ok, string detail)
        {
            if (!ok) m_failed = true;
            Debug.Log($"{k_tag} {label} {(ok ? "PASS" : "FAIL")} — {detail}");
        }
    }
}
