#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static CampLantern.EditorTools.MapBuildUtil;

namespace CampLantern.EditorTools
{
    /// <summary>
    /// 활동 공간 씬(낚시터/사냥터/영지) VR 맵 구성기 — 로비는 <see cref="LobbyMapFactory"/> 담당.
    /// 공용 조립 헬퍼는 <see cref="MapBuildUtil"/>.
    ///
    /// 설계 근거 (GDD):
    ///   - 낚시터: 호숫가 캠프 — 기존 Pond(게임플레이)를 중심으로 모래톱·부두·갈대. 맑은 아침 무드.
    ///   - 사냥터: 그림 2 "존 분할 개념도"의 존A = 숲 — 사냥감 스폰 공터(x±8, z 0~15)는 비우고
    ///     둘레를 숲으로 두른다. 늦은 오후 무드.
    ///   - 영지: 그림 3 "영지 레이아웃 목업" 그대로 — 울타리 필지, 남쪽 입구→중앙 캠프파이어 소셜존
    ///     길+랜턴, 쉘터존(좌상)·트로피룸(우상)·방명록(좌하)·전시 슬롯(우하)·동쪽 확장 예정 구역.
    ///   - 공통: PlayerSpawnPoint + 로비 귀환 관문(그림 1 로비=회색) + VRUIEventSystem.
    ///
    /// idempotent: 환경/관문 루트는 지우고 재구성, 스폰·EventSystem·게임플레이 오브젝트
    /// (Pond/FishingSpot/CookingPot/사냥감 스폰 등)는 건드리지 않는다.
    /// RoomScenesFactory.CreateXxx가 씬 생성 시 공통 호출한다.
    /// </summary>
    public static class RoomMapsFactory
    {
        private const string k_fishingScenePath = "Assets/Scenes/FishingGround.unity";
        private const string k_huntScenePath    = "Assets/Scenes/HuntZone_A.unity";
        private const string k_estateScenePath  = "Assets/Scenes/EstateTemplate.unity";

        // ── 메뉴 ─────────────────────────────────────────────────────

        [MenuItem("Tools/Make Assets/Build Fishing Ground Map (VR)")]
        public static void BuildFishing() => BuildScene(k_fishingScenePath, BuildFishingGroundIntoOpenScene, "낚시터");

        [MenuItem("Tools/Make Assets/Build Hunt Zone Map (VR)")]
        public static void BuildHunt() => BuildScene(k_huntScenePath, BuildHuntZoneIntoOpenScene, "사냥터(존A 숲)");

        [MenuItem("Tools/Make Assets/Build Estate Map (VR)")]
        public static void BuildEstate() => BuildScene(k_estateScenePath, BuildEstateIntoOpenScene, "영지");

        [MenuItem("Tools/Make Assets/Build All Room Maps (VR)")]
        public static void BuildAll()
        {
            BuildFishing();
            BuildHunt();
            BuildEstate();
        }

        private static void BuildScene(string scenePath, System.Action buildAction, string label)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
            {
                Debug.LogError($"[MakeAssets] 씬 없음: {scenePath} — 먼저 Tools > Make Assets > Room Scenes (Create All) 실행");
                return;
            }

            EditorSceneManager.SaveOpenScenes();
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            buildAction();
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[MakeAssets] {label} VR 맵 구성 완료: {scenePath}");
        }

        // ── 낚시터 — 호숫가 캠프 (맑은 아침) ─────────────────────────

        public static void BuildFishingGroundIntoOpenScene()
        {
            DestroyRootIfExists("FishingEnvironment");
            Transform env = new GameObject("FishingEnvironment").transform;

            TextureGround("ground_grass", "#567455");
            PolishPondWater(); // 투명·고광택 수면

            // 모래톱 — Pond(plane 10x10, 중심 (0,0.02,2)) 밑에 깔리는 원반, 물가 테두리 연출
            Part(env, PrimitiveType.Cylinder, new Vector3(0f, 0.004f, 2f),
                 new Vector3(13.6f, 0.008f, 13.6f), "#c9b98a", name: "SandRim");

            // 부두 — FishingRod(0,0,-1)가 서는 자리에서 연못 쪽으로
            Transform dock = Group(env, "Dock", new Vector3(0f, 0f, -0.4f));
            Part(dock, PrimitiveType.Cube, new Vector3(0f, 0.06f, 0f), new Vector3(1.4f, 0.04f, 3.2f), Wood, name: "Deck");
            Part(dock, PrimitiveType.Cube, new Vector3(0f, 0.061f, 0f), new Vector3(1.42f, 0.038f, 0.06f), WoodDark, name: "DeckSeam");
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    Part(dock, PrimitiveType.Cylinder, new Vector3(sx * 0.62f, -0.15f, sz * 1.45f),
                         new Vector3(0.1f, 0.3f, 0.1f), WoodDark2, name: "Pile");

            // 갈대 군락 — 연못 가장자리
            Vector3[] reedSpots = { new Vector3(-4.6f, 0f, 5.8f), new Vector3(4.8f, 0f, 5.2f),
                                    new Vector3(-5.2f, 0f, 0.2f), new Vector3(5.4f, 0f, 1.0f), new Vector3(2.8f, 0f, 6.4f) };
            foreach (var spot in reedSpots)
            {
                Transform reeds = Group(env, "Reeds", spot);
                for (int i = 0; i < 3; i++)
                {
                    float h = 0.45f + i * 0.12f;
                    Vector3 off = new Vector3((i - 1) * 0.12f, h, (i % 2) * 0.1f);
                    Part(reeds, PrimitiveType.Cylinder, off, new Vector3(0.03f, h, 0.03f), "#7a9a4a");
                    Part(reeds, PrimitiveType.Capsule, off + new Vector3(0f, h * 0.95f, 0f),
                         new Vector3(0.05f, 0.09f, 0.05f), "#6a4a25", name: "Cattail");
                }
            }

            // 물가 바위 (실물 프리팹 변주) — 연못·부두 밖 물가에만
            PlaceRock(env, new Vector3(-6.4f, 0f, 3.2f), 1.2f);
            PlaceRock(env, new Vector3(6.6f, 0f, 4.2f),  0.8f);
            PlaceRock(env, new Vector3(3.0f, 0f, -4.0f), 0.45f);

            // 낚시 캠프 (서쪽) + 부두 입구 랜턴
            BuildCampfire(env, new Vector3(-7f, 0f, -5f), withSeats: true);
            BuildTent(env, new Vector3(-9.5f, 0f, -3.2f), yaw: 120f, scale: 1.5f);
            BuildLanternPost(env, new Vector3(1.7f, 0f, -3.9f)); // 연못 수면(z≥-3) 밖 마른 땅

            // 캠프 소품 + 지피식물 — 연못(중심 (0,2) 반경 ~6.8 모래톱) 안으로 침범하지 않는 범위만
            PlaceEnv(env, "storage_barrel_fish", new Vector3(-1.7f, 0f, -3.8f), yaw: 20f, scale: 0.55f); // 부두 서쪽 마른 땅 (원본이 부두보다 큼)
            PlaceEnv(env, "storage_basket",      new Vector3(-6.0f, 0f, -3.6f), yaw: 70f);
            ScatterGroundCover(env, new Vector3(0f, 0f, -9f),   4.2f, 40, seed: 7161);
            ScatterGroundCover(env, new Vector3(-10.5f, 0f, 2f), 3.2f, 15, seed: 7162);
            ScatterGroundCover(env, new Vector3(10.5f, 0f, 2f),  3.2f, 15, seed: 7163);

            // 둘레 숲 (연못 남쪽/동서 가장자리)
            Vector3[] trees = { new Vector3(-13f, 0f, 3f), new Vector3(13f, 0f, 4f), new Vector3(-11f, 0f, -8f),
                                new Vector3(11f, 0f, -9f), new Vector3(-14f, 0f, -3f), new Vector3(14f, 0f, -2f),
                                new Vector3(7f, 0f, -13f), new Vector3(-7f, 0f, -14f), new Vector3(15f, 0f, 9f),
                                new Vector3(-15f, 0f, 10f) };
            for (int i = 0; i < trees.Length; i++)
                BuildTree(env, trees[i], 0.95f + (i % 3) * 0.15f);

            EnsureSpawnPoint(new Vector3(0f, 0f, -5f), Quaternion.identity); // +Z = 부두/연못 방향

            // 로비 귀환 관문 (스폰 뒤)
            DestroyRootIfExists("PortalGates");
            Transform gates = new GameObject("PortalGates").transform;
            BuildLobbyReturnGate(gates, new Vector3(0f, 0f, -9f), new Vector3(0f, 0f, -5f));
            EnsureEventSystem();

            // 맑은 아침 — 높은 해, 푸른 하늘, 옅은 물안개
            ConfigureSun(new Vector3(48f, -30f, 0f), "#fff2dd", 1.05f);
            ApplySkybox("Mat_FishingSky", 0.9f, 1.2f, "#7fa3c8", "#3d4a42");
            ConfigureLinearFog("#a7bfd0", 25f, 70f);
            PlacePreviewCamera(new Vector3(0f, 2f, -7f), new Vector3(8f, 0f, 0f));
        }

        // ── 사냥터 — 존A 숲 (늦은 오후) ──────────────────────────────

        public static void BuildHuntZoneIntoOpenScene()
        {
            DestroyRootIfExists("HuntZoneEnvironment");
            Transform env = new GameObject("HuntZoneEnvironment").transform;

            TextureGround("ground_grass", "#55603f", 20f, "#cfd8c2"); // 숲그늘 톤 다운

            // 둘레 숲 — 사냥감 스폰 공터(x±8, z 0~15)와 스폰 접근로(z<0 중앙)는 비운다
            Vector3[] trees =
            {
                new Vector3(-12f, 0f, 8f),  new Vector3(12f, 0f, 8f),   new Vector3(-15f, 0f, 2f),
                new Vector3(15f, 0f, 2f),   new Vector3(-10f, 0f, 14f), new Vector3(10f, 0f, 14f),
                new Vector3(-13f, 0f, -5f), new Vector3(13f, 0f, -5f),  new Vector3(-16f, 0f, -10f),
                new Vector3(16f, 0f, -9f),  new Vector3(-4f, 0f, 17f),  new Vector3(4f, 0f, 17f),
                new Vector3(-9f, 0f, 18f),  new Vector3(9f, 0f, 18f),   new Vector3(0f, 0f, 19f),
                new Vector3(-17f, 0f, 12f), new Vector3(17f, 0f, 11f),  new Vector3(7f, 0f, -14f),
                new Vector3(-7f, 0f, -15f),
            };
            for (int i = 0; i < trees.Length; i++)
                BuildTree(env, trees[i], 1.1f + (i % 3) * 0.18f); // 숲이라 로비보다 크게

            // 공터 가장자리 디테일 — 쓰러진 통나무·그루터기·바위·수풀
            Part(env, PrimitiveType.Cylinder, new Vector3(8.5f, 0.3f, 2f), new Vector3(0.35f, 1.6f, 0.35f),
                 WoodDark2, new Vector3(0f, 20f, 90f), name: "FallenLog");
            PlaceEnv(env, "Pine_stump",           new Vector3(-7.5f, 0f, 12.5f), yaw: 80f);
            PlaceEnv(env, "Deciduous_tree_stump", new Vector3(6.8f, 0f, 13f),    yaw: 200f);
            PlaceRock(env, new Vector3(-8.8f, 0f, 3.5f), 1.4f);
            PlaceRock(env, new Vector3(9.5f, 0f, 10f),   1.0f);
            PlaceRock(env, new Vector3(-9.2f, 0f, 11f),  0.9f);
            PlaceBush(env, new Vector3(-8f, 0f, 7f),  1.3f);
            PlaceBush(env, new Vector3(8.2f, 0f, 6f), 1.1f);
            PlaceBush(env, new Vector3(3f, 0f, -4f),  1.0f);

            // 숲 바닥 지피식물 (버섯·양치 포함 — 공터 중앙까지 옅게)
            ScatterGroundCover(env, new Vector3(0f, 0f, 7f),   11f, 70, seed: 7164);
            ScatterGroundCover(env, new Vector3(0f, 0f, -8f),  6f,  25, seed: 7165);

            // 사냥 베이스캠프 (남서) — 모닥불·텐트·랜턴
            BuildCampfire(env, new Vector3(-6.5f, 0f, -5f), withSeats: false);
            BuildTent(env, new Vector3(-8.5f, 0f, -4f), yaw: 110f, scale: 1.5f);
            BuildLanternPost(env, new Vector3(-5f, 0f, -5.8f));

            // 존A 숲 깃발 (그림 2 존 테마 표식)
            Transform flag = Group(env, "ZoneFlag", new Vector3(2.2f, 0f, -4f));
            Part(flag, PrimitiveType.Cylinder, new Vector3(0f, 1.5f, 0f),   new Vector3(0.06f, 1.5f, 0.06f), WoodDark);
            Part(flag, PrimitiveType.Cube,     new Vector3(0.3f, 2.7f, 0f), new Vector3(0.55f, 0.32f, 0.03f), "#567a44", name: "Pennant");

            EnsureSpawnPoint(new Vector3(0f, 0f, -8f), Quaternion.identity); // +Z = 사냥 공터 방향

            // 로비 귀환 관문 (스폰 뒤)
            DestroyRootIfExists("PortalGates");
            Transform gates = new GameObject("PortalGates").transform;
            BuildLobbyReturnGate(gates, new Vector3(0f, 0f, -12f), new Vector3(0f, 0f, -8f));
            EnsureEventSystem();

            // 늦은 오후 숲 — 낮은 해, 짙은 초록 안개
            ConfigureSun(new Vector3(33f, -25f, 0f), "#ffe3b8", 0.9f);
            ApplySkybox("Mat_HuntSky", 1.15f, 1.05f, "#8a8f6a", "#39402f");
            ConfigureLinearFog("#75806a", 12f, 40f);
            PlacePreviewCamera(new Vector3(0f, 2.2f, -10f), new Vector3(10f, 0f, 0f));
        }

        // ── 영지 — 그림 3 레이아웃 목업 (포근한 초저녁) ──────────────

        public static void BuildEstateIntoOpenScene()
        {
            DestroyRootIfExists("EstateEnvironment");
            Transform env = new GameObject("EstateEnvironment").transform;

            TextureGround("ground_grass", "#63804d");

            // 필지 울타리 (x ±12, z ±8) — 남쪽 중앙 입구 개구부
            BuildFence(env);

            // 중앙 캠프파이어 소셜존 (그림 3 중앙) — CookingPot(0,0,1)이 바로 옆
            BuildCampfire(env, Vector3.zero, withSeats: true);

            // 입구(남쪽 중앙)→소셜존 길 + 길가 랜턴 3개 (그림 3의 노란 점)
            LayPathStones(env, new Vector3(0f, 0f, -8f), new Vector3(0f, 0f, 0f));
            BuildLanternPost(env, new Vector3(0.9f, 0f, -6f));
            BuildLanternPost(env, new Vector3(0.9f, 0f, -3.5f));
            BuildLanternPost(env, new Vector3(0.9f, 0f, -1f));

            // 쉘터존 (좌상)
            BuildTent(env, new Vector3(-8f, 0f, 5f), yaw: 150f, scale: 1.8f);

            // 트로피룸 (우상) — 지붕 얹은 전시대 + 보라 현판 + 뿔 장식 (그림 3 보라 박스)
            Transform trophy = Group(env, "TrophyRoom", new Vector3(8f, 0f, 5f));
            trophy.localRotation = Quaternion.Euler(0f, 200f, 0f); // 정면이 소셜존을 향하게
            Part(trophy, PrimitiveType.Cube, new Vector3(0f, 0.15f, 0f), new Vector3(3f, 0.15f, 2.4f), Wood, name: "Floor");
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    Part(trophy, PrimitiveType.Cylinder, new Vector3(sx * 1.3f, 1.05f, sz * 1.0f),
                         new Vector3(0.09f, 0.9f, 0.09f), WoodDark, name: "Post");
            Part(trophy, PrimitiveType.Cube, new Vector3(0f, 2.0f, 0f),    new Vector3(3.4f, 0.12f, 2.8f), WoodDark2, name: "Roof");
            Part(trophy, PrimitiveType.Cube, new Vector3(0f, 1.62f, -1.0f), new Vector3(2.2f, 0.32f, 0.05f), "#9a86c4", name: "Banner");
            Part(trophy, PrimitiveType.Cylinder, new Vector3(-0.12f, 2.25f, 0f), new Vector3(0.05f, 0.22f, 0.05f), "#d9cba8", new Vector3(0f, 0f, 35f),  name: "Antler");
            Part(trophy, PrimitiveType.Cylinder, new Vector3(0.12f, 2.25f, 0f),  new Vector3(0.05f, 0.22f, 0.05f), "#d9cba8", new Vector3(0f, 0f, -35f), name: "Antler");

            // 방명록 (좌하) — 스탠드 + 펼친 책 (그림 3 크림 박스)
            Transform guestbook = Group(env, "Guestbook", new Vector3(-8f, 0f, -5f));
            guestbook.localRotation = Quaternion.Euler(0f, 40f, 0f); // 입구 쪽을 향하게
            Part(guestbook, PrimitiveType.Cylinder, new Vector3(0f, 0.55f, 0f), new Vector3(0.09f, 0.55f, 0.09f), WoodDark, name: "Stand");
            Part(guestbook, PrimitiveType.Cube, new Vector3(0f, 1.12f, 0f), new Vector3(0.5f, 0.05f, 0.38f), "#f2e3c2", new Vector3(-18f, 0f, 0f), name: "Book");
            Part(guestbook, PrimitiveType.Cube, new Vector3(0f, 1.14f, 0f), new Vector3(0.03f, 0.06f, 0.38f), CanvasDk, new Vector3(-18f, 0f, 0f), name: "BookSpine");

            // 전시 슬롯 3개 (우하 — 수집품 받침대, 그림 3 회색 박스 3개)
            for (int i = 0; i < 3; i++)
            {
                Transform slot = Group(env, "DisplaySlot", new Vector3(6.8f + i * 1.2f, 0f, -5f));
                Part(slot, PrimitiveType.Cube, new Vector3(0f, 0.4f, 0f),  new Vector3(0.5f, 0.8f, 0.5f),   "#9a9aa0", name: "Pedestal");
                Part(slot, PrimitiveType.Cube, new Vector3(0f, 0.82f, 0f), new Vector3(0.56f, 0.04f, 0.56f), "#b8b8be", name: "Top");
            }

            // 확장 예정 구역 (동쪽 바깥, 그림 3 점선) — 마른 땅 + 말뚝·로프
            Part(env, PrimitiveType.Cube, new Vector3(16f, 0.005f, 0f), new Vector3(6f, 0.01f, 12f), "#7d7a5a", name: "ExpansionGround");
            for (int sx = 0; sx <= 1; sx++)
                for (int sz = -1; sz <= 1; sz += 2)
                    Part(env, PrimitiveType.Cylinder, new Vector3(13.5f + sx * 5f, 0.4f, sz * 6f),
                         new Vector3(0.07f, 0.4f, 0.07f), WoodDark, name: "Stake");
            Part(env, PrimitiveType.Cylinder, new Vector3(16f, 0.7f, 6f),    new Vector3(0.03f, 2.5f, 0.03f), "#c9b98a", new Vector3(0f, 0f, 90f), name: "Rope");
            Part(env, PrimitiveType.Cylinder, new Vector3(16f, 0.7f, -6f),   new Vector3(0.03f, 2.5f, 0.03f), "#c9b98a", new Vector3(0f, 0f, 90f), name: "Rope");
            Part(env, PrimitiveType.Cylinder, new Vector3(13.5f, 0.7f, 0f),  new Vector3(0.03f, 6f, 0.03f),   "#c9b98a", new Vector3(90f, 0f, 0f), name: "Rope");
            Part(env, PrimitiveType.Cylinder, new Vector3(18.5f, 0.7f, 0f),  new Vector3(0.03f, 6f, 0.03f),   "#c9b98a", new Vector3(90f, 0f, 0f), name: "Rope");

            // 생활 소품 + 화단 (팩 있을 때만)
            PlaceEnv(env, "storage_barrel",       new Vector3(9.8f, 0f, 3.4f),  yaw: 25f, scale: 0.7f);
            PlaceEnv(env, "storage_basket_small", new Vector3(-7.2f, 0f, -4.3f), yaw: 150f);
            PlaceEnv(env, "storage_jug",          new Vector3(-8.9f, 0f, -4.6f), yaw: 0f);
            PlaceEnv(env, "Sunflower", new Vector3(-2.7f, 0f, -7.6f), yaw: 10f);  // 입구 양옆 해바라기 (통로 x±1.2 밖)
            PlaceEnv(env, "Sunflower", new Vector3(2.7f, 0f, -7.6f),  yaw: 190f);
            ScatterGroundCover(env, new Vector3(-4f, 0f, 3f), 6f, 30, seed: 7166); // 필지 안 꽃밭
            ScatterGroundCover(env, new Vector3(4f, 0f, -2f), 5f, 20, seed: 7167);

            // 필지 밖 나무 (서·북쪽 — 동쪽은 확장 구역이라 비움)
            Vector3[] trees = { new Vector3(-15f, 0f, 8f), new Vector3(-14f, 0f, -8f), new Vector3(-5f, 0f, 11f),
                                new Vector3(5f, 0f, 11f),  new Vector3(-16f, 0f, 0f) };
            for (int i = 0; i < trees.Length; i++)
                BuildTree(env, trees[i], 1.0f + (i % 3) * 0.15f);

            EnsureSpawnPoint(new Vector3(0f, 0f, -6f), Quaternion.identity); // 입구 안쪽, +Z = 소셜존 방향

            // 로비 귀환 관문 (입구 밖 남쪽)
            DestroyRootIfExists("PortalGates");
            Transform gates = new GameObject("PortalGates").transform;
            BuildLobbyReturnGate(gates, new Vector3(0f, 0f, -11f), new Vector3(0f, 0f, -6f));
            EnsureEventSystem();

            // 포근한 초저녁
            ConfigureSun(new Vector3(30f, -40f, 0f), "#ffd2a8", 0.8f);
            ApplySkybox("Mat_EstateSky", 1.35f, 1.1f, "#a8825f", "#42392f");
            ConfigureLinearFog("#8a7c6a", 14f, 42f);
            PlacePreviewCamera(new Vector3(0f, 2f, -10f), new Vector3(10f, 0f, 0f));
        }

        /// <summary>필지 울타리 — 기둥 + 2단 레일, 남쪽 중앙(|x|&lt;1.2)은 입구로 비운다.</summary>
        private static void BuildFence(Transform env)
        {
            Transform fence = Group(env, "Fence", Vector3.zero);

            // 기둥 — 3m 간격 사각 둘레, 남쪽 입구 구간 스킵
            for (float x = -12f; x <= 12f; x += 3f)
            {
                Part(fence, PrimitiveType.Cylinder, new Vector3(x, 0.5f, 8f), new Vector3(0.09f, 0.5f, 0.09f), WoodDark, name: "FencePost");
                if (Mathf.Abs(x) > 1.4f)
                    Part(fence, PrimitiveType.Cylinder, new Vector3(x, 0.5f, -8f), new Vector3(0.09f, 0.5f, 0.09f), WoodDark, name: "FencePost");
            }
            for (float z = -5f; z <= 5f; z += 3f)
            {
                Part(fence, PrimitiveType.Cylinder, new Vector3(-12f, 0.5f, z), new Vector3(0.09f, 0.5f, 0.09f), WoodDark, name: "FencePost");
                Part(fence, PrimitiveType.Cylinder, new Vector3(12f, 0.5f, z),  new Vector3(0.09f, 0.5f, 0.09f), WoodDark, name: "FencePost");
            }

            // 레일 2단 — 북/동/서는 통짜, 남쪽은 입구 양옆 2조각
            foreach (float h in new[] { 0.45f, 0.8f })
            {
                Part(fence, PrimitiveType.Cube, new Vector3(0f, h, 8f),    new Vector3(24f, 0.06f, 0.06f), Wood, name: "FenceRail");
                Part(fence, PrimitiveType.Cube, new Vector3(-12f, h, 0f),  new Vector3(0.06f, 0.06f, 16f), Wood, name: "FenceRail");
                Part(fence, PrimitiveType.Cube, new Vector3(12f, h, 0f),   new Vector3(0.06f, 0.06f, 16f), Wood, name: "FenceRail");
                Part(fence, PrimitiveType.Cube, new Vector3(-6.6f, h, -8f), new Vector3(10.8f, 0.06f, 0.06f), Wood, name: "FenceRail");
                Part(fence, PrimitiveType.Cube, new Vector3(6.6f, h, -8f),  new Vector3(10.8f, 0.06f, 0.06f), Wood, name: "FenceRail");
            }

            // 입구 기둥 (조금 크게, 글로우 갓돌)
            foreach (float sx in new[] { -1.2f, 1.2f })
            {
                Part(fence, PrimitiveType.Cylinder, new Vector3(sx, 0.7f, -8f),  new Vector3(0.12f, 0.7f, 0.12f), WoodDark, name: "EntrancePost");
                Part(fence, PrimitiveType.Cube,     new Vector3(sx, 1.48f, -8f), new Vector3(0.18f, 0.16f, 0.18f), Glow,     name: "EntranceGlow");
            }
        }
    }
}
#endif
