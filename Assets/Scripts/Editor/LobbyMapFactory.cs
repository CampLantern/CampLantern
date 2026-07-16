#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static CampLantern.EditorTools.MapBuildUtil;

namespace CampLantern.EditorTools
{
    /// <summary>
    /// 로비 씬 VR 맵 구성기 — 캠프장 환경(프리미티브 플레이스홀더 아트), 스폰포인트, 석양 라이팅,
    /// VR 포탈 관문을 Lobby.unity에 구성한다. 공용 조립 헬퍼는 <see cref="MapBuildUtil"/>.
    ///
    /// 포탈 구조는 GDD 그림 1 "공간 구조 흐름도"를 따른다: 로비를 관문으로 세 활동 공간이 **분기** —
    /// 표지판 하나가 아니라 목적지별 관문(게이트) 3개가 세 갈래 길 끝에 서고, 색 코딩도 흐름도와 맞춘다
    /// (낚시터=파랑, 사냥터=주황+존 페넌트, 영지=초록). 각 관문의 패널 버튼으로 해당 공간에 진입한다.
    ///
    /// idempotent: 환경(LobbyEnvironment)·관문(PortalGates, 구버전 PortalBoard 포함) 루트는 지우고 재구성,
    /// 스폰포인트·EventSystem은 없을 때만 추가. RoomScenesFactory.CreateLobby가 씬 생성 시 공통 호출하므로
    /// 씬을 재생성해도 맵이 유지된다.
    /// </summary>
    public static class LobbyMapFactory
    {
        private const string k_scenePath = "Assets/Scenes/Lobby.unity";
        private const string k_grass     = "#5f7d4a";

        // 둘레 숲 (반경 8~15m 링, 위치 하드코딩 — 재생성해도 동일한 맵)
        private static readonly Vector3[] k_treePositions =
        {
            new Vector3(8f, 0f, 10f),   new Vector3(-9f, 0f, 9f),   new Vector3(12f, 0f, 2f),
            new Vector3(-12f, 0f, 4f),  new Vector3(10f, 0f, -6f),  new Vector3(-10f, 0f, -7f),
            new Vector3(3f, 0f, 13f),   new Vector3(-4.5f, 0f, 12f),new Vector3(14f, 0f, 7f),
            new Vector3(-14f, 0f, -2f), new Vector3(6f, 0f, -11f),  new Vector3(-6f, 0f, -12f),
        };

        // 흐름도 색 코딩: 낚시터=파랑, 사냥터=주황, 영지=초록
        private static readonly (string label, string sceneName, Vector3 pos, string banner, Color panelTint)[] k_gates =
        {
            ("낚시터", "FishingGround",  new Vector3(-5.5f, 0f, 6f),   "#4a7fae", new Color(0.10f, 0.15f, 0.22f, 0.92f)),
            ("사냥터", "HuntZone_A",     new Vector3(0f, 0f, 7.5f),    "#c77b2f", new Color(0.20f, 0.14f, 0.08f, 0.92f)),
            ("영지",   "EstateTemplate", new Vector3(5.5f, 0f, 6f),    "#4a7a55", new Color(0.10f, 0.18f, 0.12f, 0.92f)),
        };

        [MenuItem("Tools/Make Assets/Build Lobby Map (VR)")]
        public static void Build()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(k_scenePath) == null)
            {
                Debug.LogError($"[MakeAssets] 씬 없음: {k_scenePath} — 먼저 Tools > Make Assets > Room Scenes (Create All) 실행");
                return;
            }

            EditorSceneManager.SaveOpenScenes();
            Scene scene = EditorSceneManager.OpenScene(k_scenePath, OpenSceneMode.Single);
            BuildIntoOpenScene();
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[MakeAssets] 로비 VR 맵 구성 완료: " + k_scenePath);
        }

        /// <summary>현재 열린 씬(로비)에 맵을 구성한다. RoomScenesFactory.CreateLobby가 씬 생성 시 호출.</summary>
        public static void BuildIntoOpenScene()
        {
            BuildEnvironment();
            EnsureSpawnPoint(new Vector3(0f, 0f, -2f), Quaternion.identity); // +Z = 관문 방향
            ConfigureSun(new Vector3(26f, -38f, 0f), "#ffd9b3", 0.85f);      // 낮은 석양 각
            ApplySkybox("Mat_LobbySky", 1.3f, 1.15f, "#a4785c", "#473d33");  // 노을 톤
            ConfigureLinearFog("#8d7a68", 15f, 45f);
            BuildPortalGates();
            PlacePreviewCamera(new Vector3(0f, 1.7f, -4f), new Vector3(6f, 0f, 0f));
        }

        // ── 환경 (캠프장) ─────────────────────────────────────────────

        private static void BuildEnvironment()
        {
            DestroyRootIfExists("LobbyEnvironment");
            Transform env = new GameObject("LobbyEnvironment").transform;

            TextureGround("ground_grass", k_grass); // 잔디 텍스처 (팩 미임포트 시 단색 폴백)

            // 모닥불 플라자 (스폰 오른편) + 장작 더미
            BuildCampfire(env, new Vector3(2.8f, 0f, 1.2f), withSeats: true);
            Transform pile = Group(env, "LogPile", new Vector3(4.3f, 0f, 2.6f));
            Part(pile, PrimitiveType.Cylinder, new Vector3(0f, 0.12f, 0f),    new Vector3(0.12f, 0.45f, 0.12f), WoodDark,  new Vector3(90f, 0f, 0f));
            Part(pile, PrimitiveType.Cylinder, new Vector3(0.26f, 0.12f, 0f), new Vector3(0.12f, 0.45f, 0.12f), WoodDark2, new Vector3(90f, 0f, 0f));
            Part(pile, PrimitiveType.Cylinder, new Vector3(0.13f, 0.32f, 0f), new Vector3(0.12f, 0.45f, 0.12f), WoodDark,  new Vector3(90f, 0f, 0f));

            // 텐트 (스폰 왼편 — 낚시터 갈래길을 막지 않게 서쪽으로) + 랜턴 기둥(상징물)
            BuildTent(env, new Vector3(-5.2f, 0f, 1.2f), yaw: 60f, scale: 1.6f);
            BuildLanternPost(env, new Vector3(1.5f, 0f, 2.2f));

            // 바위·수풀 (실물 프리팹 변주, 팩 없으면 프리미티브 폴백)
            PlaceRock(env, new Vector3(-1.8f, 0f, 0.3f),  0.9f);
            PlaceRock(env, new Vector3(5.2f, 0f, -1.0f),  0.7f);
            PlaceRock(env, new Vector3(-5.5f, 0f, -2.5f), 1.2f);
            PlaceRock(env, new Vector3(0.8f, 0f, 6.5f),   0.8f);
            PlaceBush(env, new Vector3(-1.6f, 0f, 5.4f),  1.1f);
            PlaceBush(env, new Vector3(4.6f, 0f, 4.6f),   1.0f);

            // 캠프 생활 소품 (팩 있을 때만 — PlaceEnv가 없으면 무시)
            PlaceEnv(env, "storage_barrel", new Vector3(5.0f, 0f, 1.8f),  yaw: 40f, scale: 0.7f); // 원본이 1.8m급 — 캠프 소품 크기로
            PlaceEnv(env, "storage_basket", new Vector3(3.8f, 0f, 0.1f),  yaw: 300f);
            PlaceEnv(env, "cart1",          new Vector3(-7.4f, 0f, 3.0f), yaw: 115f);
            PlaceEnv(env, "storage_bag",    new Vector3(-4.1f, 0f, 2.4f), yaw: 20f);

            // 지피식물 스캐터 (풀·꽃·양치 — 고정 시드, 재생성해도 동일)
            ScatterGroundCover(env, Vector3.zero, 13f, 80, seed: 7160);

            // 원경 풍차 실루엣 (Ground 평면 ±20m 안, 숲 링 바깥) — 프리팹 피벗이 바닥이 아니라 y 보정 (감사에서 2m 매몰 확인)
            PlaceEnv(env, "Windmill", new Vector3(16f, 1.85f, 18f), yaw: 205f);

            // 둘레 숲
            for (int i = 0; i < k_treePositions.Length; i++)
                BuildTree(env, k_treePositions[i], 0.9f + (i % 3) * 0.15f); // 크기 변주(결정적)
        }

        // ── VR 포탈 관문 (GDD 그림 1 — 로비를 관문으로 세 활동 공간 분기) ──

        private static void BuildPortalGates()
        {
            EnsureEventSystem();

            DestroyRootIfExists("PortalBoard"); // 구버전 단일 안내판
            DestroyRootIfExists("PortalGates");

            Transform gates = new GameObject("PortalGates").transform;
            Vector3 spawnPos = new Vector3(0f, 0f, -3f); // "PlayerSpawn" 위치 — 관문이 이쪽을 바라본다
            Vector3 plaza    = new Vector3(0f, 0f, -0.5f); // 세 갈래 길의 분기점

            foreach (var g in k_gates)
            {
                Transform gate = BuildPortalGate(gates, g.label, g.sceneName, g.pos, spawnPos, g.banner, g.panelTint);
                if (gate == null) return; // VR UI 프리팹 없음 — 경고는 BuildPortalGate가 출력

                // 사냥터 관문엔 존 페넌트 3개 (그림 1의 숲/설원/해안 존 분기 상징)
                if (g.sceneName == "HuntZone_A")
                {
                    Part(gate, PrimitiveType.Cube, new Vector3(-0.35f, 1.72f, 0.05f), new Vector3(0.16f, 0.2f, 0.04f), "#567a44", name: "Pennant"); // 숲
                    Part(gate, PrimitiveType.Cube, new Vector3(0f, 1.72f, 0.05f),     new Vector3(0.16f, 0.2f, 0.04f), "#c9d6e2", name: "Pennant"); // 설원
                    Part(gate, PrimitiveType.Cube, new Vector3(0.35f, 1.72f, 0.05f),  new Vector3(0.16f, 0.2f, 0.04f), "#d9b96a", name: "Pennant"); // 해안
                }

                LayPathStones(gates, plaza, g.pos);
            }
        }
    }
}
#endif
