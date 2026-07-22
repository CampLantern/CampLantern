using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CampLantern.Cooking;
using CampLantern.Core;
using CampLantern.Core.Persistence;
using CampLantern.Estate;
using CampLantern.Fishing;
using CampLantern.Hunting;
using CampLantern.Networking;
using CampLantern.Player;
using CampLantern.UI;
using Fusion;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace CampLantern.Bootstrap
{
    /// <summary>
    /// P0 완료 판정 플레이 자동 검증 드라이버 (에디터 전용 QA 하네스 — Quest 빌드에는 부트스트랩이 발화하지 않음).
    /// P0AutoVerifyRunner(에디터 메뉴/배치모드)가 SessionState 플래그를 세우고 Play에 진입하면
    /// 이 드라이버가 스폰되어 낚시→요리→판매→구매·배치→영속성 라운드트립→2피어 협동 사냥을
    /// 실제 런타임 API로 구동하고 기준별 Pass/Fail을 Library/P0VerifyReport.json에 기록한다.
    ///
    /// 검증 원칙: 시스템 경로(FSM·이벤트·저장)는 실제 API로 구동하고, VR 입력 물리(젓기 제스처·
    /// 레이 클릭)만 우회한다 — 그 부분은 실기 수동 확인 대상으로 리포트에 SKIP 명시.
    /// </summary>
    public class P0AutoVerifyDriver : MonoBehaviour
    {
        private const float k_globalTimeoutSeconds = 480f;
        private const string k_sessionFlag = "P0AutoVerify"; // P0AutoVerifyRunner와 공유하는 SessionState 키

        [Serializable]
        private class CheckResult
        {
            public string name;
            public string status; // PASS / FAIL / SKIP
            public string detail;
        }

        [Serializable]
        private class Report
        {
            public int pass;
            public int fail;
            public int skip;
            public List<CheckResult> checks = new List<CheckResult>();
        }

        private readonly Report m_report = new Report();
        private float m_startedAt;
        private bool m_finished;
        private bool m_waitOk; // WaitFor 결과 수신용 (코루틴은 out 불가)

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            // 러너가 세운 플래그가 있을 때만 활성 — 일반 에디터 Play에는 개입하지 않는다
            if (!SessionState.GetBool(k_sessionFlag, false)) return;
            if (FindFirstObjectByType<P0AutoVerifyDriver>() != null) return;

            var go = new GameObject("P0AutoVerifyDriver");
            DontDestroyOnLoad(go);
            go.AddComponent<P0AutoVerifyDriver>();
        }

        private void Start()
        {
            m_startedAt = Time.realtimeSinceStartup;
            StartCoroutine(Run());
        }

        private void Update()
        {
            if (!m_finished && Time.realtimeSinceStartup - m_startedAt > k_globalTimeoutSeconds)
                Finish("전역 타임아웃 — 시나리오가 제한 시간 안에 끝나지 않음");
        }

        // ── 시나리오 본체 ─────────────────────────────────────────────

        private IEnumerator Run()
        {
            Debug.Log("[P0Verify] 자동 검증 시작");

            // ── Phase 1: 낚시터 — 시작 코인·포획·인벤토리·즉시 저장 ──
            yield return WaitFor(() => FindFirstObjectByType<FishingGroundHarness>() != null, 30f, "낚시터 하네스 대기");
            if (!m_waitOk) { Finish("FishingGroundHarness 없음 — 시작 씬이 FishingGround가 아님"); yield break; }
            yield return null; // Start까지 완료 대기

            // QA 캡처 정리 — OVRScreenFade의 fadeOnStart 페이드 인이 배치모드에선 WaitForEndOfFrame
            // 미발화로 멈춰, 리그 카메라 앞 페이드 쿼드(CenterEyeAnchor의 4×4m 메시)가 외부 캡처
            // 카메라에 어두운 박스로 찍힌다. 실플레이 결함 아님(렌더링 환경에선 페이드가 정상 완료).
            // 컴포넌트만 끄면 같은 GO의 MeshRenderer가 계속 그리므로 렌더러까지 끈다 (리그 영속 — 1회면 충분).
            var screenFade = FindFirstObjectByType<OVRScreenFade>();
            if (screenFade != null)
            {
                screenFade.enabled = false;
                var fadeRenderer = screenFade.GetComponent<MeshRenderer>();
                if (fadeRenderer != null) fadeRenderer.enabled = false;
            }

            var fishingHarness = FindFirstObjectByType<FishingGroundHarness>();
            var fishState = GetPrivateField<PlayerState>(fishingHarness, "m_state");

            Check("시작 코인 100c 지급 (결함3 수정)", fishState.Wallet.Coins == 100,
                $"신규 저장에서 코인 {fishState.Wallet.Coins} (기대 100)");

            var rod  = FindFirstObjectByType<FishingRod>();
            var spot = FindFirstObjectByType<FishingSpot>();
            if (rod == null || spot == null) { Finish("FishingRod/FishingSpot 없음"); yield break; }

            // VR 입력 어댑터가 매 프레임 SetReeling을 덮어쓰므로 드라이버 구동 동안 비활성화
            foreach (var input in FindObjectsByType<FishingRodInput>(FindObjectsSortMode.None)) input.enabled = false;
            foreach (var rodGrabberAll in FindObjectsByType<RodGrabber>(FindObjectsSortMode.None)) rodGrabberAll.enabled = false;

            LogOversizedVisuals(rod.transform.position); // 진단 — 부두 주변 대형 비주얼 식별 (스크린샷 어두운 슬랩 추적)
            CaptureShot("fishingground_overview", rod.transform.position, 6f, 2f, 240f);

            // 낚싯대 그랩 시각 검증 — 리그 오른손 앵커에 실제 Grab() 부착 후 스크린샷 (빨간 구 = 손 앵커)
            var rodGrabber = FindFirstObjectByType<RodGrabber>();
            Transform rigHand = null;
            if (rodGrabber != null)
            {
                yield return WaitFor(() => GetPrivateField<Transform>(rodGrabber, "m_hand") != null, 10f, "리그 손 앵커 대기");
                rigHand = GetPrivateField<Transform>(rodGrabber, "m_hand");
            }
            if (rigHand != null)
            {
                InvokePrivate(rodGrabber, "Grab");
                yield return null;
                var rigMarker = AddMarker(rigHand, Color.red);
                CaptureShot("rod_held_closeup", rigHand.position, 1.0f, 0.15f, 160f);
                CaptureShot("rod_held_wide", rigHand.position, 2.5f, 0.6f, 200f);
                Check("시각: 낚싯대 그랩 부착 (리그 오른손 앵커)", rod.transform.parent == rigHand,
                    "실제 RodGrabber.Grab 경로 — 스크린샷 rod_held_* 눈검증");
                Destroy(rigMarker);
                InvokePrivate(rodGrabber, "Release");
                yield return null;
            }
            else
            {
                Skip("시각: 낚싯대 그랩 부착", "리그 손 앵커 해석 실패(리그 미스폰) — 실기 확인");
            }

            yield return WaitFor(() => { Fish f; return spot.TryGetNearestFish(rod.transform.position, rod.Rod.length, out f); },
                20f, "사거리 내 물고기 스폰 대기");
            Check("낚시: 사거리 내 물고기 스폰", m_waitOk,
                m_waitOk ? "스폿 실루엣이 낚싯대 사거리 안에 존재" : $"사거리 {rod.Rod.length}m 안에 물고기 없음 — 스폿/부두 배치 확인 필요");

            int coinsBeforeCatch = fishState.Wallet.Coins;
            int baitBefore       = rod.BaitCount;
            int invBeforeCatch   = TotalItems(fishState.Inventory);
            string caughtFishId  = null;

            Action<FishInstance, FishReward> onCaught = (inst, reward) => caughtFishId = inst.Species.fishId;
            rod.FishCaught += onCaught;

            for (int attempt = 0; attempt < 8 && caughtFishId == null; attempt++)
            {
                Fish fish;
                if (!spot.TryGetNearestFish(rod.transform.position, rod.Rod.length, out fish))
                {
                    yield return new WaitForSeconds(1f);
                    continue;
                }

                rod.Cast(fish);
                float attemptStart = Time.realtimeSinceStartup;
                bool fightShot = false;
                while (caughtFishId == null && Time.realtimeSinceStartup - attemptStart < 120f)
                {
                    if (fish == null) break; // Caught 후 파괴 — 이벤트가 caughtFishId를 채웠는지는 루프 조건이 판정
                    switch (fish.State)
                    {
                        case FishState.Bite:        rod.Chamjil(); break;               // 챔질
                        case FishState.FightNormal:
                            if (!fightShot) { fightShot = true; CaptureShot("fishing_fight", fish.transform.position, 3.5f, 1.2f, 30f); }
                            rod.SetReeling(true); break;                                 // 흰줄 — 릴링
                        case FishState.FightEscape:                                      // 빨간줄 — 릴링 해제(텐션 보존)
                        case FishState.FightShake:  rod.SetReeling(false); break;
                        case FishState.Hooked:      rod.SetReeling(false); fish.Hook(); break; // 낚아올림
                        case FishState.Idle:        break;                               // 실패 복귀 — 재시도
                    }
                    if (fish != null && fish.State == FishState.Idle && rod.CurrentFish == null) break; // 시도 실패
                    yield return null;
                }
            }
            rod.FishCaught -= onCaught;

            Check("낚시: FSM 완주 포획 (캐스팅→입질→챔질→파이팅→후킹)", caughtFishId != null,
                caughtFishId != null ? $"포획 어종 {caughtFishId}, 미끼 {baitBefore}→{rod.BaitCount}" : "8회 시도 내 포획 실패");
            Check("낚시: 인벤토리·코인 반영", TotalItems(fishState.Inventory) > invBeforeCatch && fishState.Wallet.Coins > coinsBeforeCatch,
                $"아이템 {invBeforeCatch}→{TotalItems(fishState.Inventory)}, 코인 {coinsBeforeCatch}→{fishState.Wallet.Coins}");
            Check("낚시: 미끼 차감", rod.BaitCount < baitBefore, $"미끼 {baitBefore}→{rod.BaitCount}");

            var savedAfterCatch = ReadSave();
            Check("낚시: 포획 즉시 저장 + 미끼·내구도 기록 (결함5 수정)",
                savedAfterCatch != null && savedAfterCatch.BaitCount == rod.BaitCount && savedAfterCatch.RodDurability == rod.Rod.durability,
                savedAfterCatch == null ? "저장 파일 없음"
                    : $"파일 BaitCount={savedAfterCatch.BaitCount}(메모리 {rod.BaitCount}), RodDurability={savedAfterCatch.RodDurability}(메모리 {rod.Rod.durability})");

            int coinsAfterFishing = fishState.Wallet.Coins;
            int baitAfterFishing  = rod.BaitCount;
            int durAfterFishing   = rod.Rod.durability;

            // ── Phase 2: 영지 — 이월·요리·판매·구매·배치 ──
            SceneManager.LoadScene("EstateTemplate");
            yield return WaitFor(() => FindFirstObjectByType<EstateHarness>() != null, 30f, "영지 하네스 대기");
            if (!m_waitOk) { Finish("EstateHarness 없음"); yield break; }
            yield return null;

            var estateHarness = FindFirstObjectByType<EstateHarness>();
            var estateManager = FindFirstObjectByType<EstateManager>();
            var estState = estateHarness.State;
            var registry = Resources.Load<ContentRegistry>("ContentRegistry");

            ItemDef caughtItem = null;
            bool carriedFish = caughtFishId != null && registry.TryGetItem(caughtFishId, out caughtItem) &&
                               estState.Inventory.CountOf(caughtItem) > 0;
            Check("영속성: 낚시터→영지 코인·인벤토리 이월", estState.Wallet.Coins == coinsAfterFishing && carriedFish,
                $"코인 {estState.Wallet.Coins}(기대 {coinsAfterFishing}), 포획물 이월 {carriedFish}");

            // 레시피 검증 — 씬 냄비가 참조하는 RecipeDef 중 하나를 고른다 (재료는 레지스트리 데이터 주입)
            var pot = FindFirstObjectByType<CookingPot>();
            RecipeDef recipe = PickLoadedRecipe();
            if (pot != null && recipe != null)
            {
                foreach (ItemDef ing in recipe.Ingredients) estState.Inventory.Add(ing); // 검증용 재료 주입

                // 결함4 검증 — 냄비 투입분이 Save에서 인벤토리 보유분으로 합산되는가
                int ing0Before = estState.Inventory.CountOf(recipe.Ingredients[0]);
                pot.TryAddIngredient(recipe.Ingredients[0]);
                InvokePrivate(estateHarness, "SaveState");
                var savedMidCook = ReadSave();
                int savedIng0 = savedMidCook != null ? CountInSave(savedMidCook, recipe.Ingredients[0].Id) : -1;
                Check("요리: 냄비 투입분 저장 합산 (결함4 수정)", savedIng0 == ing0Before,
                    $"투입 전 보유 {ing0Before} → 저장 파일 기록 {savedIng0} (메모리 {estState.Inventory.CountOf(recipe.Ingredients[0])} + 냄비 1)");

                for (int i = 1; i < recipe.Ingredients.Length; i++) pot.TryAddIngredient(recipe.Ingredients[i]);

                ItemDef cookedResult = null;
                Action<ItemDef> onCooked = r => cookedResult = r;
                pot.Cooked += onCooked;
                pot.Cook();
                pot.Cooked -= onCooked;

                Check("요리: 레시피 매칭·결과물 생성", cookedResult == recipe.Result && estState.Inventory.CountOf(recipe.Result) >= 1,
                    $"레시피 {recipe.Id} → 결과 {(cookedResult != null ? cookedResult.Id : "없음")}");

                // 판매 — 하네스 실경로(SellItem: 차감→입금→저장) 호출
                int coinsBeforeSell = estState.Wallet.Coins;
                InvokePrivate(estateHarness, "SellItem", recipe.Result);
                Check("판매: 요리 판매 코인 입금", estState.Wallet.Coins == coinsBeforeSell + recipe.Result.SellPrice,
                    $"코인 {coinsBeforeSell}→{estState.Wallet.Coins} (+{recipe.Result.SellPrice} 기대)");
            }
            else
            {
                Check("요리: 레시피 매칭·결과물 생성", false, pot == null ? "CookingPot 없음" : "로드된 RecipeDef 없음");
            }

            // 구매→배치 — 하네스 실경로. 구매 보장용 코인 보충은 검증 보조(경제 페이싱은 별도 수동 항목)
            EstateObjectDef tentDef;
            bool placedOk = false;
            int placedCount = 0;
            if (registry.TryGetEstateObject("estate_tent", out tentDef))
            {
                estState.Wallet.Add(500);
                int placedBefore = estateManager.PlacedObjects.Count;
                InvokePrivate(estateHarness, "PurchaseDef", tentDef);
                InvokePrivate(estateHarness, "TryPlace", tentDef);
                placedCount = estateManager.PlacedObjects.Count;
                placedOk = placedCount == placedBefore + 1;
                Check("영지: 구매→배치 (수용량 검사 경유)", placedOk, $"배치 수 {placedBefore}→{placedCount}");
                if (placedOk)
                    CaptureShot("estate_placed", estateManager.PlacedObjects[placedCount - 1].transform.position, 4f, 1.5f, 210f);
            }
            else
            {
                Check("영지: 구매→배치 (수용량 검사 경유)", false, "estate_tent Def를 레지스트리에서 못 찾음");
            }

            // ── Phase 2.5: 물리 요리 — 합성 손으로 실제 그랩·투입·젓기 경로 구동 ──
            yield return RunPhysicalCookingPhase(estState, pot);

            int coinsBeforeReload = estState.Wallet.Coins;
            int invBeforeReload   = TotalItems(estState.Inventory);

            // ── Phase 3: 영속성 라운드트립 — 씬 리로드 후 복원 ──
            SceneManager.LoadScene("EstateTemplate");
            yield return WaitFor(() => {
                var h = FindFirstObjectByType<EstateHarness>();
                return h != null && h != estateHarness;
            }, 30f, "영지 리로드 대기");
            var reloadedHarness = FindFirstObjectByType<EstateHarness>();
            var reloadedManager = FindFirstObjectByType<EstateManager>();
            // 배치 복원은 Start에서 수행 — 복원 완료까지 폴링
            yield return WaitFor(() => reloadedManager.PlacedObjects.Count >= placedCount, 10f, "배치 복원 대기");

            Check("영속성: 리로드 후 코인·인벤토리·배치 복원",
                reloadedHarness.State.Wallet.Coins == coinsBeforeReload &&
                TotalItems(reloadedHarness.State.Inventory) == invBeforeReload &&
                (!placedOk || reloadedManager.PlacedObjects.Count == placedCount),
                $"코인 {reloadedHarness.State.Wallet.Coins}(기대 {coinsBeforeReload}), " +
                $"아이템 {TotalItems(reloadedHarness.State.Inventory)}(기대 {invBeforeReload}), " +
                $"배치 {reloadedManager.PlacedObjects.Count}(기대 {placedCount})");

            var savedAfterEstate = ReadSave();
            Check("영속성: 낚싯대 소모품이 영지 저장에도 보존 (결함5 — 모르는 씬 불간섭)",
                savedAfterEstate != null && savedAfterEstate.BaitCount == baitAfterFishing && savedAfterEstate.RodDurability == durAfterFishing,
                savedAfterEstate == null ? "저장 파일 없음"
                    : $"BaitCount={savedAfterEstate.BaitCount}(기대 {baitAfterFishing}), RodDurability={savedAfterEstate.RodDurability}(기대 {durAfterFishing})");

            // ── Phase 4: 사냥터 — 2피어 협동 사냥 + 재사냥 (결함1) ──
            yield return RunHuntPhase();

            // ── Phase 5: 로비 포탈 — UGUI 버튼 클릭→씬 전환 ──
            yield return RunPortalUiPhase();

            // ── 자동화 불가 잔여 항목 — 실기 수동 확인 대상 ──
            Skip("실컨트롤러 입력 매핑 (트리거 조준·그랩, Meta 레이→PointableCanvas)",
                "OVRInput 디바이스 읽기는 헤드리스에서 원리적 불가 — 그랩·젓기·클릭의 코드 경로는 위에서 검증됨, 입력 매핑만 실기 확인");
            Skip("음성 송수신·음소거 실효", "더미 피어는 음성 미지원(의도된 생략) — 실기 2클라이언트(기기+PC) 필요");
            Skip("15~30분 페이싱·젓기 임계값 체감", "자동 드라이버는 대기·체감을 생략 — 실플레이 계측·튜닝 필요");

            Finish(null);
        }

        // 사냥 페이즈 — Photon 클라우드 접속이 필요해 실패해도 나머지 리포트는 유지한다
        private IEnumerator RunHuntPhase()
        {
            SceneManager.LoadScene("HuntZone_A");
            yield return WaitFor(() => FindFirstObjectByType<HuntZoneHarness>() != null, 30f, "사냥터 하네스 대기");
            if (!m_waitOk) { Check("사냥: 세션 진입", false, "HuntZoneHarness 없음"); yield break; }
            yield return null;

            var huntHarness = FindFirstObjectByType<HuntZoneHarness>();
            var huntState = GetPrivateField<PlayerState>(huntHarness, "m_state");
            var launcher = FindFirstObjectByType<SessionLauncher>();

            // 고정 룸 오염 방지 — 검증 전용 존 이름으로 격리
            string zoneId = $"qa{UnityEngine.Random.Range(1000, 9999)}";
            Task joinTask = launcher.StartHuntZone(zoneId, CancellationToken.None);
            yield return WaitFor(() => joinTask.IsCompleted, 60f, "Photon 세션 접속 대기");
            if (!m_waitOk || joinTask.IsFaulted)
            {
                Check("사냥: 세션 진입 (Fusion Shared)", false,
                    joinTask.IsFaulted ? $"접속 실패: {joinTask.Exception?.GetBaseException().Message}" : "접속 타임아웃");
                yield break;
            }
            var runner = launcher.Runner;
            Check("사냥: 세션 진입 (Fusion Shared)", runner != null && runner.SessionInfo.IsValid,
                $"룸 {runner.SessionInfo.Name}, 인원 {runner.SessionInfo.PlayerCount}");

            // 마스터(=이 클라이언트)가 스폰한 사냥감 대기
            var targets = new List<HuntTarget>();
            yield return WaitFor(() => { targets.Clear(); runner.GetAllBehaviours(targets); return targets.Count > 0; },
                30f, "사냥감 스폰 대기");
            if (!m_waitOk) { Check("사냥: 사냥감 스폰", false, "HuntTarget 스폰 안 됨"); yield break; }

            HuntTarget target = null;
            foreach (HuntTarget t in targets)
                if (t != null && t.Def != null && t.Def.RequiredParticipants > 1) { target = t; break; }
            if (target == null) target = targets[0];
            var ledger = target.GetComponent<HuntLedger>();

            // 더미 피어(같은 프로세스 두 번째 러너) — HuntZoneHarness.StartDummyAsync와 동일 패턴
            var dummyGo = new GameObject("QA_DummyPeer");
            var dummyRunner = dummyGo.AddComponent<NetworkRunner>();
            var dummySceneMgr = dummyGo.AddComponent<NetworkSceneManagerDefault>();
            var dummyArgs = new StartGameArgs
            {
                GameMode     = GameMode.Shared,
                SessionName  = $"hunt_zone_{zoneId}",
                SceneManager = dummySceneMgr,
            };
            NetworkSceneInfo? arenaScene = SessionLauncher.TryGetMultiPeerArenaScene();
            if (arenaScene.HasValue) dummyArgs.Scene = arenaScene.Value;

            Task<StartGameResult> dummyTask = dummyRunner.StartGame(dummyArgs);
            yield return WaitFor(() => dummyTask.IsCompleted, 60f, "더미 피어 접속 대기");
            bool dummyOk = m_waitOk && !dummyTask.IsFaulted && dummyTask.Result.Ok;
            Check("사냥: 2번째 피어 접속", dummyOk,
                dummyOk ? $"더미 P{dummyRunner.LocalPlayer.PlayerId}" : "더미 접속 실패 — 2인 게이트/비권한 검증 불가");

            yield return WaitFor(() => runner.SessionInfo.PlayerCount >= 2, 30f, "2인 인식 대기");

            // 더미 러너 쪽 복제 인스턴스 — 비권한 클라이언트 경로(Defeated/보상 발화) 검증용
            HuntTarget dummyTarget = null;
            if (dummyOk)
            {
                var dummyTargets = new List<HuntTarget>();
                yield return WaitFor(() => { dummyTargets.Clear(); dummyRunner.GetAllBehaviours(dummyTargets); return dummyTargets.Count > 0; },
                    30f, "더미 쪽 복제 대기");
                if (m_waitOk)
                    foreach (HuntTarget t in dummyTargets)
                        if (t != null && t.Def != null && target.Def != null && t.Def.Id == target.Def.Id) { dummyTarget = t; break; }
                if (dummyTarget == null && dummyTargets.Count > 0) dummyTarget = dummyTargets[0];
            }

            int localDefeated = 0, dummyDefeated = 0, rewardCount = 0;
            Action<HuntTarget> onLocalDefeated = _ => localDefeated++;
            Action<HuntTarget> onDummyDefeated = _ => dummyDefeated++;
            Action<HuntTargetDef> onReward = _ => rewardCount++;
            target.Defeated += onLocalDefeated;
            ledger.RewardGranted += onReward;
            if (dummyTarget != null) dummyTarget.Defeated += onDummyDefeated;

            int invBeforeHunt = TotalItems(huntState.Inventory);

            // 1회차 사냥
            bool started = target.TryStartHunt();
            Check("사냥: 2인 게이트 통과 후 시작", started, $"PlayerCount={runner.SessionInfo.PlayerCount}");
            if (started)
            {
                yield return null;
                CaptureShot("hunt_target", target.transform.position, 5f, 1.6f, 300f);
                yield return HitUntilDead(target, runner, dummyTarget, dummyOk ? dummyRunner : null, 90f);
                Check("사냥: 처치 (권한자 감지)", localDefeated >= 1, $"Defeated 발화 {localDefeated}회, HP={target.CurrentHealth}");
                yield return new WaitForSeconds(1f); // 쓰러짐 연출(HuntTargetDeathVisual 0.6s) 완료 후 캡처
                CaptureShot("hunt_defeated", target.transform.position, 5f, 1.6f, 300f);

                bool bothRecorded = ledger.IsParticipant(runner.LocalPlayer) &&
                                    (!dummyOk || ledger.IsParticipant(dummyRunner.LocalPlayer));
                Check("사냥: 기여도 양쪽 기록 (Shared Ledger)", bothRecorded,
                    $"로컬 참여 {ledger.IsParticipant(runner.LocalPlayer)}" +
                    (dummyOk ? $", 더미 참여 {ledger.IsParticipant(dummyRunner.LocalPlayer)}" : " (더미 없음)"));

                if (dummyTarget != null)
                {
                    yield return WaitFor(() => dummyDefeated >= 1, 15f, "비권한 처치 복제 대기");
                    Check("사냥: 처치 (비권한 복제 감지)", dummyDefeated >= 1, $"더미 쪽 Defeated {dummyDefeated}회");
                }
                Check("사냥: 보상 지급·인벤토리 반영", rewardCount >= 1 && TotalItems(huntState.Inventory) > invBeforeHunt,
                    $"RewardGranted {rewardCount}회, 아이템 {invBeforeHunt}→{TotalItems(huntState.Inventory)}");

                // 2회차 재사냥 — 결함1(비권한 m_defeatedFired 미리셋) 수정 검증
                yield return new WaitForSeconds(1.5f);
                bool restarted = target.TryStartHunt();
                if (restarted)
                {
                    yield return HitUntilDead(target, runner, dummyTarget, dummyOk ? dummyRunner : null, 90f);
                    if (dummyTarget != null)
                    {
                        yield return WaitFor(() => dummyDefeated >= 2, 15f, "재사냥 비권한 처치 대기");
                        Check("재사냥: 비권한 처치 재발화 (결함1 수정)", dummyDefeated >= 2, $"더미 쪽 Defeated 누적 {dummyDefeated}회 (기대 2)");
                    }
                    else
                    {
                        Skip("재사냥: 비권한 처치 재발화 (결함1 수정)", "더미 복제 인스턴스 확보 실패 — 실기 2클라이언트로 확인");
                    }
                    Check("재사냥: 보상 재지급", rewardCount >= 2, $"RewardGranted 누적 {rewardCount}회");
                }
                else
                {
                    Check("재사냥: 시작", false, "2회차 TryStartHunt 실패");
                }
            }

            target.Defeated -= onLocalDefeated;
            ledger.RewardGranted -= onReward;
            if (dummyTarget != null) dummyTarget.Defeated -= onDummyDefeated;
            if (dummyRunner != null && dummyRunner.IsRunning) { var _ = dummyRunner.Shutdown(); }
        }

        // ── Phase 2.5: 물리 요리 — HeldItemGrabber의 실제 Grab/Release 내부 경로를 합성 손으로 구동.
        // OVRInput 트리거/조준 읽기(디바이스 입력)만 우회하고 부착·키네마틱·손 점유·투입 트리거·젓기 누적은 실경로다.
        private IEnumerator RunPhysicalCookingPhase(PlayerState estState, CookingPot pot)
        {
            var zone = FindFirstObjectByType<PotInteractionZone>();
            var stir = FindFirstObjectByType<StirTool>();
            var shelf = FindFirstObjectByType<IngredientShelf>();
            bool assembled = pot != null && zone != null && stir != null && shelf != null;
            Check("물리 요리: 조립 (투입존·선반·국자)", assembled,
                $"zone={zone != null}, ladle={stir != null}, shelf={shelf != null}");
            if (!assembled) yield break;

            CaptureShot("cooking_set", pot.transform.position, 3f, 1.2f, 150f);

            var hand = new GameObject("QA_SyntheticHand").transform;
            AddMarker(hand, Color.red); // 스크린샷에서 손 위치 기준점 — 손과 함께 파괴됨

            // ① 선반 프록시를 손으로 집어 냄비 림에 넣는다 (투입 시점 인벤토리 차감 검증)
            IngredientPickup pickup = null;
            yield return WaitFor(() => { pickup = FindShelfPickup(estState); return pickup != null; }, 12f, "선반 프록시 대기");
            if (pickup != null)
            {
                ItemDef item = pickup.Item;
                int invBefore = estState.Inventory.CountOf(item);
                int potBefore = pot.Ingredients.Count;
                ItemDef addedItem = null;
                Action<ItemDef> onAdded = i => addedItem = i;
                pot.IngredientAdded += onAdded;

                hand.SetPositionAndRotation(pickup.transform.position + Vector3.up * 0.05f, Quaternion.identity);
                bool grabOk = InvokeGrab(pickup.GetComponent<HeldItemGrabber>(), hand);
                Check("물리 요리: 프록시 그랩 (손 부착·키네마틱 전환)", grabOk && pickup.transform.parent == hand,
                    grabOk ? "Grab 경로 실행, 손에 부착됨" : "Grab 리플렉션 실패");
                yield return null;
                CaptureShot("held_ingredient", hand.position, 0.8f, 0.12f, 180f);

                // 손을 냄비 림 트리거로 이동 — 든 채 진입(OnTriggerEnter → TryAddIngredient → 프록시 파괴)
                yield return MoveHand(hand, hand.position, zone.transform.position, 1.2f);
                yield return WaitFor(() => pickup == null, 5f, "투입 트리거 대기");

                pot.IngredientAdded -= onAdded;
                Check("물리 요리: 림 트리거 투입 + 투입 시점 차감", addedItem == item &&
                    pot.Ingredients.Count == potBefore + 1 && estState.Inventory.CountOf(item) == invBefore - 1,
                    $"{(item != null ? item.Id : "?")} 투입, 냄비 {potBefore}→{pot.Ingredients.Count}, 인벤 {invBefore}→{estState.Inventory.CountOf(item)}");
            }
            else
            {
                Check("물리 요리: 림 트리거 투입 + 투입 시점 차감", false, "선반 프록시가 스폰되지 않음");
            }

            // ② 국자를 잡고 존 안에서 원형 젓기 — 누적 2.2m(속도 게이트 0.15~5m/s) 도달 시 Cook 발동
            hand.SetPositionAndRotation(stir.transform.position + Vector3.up * 0.05f, Quaternion.identity);
            var ladleGrabber = stir.GetComponent<HeldItemGrabber>();
            bool ladleGrabOk = InvokeGrab(ladleGrabber, hand);

            ItemDef physCooked = null;
            Action<ItemDef> onCookedPhys = r => physCooked = r;
            pot.Cooked += onCookedPhys;

            if (ladleGrabOk && pot.Ingredients.Count > 0)
            {
                Vector3 center = zone.transform.position;
                const float radius = 0.12f;
                const float omega  = 5.5f; // 팁 속도 ≈ 0.66 m/s — 게이트(0.15~5) 안
                float start = Time.time;
                float angle = 0f;
                bool stirShot = false;
                while (physCooked == null && Time.time - start < 30f)
                {
                    // 프레임 드랍 시 한 프레임 이동이 속도 게이트(5m/s) 초과 노이즈로 버려지지 않게 전진량 캡
                    angle += Mathf.Min(Time.deltaTime, 0.05f) * omega;
                    Vector3 targetTip = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                    hand.position += targetTip - stir.Tip.position; // 강체 부착 — 팁 오차만큼 손을 이동
                    if (!stirShot && Time.time - start > 1.5f)
                    {
                        stirShot = true;
                        CaptureShot("cooking_stirring", zone.transform.position, 1.8f, 0.5f, 120f);
                        CaptureShot("held_ladle", hand.position, 0.9f, 0.15f, 200f);
                    }
                    yield return null;
                }
            }
            pot.Cooked -= onCookedPhys;

            Check("물리 요리: 국자 젓기 누적→Cook 발동", physCooked != null,
                physCooked != null ? $"젓기 누적 2.2m 도달 — 결과 {physCooked.Id}" : "30초 내 Cook 미발동");

            if (ladleGrabOk)
            {
                InvokeRelease(ladleGrabber);
                Check("물리 요리: 국자 릴리스 (거치대 복귀)", stir != null && stir.transform.parent != hand,
                    "Release 경로 실행 — 도킹 복귀");
                yield return null;
                CaptureShot("ladle_docked", stir.transform.position, 1.4f, 0.4f, 200f);
            }
            Destroy(hand.gameObject);
        }

        // ── Phase 5: 로비 포탈 UGUI 클릭 — Button.onClick→VRUIButton.Clicked→포탈 핸들러→씬 전환 ──
        private IEnumerator RunPortalUiPhase()
        {
            SceneManager.LoadScene("Lobby");
            yield return WaitFor(() => FindFirstObjectByType<PortalPanelController>() != null, 30f, "로비 포탈 대기");
            if (!m_waitOk) { Check("UI: 포탈 버튼 클릭→씬 전환", false, "PortalPanelController 없음"); yield break; }
            yield return null; // Start/OnEnable(라벨·클릭 구독) 완료 대기

            Button targetButton = null;
            string targetScene = "FishingGround";
            foreach (var portal in FindObjectsByType<PortalPanelController>(FindObjectsSortMode.None))
            {
                var destinations = GetPrivateField<PortalPanelController.Destination[]>(portal, "m_destinations");
                var buttons = GetPrivateField<VRUIButton[]>(portal, "m_buttons");
                if (destinations == null || buttons == null) continue;
                for (int i = 0; i < destinations.Length && i < buttons.Length; i++)
                {
                    if (destinations[i].sceneName != targetScene || buttons[i] == null) continue;
                    targetButton = buttons[i].GetComponentInChildren<Button>(true);
                    break;
                }
                if (targetButton != null) break;
            }

            if (targetButton == null)
            {
                Check("UI: 포탈 버튼 클릭→씬 전환", false, $"{targetScene} 목적지 버튼을 못 찾음");
                yield break;
            }

            CaptureShot("lobby_portal_a", targetButton.transform.position, 2.2f, 0.2f, 0f);
            CaptureShot("lobby_portal_b", targetButton.transform.position, 2.2f, 0.2f, 180f);

            var pointer = new PointerEventData(EventSystem.current);
            ExecuteEvents.Execute(targetButton.gameObject, pointer, ExecuteEvents.pointerClickHandler);

            yield return WaitFor(() => SceneManager.GetActiveScene().name == targetScene, 12f, "포탈 씬 전환 대기");
            Check("UI: 포탈 버튼 클릭→씬 전환 (UGUI 체인)", m_waitOk,
                m_waitOk ? "onClick→Clicked→페이드→LoadScene 완주" : "클릭 후 12초 내 씬 전환 안 됨");
        }

        // 선반 위 살아있는 프록시 중 재고가 남은 첫 항목
        private static IngredientPickup FindShelfPickup(PlayerState state)
        {
            foreach (var pickup in FindObjectsByType<IngredientPickup>(FindObjectsSortMode.None))
            {
                if (pickup == null || pickup.Item == null) continue;
                if (state.Inventory.CountOf(pickup.Item) <= 0) continue;
                if (pickup.GetComponent<HeldItemGrabber>() == null) continue;
                return pickup;
            }
            return null;
        }

        private IEnumerator MoveHand(Transform hand, Vector3 from, Vector3 to, float seconds)
        {
            float start = Time.time;
            while (Time.time - start < seconds)
            {
                hand.position = Vector3.Lerp(from, to, (Time.time - start) / seconds);
                yield return null;
            }
            hand.position = to;
        }

        private static bool InvokeGrab(HeldItemGrabber grabber, Transform hand)
        {
            if (grabber == null) return false;
            MethodInfo method = typeof(HeldItemGrabber).GetMethod("Grab", BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null) return false;
            method.Invoke(grabber, new object[] { hand, OVRInput.Controller.RTouch });
            return true;
        }

        private static void InvokeRelease(HeldItemGrabber grabber)
        {
            if (grabber == null) return;
            MethodInfo method = typeof(HeldItemGrabber).GetMethod("Release", BindingFlags.Instance | BindingFlags.NonPublic);
            method?.Invoke(grabber, null);
        }

        // 양쪽 피어에서 번갈아 타격 — 비권한(더미) 타격은 RPC로 권한자에 위임되는 실경로
        private IEnumerator HitUntilDead(HuntTarget target, NetworkRunner runner, HuntTarget dummyTarget, NetworkRunner dummyRunner, float timeout)
        {
            float start = Time.realtimeSinceStartup;
            bool dummyTurn = false;
            while (target != null && target.CurrentHealth > 0 && Time.realtimeSinceStartup - start < timeout)
            {
                if (dummyTurn && dummyTarget != null && dummyRunner != null)
                    dummyTarget.ApplyHit(dummyRunner.LocalPlayer, 10);
                else
                    target.ApplyHit(runner.LocalPlayer, 10);
                dummyTurn = !dummyTurn;
                yield return new WaitForSeconds(0.25f);
            }
        }

        // ── 판정·리포트 헬퍼 ─────────────────────────────────────────

        private void Check(string name, bool pass, string detail)
        {
            m_report.checks.Add(new CheckResult { name = name, status = pass ? "PASS" : "FAIL", detail = detail });
            if (pass) m_report.pass++; else m_report.fail++;
            Debug.Log($"[P0Verify] {(pass ? "PASS" : "FAIL")} | {name} — {detail}");
        }

        private void Skip(string name, string detail)
        {
            m_report.checks.Add(new CheckResult { name = name, status = "SKIP", detail = detail });
            m_report.skip++;
            Debug.Log($"[P0Verify] SKIP | {name} — {detail}");
        }

        private void Finish(string abortReason)
        {
            if (m_finished) return;
            m_finished = true;

            if (!string.IsNullOrEmpty(abortReason))
                Check("시나리오 완주", false, abortReason);

            string path = Path.Combine(Application.dataPath, "..", "Library", "P0VerifyReport.json");
            File.WriteAllText(path, JsonUtility.ToJson(m_report, prettyPrint: true));
            Debug.Log($"[P0Verify] 완료 — PASS {m_report.pass} / FAIL {m_report.fail} / SKIP {m_report.skip} → {path}");

            SessionState.SetBool(k_sessionFlag, false); // 이후 일반 Play에 재발화 방지
            EditorSceneManager.playModeStartScene = null;

            if (Application.isBatchMode)
                EditorApplication.Exit(m_report.fail > 0 ? 1 : 0);
            else
                EditorApplication.isPlaying = false;
        }

        // ── 시각 검증 — 핵심 순간 스크린샷 (Library/P0VerifyShots). 눈검증은 리포트 소비자(에이전트/사람) 몫 ──

        private void CaptureShot(string name, Vector3 focus, float distance, float height = 0.35f, float yawDegrees = 210f)
        {
            var camGo = new GameObject("QA_ShotCam");
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.02f;
            Vector3 offset = Quaternion.Euler(0f, yawDegrees, 0f) * Vector3.forward * distance + Vector3.up * height;
            cam.transform.position = focus + offset;
            cam.transform.LookAt(focus);

            const int w = 1280, h = 720;
            var rt = new RenderTexture(w, h, 24);
            var prevActive = RenderTexture.active;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;

            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();

            string dir = Path.Combine(Application.dataPath, "..", "Library", "P0VerifyShots");
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            Debug.Log($"[P0Verify] 스크린샷: {name}");

            cam.targetTexture = null;
            RenderTexture.active = prevActive;
            Destroy(tex);
            Destroy(rt);
            Destroy(camGo);
        }

        // 진단 — 기준점 주변의 비정상 대형 비주얼(렌더러·UI 그래픽)을 로그로 나열한다.
        // 스크린샷에서 정체불명 오브젝트가 보일 때 경로를 특정하기 위한 용도.
        private static void LogOversizedVisuals(Vector3 around)
        {
            foreach (var renderer in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                Bounds b = renderer.bounds;
                Vector3 flat = b.center - around; flat.y = 0f;
                if (flat.magnitude > 6f) continue;              // 부두 주변만
                if (b.size.y < 2.0f || b.size.y > 100f) continue; // 지면/호수(납작) 제외, 대형만
                Debug.Log($"[P0Verify][DIAG] Renderer {GetPath(renderer.transform)} size={b.size} center={b.center} mat={(renderer.sharedMaterial != null ? renderer.sharedMaterial.name : "-")}");
            }

            foreach (var graphic in FindObjectsByType<UnityEngine.UI.Graphic>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                var corners = new Vector3[4];
                graphic.rectTransform.GetWorldCorners(corners);
                float width  = Vector3.Distance(corners[0], corners[3]);
                float height = Vector3.Distance(corners[0], corners[1]);
                if (width < 1.5f && height < 1.5f) continue;
                Debug.Log($"[P0Verify][DIAG] Graphic {GetPath(graphic.rectTransform)} w={width:F2}m h={height:F2}m pos={graphic.rectTransform.position} color={graphic.color}");
            }
        }

        private static string GetPath(Transform t)
        {
            string path = t.name;
            while (t.parent != null) { t = t.parent; path = t.name + "/" + path; }
            return path;
        }

        // 손 위치 마커 — 스크린샷에서 "손잡이가 손 위치에 오는가"를 눈으로 판정하기 위한 기준점
        private static GameObject AddMarker(Transform parent, Color color, float scale = 0.035f)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "QA_HandMarker";
            Destroy(marker.GetComponent<Collider>());
            marker.transform.SetParent(parent, false);
            marker.transform.localScale = Vector3.one * scale;
            var renderer = marker.GetComponent<Renderer>();
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor(Shader.PropertyToID("_BaseColor"), color);
            renderer.SetPropertyBlock(mpb);
            return marker;
        }

        // 조건 충족 대기 — 결과는 m_waitOk로 전달 (성공 true / 타임아웃 false)
        private IEnumerator WaitFor(Func<bool> condition, float timeoutSeconds, string label)
        {
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < timeoutSeconds)
            {
                bool ok;
                try { ok = condition(); }
                catch { ok = false; } // 초기화 중 일시 예외는 재시도
                if (ok) { m_waitOk = true; yield break; }
                yield return null;
            }
            m_waitOk = false;
            Debug.LogWarning($"[P0Verify] 대기 타임아웃({timeoutSeconds}s): {label}");
        }

        private static int TotalItems(Inventory inventory)
        {
            int total = 0;
            foreach (KeyValuePair<ItemDef, int> entry in inventory.Items) total += entry.Value;
            return total;
        }

        private static PlayerSaveData ReadSave()
        {
            string path = Path.Combine(Application.persistentDataPath, "player_save.json");
            if (!File.Exists(path)) return null;
            return JsonUtility.FromJson<PlayerSaveData>(File.ReadAllText(path));
        }

        private static int CountInSave(PlayerSaveData data, string itemId)
        {
            int total = 0;
            foreach (ItemStackSave stack in data.Inventory)
                if (stack.Id == itemId) total += stack.Count;
            return total;
        }

        // 씬 냄비가 참조해 로드된 RecipeDef 중 재료·결과가 온전한 첫 항목
        private static RecipeDef PickLoadedRecipe()
        {
            foreach (RecipeDef recipe in Resources.FindObjectsOfTypeAll<RecipeDef>())
            {
                if (recipe == null || recipe.Result == null) continue;
                if (recipe.Ingredients == null || recipe.Ingredients.Length == 0) continue;
                bool valid = true;
                foreach (ItemDef ing in recipe.Ingredients) if (ing == null) { valid = false; break; }
                if (valid) return recipe;
            }
            return null;
        }

        private static T GetPrivateField<T>(object target, string fieldName) where T : class
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            return field != null ? field.GetValue(target) as T : null;
        }

        private static void InvokePrivate(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null) { Debug.LogError($"[P0Verify] 리플렉션 실패: {target.GetType().Name}.{methodName}"); return; }
            method.Invoke(target, args);
        }
#endif
    }
}
