using System.Collections.Generic;
using System.Threading.Tasks;
using CampLantern.Cooking;
using CampLantern.Core;
using CampLantern.Core.Persistence;
using CampLantern.Estate;
using CampLantern.Fishing;
using CampLantern.Hunting;
using CampLantern.Networking;
using CampLantern.Networking.Voice;
using Fusion;
using Meta.XR.MultiplayerBlocks.Fusion;
using UnityEngine;

namespace CampLantern.Bootstrap
{
    /// <summary>
    /// P0 플레이 하네스 — 씬 부트스트랩 + 데스크톱 디버그 UI (IMGUI).
    /// PlayerState(Wallet/Inventory/EstateShop 소유, 로컬 JSON 동기화)를 통해 각 시스템을 배선한다:
    /// FishCaught→Inventory, Cooked 표시, RewardGranted→보상 지급, EstateManager.Bind.
    /// 저장 파일은 Room Scenes(Fishing/HuntZone/EstateTemplate)와 공유 — 서로 오가며 이어짐.
    /// VR 입력 어댑터가 생기면 이 하네스의 버튼이 하던 호출(Cast/Reel/Cook/Place...)을 그쪽으로 옮긴다.
    /// OnGUI는 개발용 임시 — 제품 UI 아님 (Quest 빌드 전 제거 대상, unity-mobile-performance.md).
    /// </summary>
    public class P0Harness : MonoBehaviour
    {
        [Header("씬 시스템 참조 (P0PlaySceneFactory가 배선)")]
        [SerializeField] private FishingRod m_rod;
        [SerializeField] private FishingSpot m_spot;
        [SerializeField] private CookingPot m_pot;
        [SerializeField] private EstateManager m_estateManager;
        [SerializeField] private SessionLauncher m_launcher;

        [Header("데이터/프리팹")]
        [Tooltip("상점 카탈로그 — Assets/Data/Estate의 EstateObjectDef들")]
        [SerializeField] private EstateObjectDef[] m_estateCatalog;
        [Tooltip("사냥감 네트워크 프리팹 — 세션 시작 시 마스터 클라이언트가 스폰")]
        [SerializeField] private NetworkObject m_huntTargetPrefab;

        [Header("테스트 편의값")]
        [Tooltip("시작 코인 — 상점 테스트 편의용 (경제 검증 시 0으로)")]
        [SerializeField] private int m_startingCoins = 100;
        [Tooltip("클릭당 사냥 타격량")]
        [SerializeField] private int m_hitDamage = 10;
        [Tooltip("영지 배치 시작 위치 — 이후 2m 간격 격자로 배치")]
        [SerializeField] private Vector3 m_placeOrigin = new Vector3(6f, 0f, 6f);
        [Tooltip("사냥감 스폰 위치")]
        [SerializeField] private Vector3 m_huntSpawnPos = new Vector3(-8f, 0f, 8f);

        private const string k_zoneId = "p0"; // 세션 룸: hunt_zone_p0 — 본체/더미가 같은 값 사용

        private PlayerState m_state;
        private ContentRegistry m_registry;

        private VoiceController m_voice; // m_launcher와 같은 GO에서 캐싱
        private PlayerMute m_mute;

        private HuntTarget m_huntTarget; // 세션 중 스폰된 사냥감 — Update에서 발견 시 구독
        private HuntLedger m_huntLedger;
        private readonly List<HuntTarget> m_huntTargetsBuffer = new List<HuntTarget>();

        // 더미 피어 — 같은 에디터에서 2번째 플레이어로 같은 룸에 접속 (NetworkProjectConfig PeerMode: Multiple 필요).
        // 협동 게이트/기여/보상 테스트용. 음성은 붙이지 않는다 (마이크 이중 캡처 방지).
        private NetworkRunner m_dummyRunner;
        private bool m_dummyJoining;
        // 더미 러너 시점에 복제되는 실플레이어 아바타 프록시(=내 아바타의 유령 사본) 은폐용 — HideDummyRunnerProxies()에서 사용
        private readonly List<AvatarBehaviourFusion> m_dummyAvatarBuffer = new List<AvatarBehaviourFusion>();
        private readonly List<HuntTarget> m_dummyHuntTargetBuffer = new List<HuntTarget>();
        private GameObject m_dummyPlaceholder; // 더미 "사람" 표시용 로컬 프리미티브 — SpawnDummyPersonAvatar 참조

        /// <summary>접속 중인 더미 피어 러너. 없으면 null. (자동 테스트 메뉴에서 사용)</summary>
        public NetworkRunner DummyRunner => m_dummyRunner;

        /// <summary>테스트/디버그 조회용 — 저장·인벤토리 라운드트립 자동 검증에 사용.</summary>
        public PlayerState State => m_state;

        private bool m_joining;
        private string m_lastLog = "-";
        private Vector2 m_scroll;

        private void Awake()
        {
            m_registry = Resources.Load<ContentRegistry>("ContentRegistry");
            if (m_registry == null)
                Debug.LogError("[P0Harness] ContentRegistry 없음 — Tools > Make Assets > Content Registry 실행 필요");

            bool isNewSave = !SaveService.Exists(); // 최초 실행에만 시작 코인 지급 — 이후엔 저장값이 우선

            m_state = new PlayerState();
            if (m_registry != null) m_state.Load(m_registry);

            if (isNewSave && m_startingCoins > 0) m_state.Wallet.Add(m_startingCoins);

            if (m_launcher != null)
            {
                m_voice = m_launcher.GetComponent<VoiceController>();
                m_mute  = m_launcher.GetComponent<PlayerMute>();
                m_launcher.SessionStarted -= OnSessionStarted;
                m_launcher.SessionStarted += OnSessionStarted;
            }

            if (m_rod != null)
            {
                m_rod.FishCaught -= OnFishCaught;
                m_rod.FishCaught += OnFishCaught;
            }
        }

        private void Start()
        {
            // Push 초기화 원칙 (rules/scripts.md) — 데이터 소유자인 하네스가 준비된 뒤 주입
            if (m_pot != null)
            {
                m_pot.Initialize(m_state.Inventory); // 레시피/실패작은 씬에 세팅된 인스펙터 값 사용
                m_pot.Cooked -= OnCooked;
                m_pot.Cooked += OnCooked;
            }

            if (m_estateManager != null)
            {
                m_estateManager.Bind(m_state.Shop);

                // 저장된 배치를 복원 — 보유 목록(Shop.OwnedDefs)과 별개로 이미 배치된 것만 Place로 재현
                if (m_registry != null)
                {
                    foreach (PlacedObjectSave saved in m_state.PendingPlacements)
                    {
                        if (!m_registry.TryGetEstateObject(saved.DefId, out EstateObjectDef def))
                        {
                            Debug.LogWarning($"[P0Harness] 저장된 배치 오브젝트 Id를 찾을 수 없음: {saved.DefId}");
                            continue;
                        }

                        if (m_estateManager.Place(def, saved.Position, saved.Rotation) == null)
                        {
                            // 수용량 초과 등으로 복원 실패 — 데이터 유실 방지를 위해 보유 목록으로 되돌린다
                            m_state.Shop.ReturnOwned(def);
                            Debug.LogWarning($"[P0Harness] 저장된 배치 복원 실패(수용량 초과 등) — 보유 목록으로 반환: {saved.DefId}");
                        }
                    }
                }
            }
        }

        private void OnDestroy()
        {
            if (m_launcher != null) m_launcher.SessionStarted -= OnSessionStarted;
            if (m_rod != null) m_rod.FishCaught -= OnFishCaught;
            if (m_pot != null) m_pot.Cooked -= OnCooked;
            UnhookHuntTarget();

            // 더미 피어 연결 누수 방지 — await 불가 지점이므로 동기 킥오프만 (SessionLauncher.OnDestroy와 동일 패턴)
            if (m_dummyRunner != null && m_dummyRunner.IsRunning)
                m_dummyRunner.Shutdown();
            m_dummyRunner = null;
            if (m_dummyPlaceholder != null) Destroy(m_dummyPlaceholder);

            m_state?.Save(m_estateManager);
        }

        private void OnApplicationQuit()
        {
            m_state.Save(m_estateManager);
        }

        private void Update()
        {
            // 사냥감은 세션 중 네트워크 스폰이라 씬 참조로 미리 배선 불가 — 등장을 감지해 구독한다.
            // 멀티 피어 모드에서는 더미 러너의 복제 인스턴스도 씬에 있으므로 반드시 본체 러너 스코프로 찾는다.
            if (m_huntTarget == null && m_launcher != null && m_launcher.Runner != null)
            {
                var target = FindHuntTarget(m_launcher.Runner);
                if (target != null) HookHuntTarget(target);
            }

            HideDummyRunnerProxies();
        }

        /// <summary>해당 러너에 속한 사냥감 인스턴스를 찾는다 (러너 스코프 — 씬 전역 검색 금지).</summary>
        public HuntTarget FindHuntTarget(NetworkRunner runner)
        {
            if (runner == null) return null;
            m_huntTargetsBuffer.Clear();
            runner.GetAllBehaviours(m_huntTargetsBuffer);
            return m_huntTargetsBuffer.Count > 0 ? m_huntTargetsBuffer[0] : null;
        }

        // 더미 러너 시점에 복제된 프록시(아바타·사냥감)는 통째로 비활성화한다 (HuntZoneHarness와 동일
        // 패턴 — Renderer만 끄면 Meta Avatar SDK의 RefreshAllActives() 재활성화 경합에 짐). 사냥감도
        // 마스터(실플레이어)만 스폰하므로 더미 러너 스코프에서 발견되는 HuntTarget은 전부 복제본이다 —
        // 아바타 타입만 처리하던 초판은 이걸 놓쳐 더미 추가 시 사냥감이 겹쳐 보이는 원인이 됐다(2026-07-20).
        private void HideDummyRunnerProxies()
        {
            if (m_dummyRunner == null) return;

            m_dummyAvatarBuffer.Clear();
            m_dummyRunner.GetAllBehaviours(m_dummyAvatarBuffer);
            foreach (AvatarBehaviourFusion avatar in m_dummyAvatarBuffer)
            {
                if (avatar == null || !avatar.gameObject.activeSelf) continue;
                avatar.gameObject.SetActive(false);
            }

            m_dummyHuntTargetBuffer.Clear();
            m_dummyRunner.GetAllBehaviours(m_dummyHuntTargetBuffer);
            foreach (HuntTarget target in m_dummyHuntTargetBuffer)
            {
                if (target == null || !target.gameObject.activeSelf) continue;
                target.gameObject.SetActive(false);
            }
        }

        // ── 이벤트 배선 ──────────────────────────────────────────────

        // 정교화 낚시 획득 적용 — fishId→기존 FishDef 매핑(아이콘·판매가 재사용) + 코인 (design/fishing-detailed)
        private void OnFishCaught(FishInstance fish, FishReward reward)
        {
            string displayName = fish.Species.fishId;
            if (m_registry != null && m_registry.TryGetItem(fish.Species.fishId, out ItemDef def))
            {
                m_state.Inventory.Add(def);
                displayName = def.DisplayName;
            }
            m_state.Wallet.Add(reward.coin);
            m_lastLog = $"낚음: {displayName} ({fish.Length:F1}cm)";
            m_state.Save(m_estateManager); // OS 강제종료 대비 — 획득 즉시 저장 (FishingGroundHarness와 동일 정책)
        }

        private void OnCooked(ItemDef result)
        {
            m_lastLog = $"조리 결과: {result.DisplayName}";
            m_state.Save(m_estateManager); // 재료 소모·결과물 반영 즉시 저장
        }

        private void OnSessionStarted(NetworkRunner runner)
        {
            // 사냥감은 마스터 클라이언트만 스폰 — 전원이 스폰하면 중복 생성
            if (m_huntTargetPrefab != null && runner.IsSharedModeMasterClient)
                runner.Spawn(m_huntTargetPrefab, m_huntSpawnPos, Quaternion.identity);
        }

        private void HookHuntTarget(HuntTarget target)
        {
            m_huntTarget = target;
            m_huntLedger = target.GetComponent<HuntLedger>();
            if (m_huntLedger != null)
            {
                m_huntLedger.RewardGranted -= OnRewardGranted;
                m_huntLedger.RewardGranted += OnRewardGranted;
            }
        }

        private void UnhookHuntTarget()
        {
            if (m_huntLedger != null) m_huntLedger.RewardGranted -= OnRewardGranted;
            m_huntTarget = null;
            m_huntLedger = null;
        }

        private void OnRewardGranted(HuntTargetDef def)
        {
            if (def.RewardMaterials == null) return;
            foreach (ItemDef material in def.RewardMaterials)
                m_state.Inventory.Add(material);
            m_lastLog = $"사냥 보상 지급: {def.DisplayName}";
            m_state.Save(m_estateManager); // OS 강제종료 대비 — 보상 즉시 저장
        }

        private async Task JoinSessionAsync()
        {
            m_joining = true;
            try
            {
                await m_launcher.StartHuntZone(k_zoneId, destroyCancellationToken);
                m_lastLog = "세션 접속 완료";
            }
            catch (System.OperationCanceledException) { /* 파괴로 인한 취소 — 정상 */ }
            catch (System.Exception e)
            {
                m_lastLog = $"세션 접속 실패: {e.Message}";
                Debug.LogException(e, this);
            }
            finally
            {
                m_joining = false;
            }
        }

        /// <summary>더미 피어 접속 시작 (fire-and-forget). 본체 세션이 열려 있을 때만 의미 있음.</summary>
        public void AddDummyPeer()
        {
            if (m_dummyRunner != null || m_dummyJoining) return;
            _ = StartDummyAsync();
        }

        /// <summary>더미 피어 접속 해제 (fire-and-forget).</summary>
        public void RemoveDummyPeer()
        {
            var runner = m_dummyRunner;
            m_dummyRunner = null;
            if (runner != null)
                _ = runner.Shutdown(); // destroyGameObject 기본 true — DummyPeer GO째 제거
            if (m_dummyPlaceholder != null) Destroy(m_dummyPlaceholder);
        }

        private async Task StartDummyAsync()
        {
            m_dummyJoining = true;
            try
            {
                // SessionLauncher를 재사용하지 않는 이유: 런처는 자기 GO에 러너를 붙이는 단일 세션 설계라
                // 더미는 별도 GO에 직접 러너를 구성한다. 룸 이름 규칙만 동일하게 맞춘다 (hunt_zone_{zoneId}).
                var go = new GameObject("DummyPeer");
                var runner = go.AddComponent<NetworkRunner>();
                var sceneManager = go.AddComponent<NetworkSceneManagerDefault>();

                var args = new StartGameArgs
                {
                    GameMode                   = GameMode.Shared,
                    SessionName                = $"hunt_zone_{k_zoneId}",
                    SceneManager               = sceneManager,
                    StartGameCancellationToken = destroyCancellationToken,
                };

                // 멀티 피어 모드는 시작 씬 필수 — 본체와 같은 규칙 (SessionLauncher 헬퍼 참조)
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
                SpawnDummyPersonAvatar(runner);
            }
            catch (System.OperationCanceledException) { /* 파괴로 인한 취소 — 정상 */ }
            catch (System.Exception e)
            {
                m_lastLog = $"더미 접속 실패: {e.Message}";
                Debug.LogException(e, this);
            }
            finally
            {
                m_dummyJoining = false;
            }
        }

        // 더미 러너용 "사람" 표시를 고정 위치에 세운다 — Meta Avatar SDK(FusionAvatarSdk28Plus)로 4번 시도
        // 했으나 전부 실패해 로컬 프리미티브 플레이스홀더로 대체(2026-07-20 결정, tech-stack-decisions.md
        // §더미 러너 참조). Fusion NetworkObject가 아니라 순수 로컬 GameObject — 네트워크 상태 검증(이 더미
        // 기능의 원래 목적)에는 영향 없고, 시야에 "누가 있다"만 표시하면 되므로 네트워크 동기화 불필요.
        //
        // 캡슐 하나짜리 몸통(1차 시도)은 사람이 아니라 파란 막대기로 보였다(2026-07-20 사용자 재확인) —
        // 머리·몸통·팔다리를 색으로 구분해 사람 실루엣이 드러나게 다시 만든다.
        private void SpawnDummyPersonAvatar(NetworkRunner dummyRunner)
        {
            Vector3 dummyPos = transform.position + new Vector3(1.5f, 0f, 1.5f);

            m_dummyPlaceholder = new GameObject("DummyPlaceholder(사람 표시)");
            m_dummyPlaceholder.transform.position = dummyPos;

            var skin = new Color(0.94f, 0.78f, 0.63f);
            var shirt = new Color(0.3f, 0.6f, 1f);
            var pants = new Color(0.2f, 0.25f, 0.4f);

            AddDummyPart(PrimitiveType.Sphere, "Head", new Vector3(0f, 1.65f, 0f), new Vector3(0.22f, 0.24f, 0.22f), skin);
            AddDummyPart(PrimitiveType.Capsule, "Torso", new Vector3(0f, 1.22f, 0f), new Vector3(0.42f, 0.32f, 0.24f), shirt);
            AddDummyPart(PrimitiveType.Capsule, "ArmL", new Vector3(-0.28f, 1.15f, 0f), new Vector3(0.12f, 0.3f, 0.12f), shirt);
            AddDummyPart(PrimitiveType.Capsule, "ArmR", new Vector3(0.28f, 1.15f, 0f), new Vector3(0.12f, 0.3f, 0.12f), shirt);
            AddDummyPart(PrimitiveType.Capsule, "LegL", new Vector3(-0.12f, 0.45f, 0f), new Vector3(0.15f, 0.45f, 0.15f), pants);
            AddDummyPart(PrimitiveType.Capsule, "LegR", new Vector3(0.12f, 0.45f, 0f), new Vector3(0.15f, 0.45f, 0.15f), pants);
        }

        private void AddDummyPart(PrimitiveType type, string partName, Vector3 localPos, Vector3 localScale, Color color)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = partName;
            part.transform.SetParent(m_dummyPlaceholder.transform, false);
            part.transform.localPosition = localPos;
            part.transform.localScale = localScale;
            Destroy(part.GetComponent<Collider>());
            part.GetComponent<Renderer>().sharedMaterial.color = color;
        }

        // ── 디버그 UI (개발용 IMGUI) ─────────────────────────────────

        private void OnGUI()
        {
            m_scroll = GUILayout.BeginScrollView(m_scroll, GUILayout.Width(340), GUILayout.Height(Screen.height - 20));

            GUILayout.Label($"[Camp Lantern P0]  코인: {m_state.Wallet.Coins}  |  {m_lastLog}");
            if (GUILayout.Button("저장")) { m_state.Save(m_estateManager); m_lastLog = "저장 완료"; }
            DrawFishing();
            DrawInventory();
            DrawCooking();
            DrawEstate();
            DrawNetwork();

            GUILayout.EndScrollView();
        }

        // 미끼 구매/수리 — 코인 싱크 배선 (economy.md, 가격은 §9 FishingFormulas 경유)
        private void BuyBait(int quantity)
        {
            int price = FishingFormulas.BaitPrice() * quantity;
            if (!m_state.Wallet.TrySpend(price)) { m_lastLog = $"미끼 구매 실패 — 코인 부족 ({price}c 필요)"; return; }
            m_rod.AddBait(quantity);
            m_lastLog = $"미끼 {quantity}개 구매 (-{price}c)";
            m_state.Save(m_estateManager);
        }

        private void RepairRod()
        {
            int price = FishingFormulas.RepairPrice(m_rod.Rod.durability);
            if (!m_state.Wallet.TrySpend(price)) { m_lastLog = $"수리 실패 — 코인 부족 ({price}c 필요)"; return; }
            m_rod.Repair();
            m_lastLog = $"낚싯대 수리 완료 (-{price}c)";
            m_state.Save(m_estateManager);
        }

        private void DrawFishing()
        {
            GUILayout.Space(8);
            Fish fish = m_rod.CurrentFish;
            GUILayout.Label($"── 낚시 ── 미끼 {m_rod.BaitCount} · 내구도 {m_rod.Rod.durability}/{m_rod.Rod.maxDurability}" +
                            (fish != null ? $" · {fish.State}/{fish.Line} · HP {fish.Health:F1} · 텐션 {fish.Tension:F1}" : " · 대기"));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button($"미끼x5 ({FishingFormulas.BaitPrice() * 5}c)"))
                BuyBait(5);
            if (m_rod.Rod.durability < m_rod.Rod.maxDurability &&
                GUILayout.Button($"수리 ({FishingFormulas.RepairPrice(m_rod.Rod.durability)}c)"))
                RepairRod();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (fish == null)
            {
                if (GUILayout.Button("캐스팅 (최근접)") &&
                    m_spot.TryGetNearestFish(m_rod.transform.position, m_rod.Rod.length, out Fish target))
                    m_rod.Cast(target);
            }
            else
            {
                if (GUILayout.Button("챔질")) m_rod.Chamjil();
                if (GUILayout.Button(m_rod.Reeling ? "릴링 중지" : "릴링")) m_rod.SetReeling(!m_rod.Reeling);
                if (GUILayout.Button("스윙")) fish.OnSwing();
                if (GUILayout.Button("낚아올림")) fish.Hook();
            }
            GUILayout.EndHorizontal();
        }

        private void DrawInventory()
        {
            GUILayout.Space(8);
            GUILayout.Label("── 인벤토리 ── (투입=냄비, 판매=코인)");
            // 버튼 콜백 중 Inventory가 변해 Dictionary가 수정되므로 스냅샷 순회
            foreach (KeyValuePair<ItemDef, int> entry in new List<KeyValuePair<ItemDef, int>>(m_state.Inventory.Items))
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{entry.Key.DisplayName} x{entry.Value}", GUILayout.Width(140));
                if (GUILayout.Button("투입", GUILayout.Width(60)))
                    m_pot.TryAddIngredient(entry.Key);
                if (GUILayout.Button($"판매 {entry.Key.SellPrice}c", GUILayout.Width(90)) &&
                    m_state.Inventory.TryRemove(entry.Key))
                {
                    m_state.Wallet.Add(entry.Key.SellPrice);
                    m_state.Save(m_estateManager); // 판매 즉시 저장
                }
                GUILayout.EndHorizontal();
            }
        }

        private void DrawCooking()
        {
            GUILayout.Space(8);
            var names = new List<string>();
            foreach (ItemDef ingredient in m_pot.Ingredients) names.Add(ingredient.DisplayName);
            GUILayout.Label($"── 요리 ── 냄비: [{string.Join(", ", names)}]");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("조리")) m_pot.Cook();
            if (GUILayout.Button("비우기")) m_pot.Clear();
            GUILayout.EndHorizontal();
        }

        private void DrawEstate()
        {
            GUILayout.Space(8);
            GUILayout.Label($"── 영지 ── 수용량: {m_estateManager.CapacityUsed}/{m_estateManager.CapacityMax}");
            foreach (EstateObjectDef def in m_estateCatalog)
            {
                if (def == null) continue;
                GUILayout.BeginHorizontal();
                string material = def.RequiredMaterial != null
                    ? $" + {def.RequiredMaterial.DisplayName} x{def.RequiredMaterialCount}" : "";
                GUILayout.Label($"{def.DisplayName} ({def.CoinCost}c{material})", GUILayout.Width(180));
                if (def.Rarity == Rarity.Epic)
                {
                    GUILayout.Label("이벤트 전용"); // 에픽은 상점 구매 불가 (estate-system.md)
                }
                else
                {
                    if (GUILayout.Button("구매", GUILayout.Width(50)))
                    {
                        bool purchased = m_state.Shop.TryPurchase(def);
                        m_lastLog = purchased ? $"구매: {def.DisplayName}" : "구매 실패 (재화 부족)";
                        if (purchased) m_state.Save(m_estateManager); // 구매 즉시 저장
                    }
                    int owned = m_state.Shop.CountOwned(def);
                    if (owned > 0 && GUILayout.Button($"배치({owned})", GUILayout.Width(70)))
                        TryPlace(def);
                }
                GUILayout.EndHorizontal();
            }

            if (m_estateManager.PlacedObjects.Count > 0 && GUILayout.Button("마지막 배치물 회수"))
            {
                m_estateManager.Remove(m_estateManager.PlacedObjects[m_estateManager.PlacedObjects.Count - 1]);
                m_state.Save(m_estateManager); // 회수(보유 반환) 즉시 저장
            }
        }

        private void TryPlace(EstateObjectDef def)
        {
            if (!m_estateManager.CanPlace(def))
            {
                m_lastLog = "배치 실패 — 수용량 초과";
                return;
            }
            if (!m_state.Shop.TryConsumeOwned(def)) return;

            // 2m 간격 격자에 순서대로 배치 (P0 자유 배치 — 스냅은 P1)
            int index = m_estateManager.PlacedObjects.Count;
            Vector3 pos = m_placeOrigin + new Vector3((index % 4) * 2f, 0f, (index / 4) * 2f);
            PlacedObject placed = m_estateManager.Place(def, pos, Quaternion.identity);
            if (placed == null)
            {
                m_state.Shop.ReturnOwned(def); // CanPlace 통과 후 실패는 도달 불가 — 방어적 반환
            }
            else
            {
                m_lastLog = $"배치: {def.DisplayName}";
                m_state.Save(m_estateManager); // 배치 즉시 저장
            }
        }

        private void DrawNetwork()
        {
            GUILayout.Space(8);
            NetworkRunner runner = m_launcher != null ? m_launcher.Runner : null;

            if (runner == null)
            {
                GUILayout.Label("── 네트워크 ── 미접속");
                GUI.enabled = !m_joining && m_launcher != null;
                if (GUILayout.Button(m_joining ? "접속 중..." : "사냥터 접속 (hunt_zone_p0)"))
                    _ = JoinSessionAsync();
                GUI.enabled = true;
                return;
            }

            GUILayout.Label($"── 네트워크 ── 접속: {runner.SessionInfo.Name} ({runner.SessionInfo.PlayerCount}명)");

            // 음성
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

            // 더미 피어 — 같은 룸 2번째 접속으로 협동 게이트/기여/보상 로컬 테스트 (음성 없음)
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
                HuntTarget dummyTarget = FindHuntTarget(m_dummyRunner);
                if (dummyTarget != null)
                {
                    // 더미의 행동은 반드시 더미 러너의 복제 인스턴스 + 더미 PlayerRef로 — 실제 원격 경로(RPC) 검증
                    if (GUILayout.Button("더미 타격"))
                        dummyTarget.ApplyHit(m_dummyRunner.LocalPlayer, m_hitDamage);
                    var dummyLedger = dummyTarget.GetComponent<HuntLedger>();
                    if (dummyLedger != null && GUILayout.Button("더미 유인"))
                        dummyLedger.RecordContribution(m_dummyRunner.LocalPlayer, HuntLedger.ContributionKind.Lure);
                }
                if (GUILayout.Button("더미 제거"))
                    RemoveDummyPeer();
                GUILayout.EndHorizontal();
            }

            // 사냥
            if (m_huntTarget == null)
            {
                GUILayout.Label("사냥감 스폰 대기 중...");
                return;
            }

            GUILayout.Label($"사냥감 HP: {m_huntTarget.CurrentHealth}  진행중: {m_huntTarget.HuntActive}");
            GUILayout.BeginHorizontal();
            if (m_huntTarget.Object.HasStateAuthority)
            {
                if (GUILayout.Button("사냥 시작"))
                    m_lastLog = m_huntTarget.TryStartHunt() ? "사냥 시작!" : "시작 불가 (2인 미만)";
            }
            else
            {
                GUILayout.Label("(시작은 마스터만)", GUILayout.Width(110));
            }
            if (GUILayout.Button("타격"))
                m_huntTarget.ApplyHit(runner.LocalPlayer, m_hitDamage);
            if (m_huntLedger != null && GUILayout.Button("유인(기여)"))
                m_huntLedger.RecordContribution(runner.LocalPlayer, HuntLedger.ContributionKind.Lure);
            GUILayout.EndHorizontal();
        }
    }
}
