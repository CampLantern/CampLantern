using CampLantern.Bootstrap;
using CampLantern.Core;
using UnityEngine;

namespace CampLantern.Cooking
{
    /// <summary>
    /// 개발용 자동 검증 봇 — 물리 요리 풀 루프 무인 주행 (봇 주입 패턴, knowledge/unity-editor-automation.md).
    /// 흐름: 붕어 2마리 주입 → 선반 프록시 스폰 확인 → 프록시를 냄비 위로 낙하 투입 x2(인벤토리 차감 확인)
    ///       → 국자 젓기 시뮬레이션 → Cooked(생선구이) 확인 → 주입 잔여물 정리(저장 오염 방지).
    /// 로그는 ASCII 마커 "[CookTest]" (Editor.log 한글 mojibake 회피). 물리 조작은 전부 FixedUpdate (RULE-03).
    /// 플레이 중 아무 GameObject에 AddComponent 하면 동작 (전부 자가 탐색).
    /// </summary>
    public class PhysicalCookingSelfTest : MonoBehaviour
    {
        private enum Phase { WaitShelf1, Drop1, WaitAdd1, WaitShelf2, Drop2, WaitAdd2, Stir, Cleanup, Done }

        private const string k_fishId   = "fish_crucian";
        private const string k_resultId = "item_grilled_fish";

        private EstateHarness m_harness;
        private CookingPot m_pot;
        private StirTool m_ladle;
        private Rigidbody m_ladleRb;
        private ItemDef m_fish;
        private ItemDef m_result;

        private Phase m_phase = Phase.Done; // Start 성공 전까지 대기
        private float m_phaseDeadline;
        private int m_baseFish, m_baseResult;
        private ItemDef m_cookedResult;
        private IngredientPickup m_target;   // 현재 낙하 대상 프록시 (직전 소모분 제외용으로도 유지)
        private float m_stirX;
        private int m_stirDir = 1;
        private int m_fails;

        /// <summary>컴파일 증명용 — Reflection.Invoke로 호출해 신 어셈블리 로드를 확인한다.</summary>
        public static string Ping() => "cooktest-v1";

        private void Start()
        {
            m_harness = FindFirstObjectByType<EstateHarness>();
            m_pot     = FindFirstObjectByType<CookingPot>();
            m_ladle   = FindFirstObjectByType<StirTool>();
            var registry = Resources.Load<ContentRegistry>("ContentRegistry");

            bool refsOk = m_harness != null && m_pot != null && m_ladle != null && registry != null
                          && registry.TryGetItem(k_fishId, out m_fish)
                          && registry.TryGetItem(k_resultId, out m_result);
            if (!refsOk)
            {
                Debug.LogError("[CookTest] ABORT missing refs: harness=" + (m_harness != null) +
                               " pot=" + (m_pot != null) + " ladle=" + (m_ladle != null) +
                               " registry=" + (registry != null));
                return;
            }

            m_ladleRb = m_ladle.GetComponent<Rigidbody>();
            m_pot.Cooked -= OnCooked;
            m_pot.Cooked += OnCooked;

            Inventory inv = m_harness.State.Inventory;
            m_baseFish   = inv.CountOf(m_fish);
            m_baseResult = inv.CountOf(m_result);
            inv.Add(m_fish, 2); // 테스트 재료 주입 — Cleanup에서 기준치로 되돌린다

            Debug.Log($"[CookTest] START baseFish={m_baseFish} baseResult={m_baseResult} injected fish x2");
            SetPhase(Phase.WaitShelf1, 6f);
        }

        private void OnDestroy()
        {
            if (m_pot != null) m_pot.Cooked -= OnCooked;
        }

        private void OnCooked(ItemDef result) => m_cookedResult = result;

        private void SetPhase(Phase phase, float timeout)
        {
            m_phase = phase;
            m_phaseDeadline = Time.time + timeout;
        }

        private void Fail(string reason)
        {
            m_fails++;
            Debug.LogError($"[CookTest] FAIL({m_phase}): {reason}");
            SetPhase(Phase.Cleanup, 1f);
        }

        // 직전 소모 프록시(m_target — 파괴 예약 상태일 수 있음)를 제외하고 붕어 프록시를 찾는다
        private IngredientPickup FindFishProxy(IngredientPickup exclude)
        {
            foreach (IngredientPickup p in FindObjectsByType<IngredientPickup>(FindObjectsSortMode.None))
                if (p != exclude && p.Item == m_fish) return p;
            return null;
        }

        private void FixedUpdate()
        {
            if (m_phase == Phase.Done) return;
            if (Time.time > m_phaseDeadline)
            {
                if (m_phase == Phase.Cleanup) FinishCleanup();
                else Fail("timeout");
                return;
            }

            switch (m_phase)
            {
                case Phase.WaitShelf1:
                case Phase.WaitShelf2:
                {
                    IngredientPickup found = FindFishProxy(m_phase == Phase.WaitShelf2 ? m_target : null);
                    if (found != null)
                    {
                        m_target = found;
                        Debug.Log($"[CookTest] OK shelf proxy ready ({m_phase})");
                        SetPhase(m_phase == Phase.WaitShelf1 ? Phase.Drop1 : Phase.Drop2, 2f);
                    }
                    break;
                }

                case Phase.Drop1:
                case Phase.Drop2:
                {
                    Rigidbody rb = m_target != null ? m_target.GetComponent<Rigidbody>() : null;
                    if (rb == null) { Fail("proxy lost before drop"); return; }
                    rb.linearVelocity  = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.position = m_pot.transform.TransformPoint(new Vector3(0f, 1.6f, 0f)); // 투입구 위 — 자유 낙하
                    Debug.Log($"[CookTest] dropped proxy above pot ({m_phase})");
                    SetPhase(m_phase == Phase.Drop1 ? Phase.WaitAdd1 : Phase.WaitAdd2, 5f);
                    break;
                }

                case Phase.WaitAdd1:
                    if (m_pot.Ingredients.Count >= 1)
                    {
                        Debug.Log($"[CookTest] OK intake#1 potCount={m_pot.Ingredients.Count} invFish={m_harness.State.Inventory.CountOf(m_fish)}");
                        SetPhase(Phase.WaitShelf2, 6f); // 선반 재보충 대기
                    }
                    break;

                case Phase.WaitAdd2:
                    if (m_pot.Ingredients.Count >= 2)
                    {
                        Debug.Log($"[CookTest] OK intake#2 potCount={m_pot.Ingredients.Count} invFish={m_harness.State.Inventory.CountOf(m_fish)}");
                        m_ladleRb.isKinematic = true; // 봇이 단독 제어 — 그랩 대체 (Cleanup에서 복원)
                        SetPhase(Phase.Stir, 25f);
                    }
                    break;

                case Phase.Stir:
                {
                    if (m_cookedResult != null)
                    {
                        int invResult = m_harness.State.Inventory.CountOf(m_result);
                        Debug.Log($"[CookTest] OK cooked result={m_cookedResult.Id} invResult={invResult} potCount={m_pot.Ingredients.Count}");
                        if (m_cookedResult != m_result)
                            Fail($"unexpected result {m_cookedResult.Id} (expected {k_resultId})");
                        else if (invResult != m_baseResult + 1)
                            Fail($"result count mismatch: {invResult} != {m_baseResult + 1}");
                        else
                            SetPhase(Phase.Cleanup, 1f);
                        return;
                    }

                    // 국자 끝을 투입구 중심 주변에서 좌우 왕복 — 스텝 0.05m/틱 (판정창 0.15~5m/s 안)
                    m_stirX += m_stirDir * 0.05f;
                    if (Mathf.Abs(m_stirX) > 0.12f) m_stirDir = -m_stirDir;
                    Vector3 zoneCenter = m_pot.transform.TransformPoint(new Vector3(0f, 1.0f, 0f));
                    Vector3 tipOffset  = m_ladle.Tip.position - m_ladle.transform.position;
                    m_ladleRb.position = zoneCenter + new Vector3(m_stirX, 0f, 0f) - tipOffset;
                    break;
                }

                case Phase.Cleanup:
                    FinishCleanup();
                    break;
            }
        }

        private void FinishCleanup()
        {
            // 저장 오염 방지 — 냄비 잔여분을 인벤토리로 되돌린 뒤, 붕어/결과물을 시작 기준치로 트림.
            // 플레이 종료 시 하네스 OnApplicationQuit가 정리된 상태를 저장한다.
            if (m_pot != null) m_pot.Clear();
            if (m_harness != null)
            {
                Inventory inv = m_harness.State.Inventory;
                int extraResult = inv.CountOf(m_result) - m_baseResult;
                if (extraResult > 0) inv.TryRemove(m_result, extraResult);
                int extraFish = inv.CountOf(m_fish) - m_baseFish;
                if (extraFish > 0) inv.TryRemove(m_fish, extraFish);
            }
            if (m_ladleRb != null) m_ladleRb.isKinematic = false;

            Debug.Log(m_fails == 0
                ? "[CookTest] RESULT: PASS (intake x2 + stir-cook + inventory authority verified)"
                : $"[CookTest] RESULT: FAIL ({m_fails} failure(s) — see FAIL logs above)");
            m_phase = Phase.Done;
        }
    }
}
