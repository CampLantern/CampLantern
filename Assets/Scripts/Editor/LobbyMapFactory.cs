#if UNITY_EDITOR
using CampLantern.Player;
using CampLantern.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CampLantern.EditorTools
{
    /// <summary>
    /// 로비 씬 VR 맵 구성기 — 캠프장 환경(프리미티브 플레이스홀더 아트), 스폰포인트, 석양 라이팅,
    /// VR 포탈 UI(월드스페이스 패널 + 낚시터/사냥터/영지 버튼)를 Lobby.unity에 구성한다.
    ///
    /// idempotent: 환경(LobbyEnvironment)·포탈(PortalBoard) 루트는 지우고 재구성, 스폰포인트·EventSystem은
    /// 없을 때만 추가. RoomScenesFactory.CreateLobby가 씬 생성 시 공통 호출하므로 씬을 재생성해도 맵이 유지된다.
    ///
    /// 선행: VR UI 프리팹(Tools > Make Assets > VR UI (Create All)). 없으면 환경만 만들고 포탈 UI는 경고 후 생략.
    /// RULE-02: .unity 직접 작성 금지 — EditorSceneManager/씬 API로만 구성.
    /// 아트 방식은 PlaceholderArtFactory와 동일(프리미티브 조합 + 색상별 공유 머티리얼 캐시 Mat_PH_*).
    /// </summary>
    public static class LobbyMapFactory
    {
        private const string k_scenePath             = "Assets/Scenes/Lobby.unity";
        private const string k_materialFolder        = "Assets/Prefabs/Materials";
        private const string k_panelPrefabPath       = "Assets/Prefabs/UI/VRUIPanel.prefab";
        private const string k_buttonPrefabPath      = "Assets/Prefabs/UI/VRUIButton.prefab";
        private const string k_eventSystemPrefabPath = "Assets/Prefabs/UI/VRUIEventSystem.prefab";

        // 팔레트 (PlaceholderArtFactory 톤과 맞춤)
        private const string k_grass     = "#5f7d4a";
        private const string k_wood      = "#9a6a3a";
        private const string k_woodDark  = "#6a4a25";
        private const string k_woodDark2 = "#8a5a2f";
        private const string k_trunk     = "#6a4a2a";
        private const string k_leafDark  = "#4a6b3a";
        private const string k_leafLight = "#567a44";
        private const string k_stone     = "#84848a";
        private const string k_flame     = "#e8641a";
        private const string k_flameCore = "#ffd23a";
        private const string k_canvas    = "#c98a4a";
        private const string k_canvasDk  = "#8a5a2a";
        private const string k_glow      = "#fff3b0";
        private const string k_gold      = "#c9a53a";

        // 둘레 숲 (반경 8~15m 링, 위치 하드코딩 — 재생성해도 동일한 맵)
        private static readonly Vector3[] k_treePositions =
        {
            new Vector3(8f, 0f, 10f),   new Vector3(-9f, 0f, 9f),   new Vector3(12f, 0f, 2f),
            new Vector3(-12f, 0f, 4f),  new Vector3(10f, 0f, -6f),  new Vector3(-10f, 0f, -7f),
            new Vector3(3f, 0f, 13f),   new Vector3(-4.5f, 0f, 12f),new Vector3(14f, 0f, 7f),
            new Vector3(-14f, 0f, -2f), new Vector3(6f, 0f, -11f),  new Vector3(-6f, 0f, -12f),
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
            EnsureSpawnPoint();
            ConfigureLighting();
            EnsurePortalUI();
            PlacePreviewCamera();
        }

        // ── 환경 (캠프장) ─────────────────────────────────────────────

        private static void BuildEnvironment()
        {
            DestroyRootIfExists("LobbyEnvironment");
            Transform env = new GameObject("LobbyEnvironment").transform;

            // 바닥 재색 — 회색 그레이박스 → 잔디
            var ground = GameObject.Find("Ground");
            if (ground != null) ground.GetComponent<Renderer>().sharedMaterial = Mat(k_grass);

            // 모닥불 플라자 (스폰 오른편) — 돌 링 + 장작 + 불꽃 + 웜 라이트
            Transform fire = Group(env, "Campfire", new Vector3(2.8f, 0f, 1.2f));
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI * 2f / 6f;
                Part(fire, PrimitiveType.Sphere,
                     new Vector3(Mathf.Cos(a) * 0.55f, 0.08f, Mathf.Sin(a) * 0.55f),
                     new Vector3(0.28f, 0.18f, 0.28f), "#7f7f83", name: "Stone");
            }
            Part(fire, PrimitiveType.Cylinder, new Vector3(0f, 0.12f, 0f), new Vector3(0.1f, 0.5f, 0.1f), k_woodDark,  new Vector3(90f, 25f, 0f));
            Part(fire, PrimitiveType.Cylinder, new Vector3(0f, 0.12f, 0f), new Vector3(0.1f, 0.5f, 0.1f), k_woodDark2, new Vector3(90f, -25f, 0f));
            Part(fire, PrimitiveType.Capsule,  new Vector3(0f, 0.42f, 0f), new Vector3(0.28f, 0.3f, 0.28f), k_flame,     name: "Flame");
            Part(fire, PrimitiveType.Capsule,  new Vector3(0f, 0.38f, 0f), new Vector3(0.16f, 0.2f, 0.16f), k_flameCore, name: "FlameCore");
            AddPointLight(fire, new Vector3(0f, 0.8f, 0f), Hex("#ffa052"), 2.0f, 9f);

            // 통나무 의자 4개 (모닥불 둘레)
            float[] seatAngles = { 40f, 140f, 220f, 320f };
            foreach (float deg in seatAngles)
            {
                float a = deg * Mathf.Deg2Rad;
                Part(fire, PrimitiveType.Cylinder,
                     new Vector3(Mathf.Cos(a) * 1.5f, 0.22f, Mathf.Sin(a) * 1.5f),
                     new Vector3(0.38f, 0.22f, 0.38f), "#7a5a34", name: "Stump");
            }

            // 텐트 (스폰 왼편, 플라자를 바라보게)
            Transform tent = Group(env, "Tent", new Vector3(-3.4f, 0f, 3.4f));
            tent.localRotation = Quaternion.Euler(0f, 140f, 0f);
            tent.localScale = Vector3.one * 1.6f;
            Part(tent, PrimitiveType.Cube, new Vector3(-0.3f, 0.55f, 0f),  new Vector3(0.06f, 1.1f, 1.3f), k_canvas, new Vector3(0f, 0f, 28f));
            Part(tent, PrimitiveType.Cube, new Vector3(0.3f, 0.55f, 0f),   new Vector3(0.06f, 1.1f, 1.3f), k_canvas, new Vector3(0f, 0f, -28f));
            Part(tent, PrimitiveType.Cube, new Vector3(0f, 0.35f, 0.62f),  new Vector3(0.35f, 0.5f, 0.04f), k_canvasDk);

            // 랜턴 기둥 (포탈 보드 옆) — 게임 상징물 + 보조광
            Transform post = Group(env, "LanternPost", new Vector3(1.5f, 0f, 2.2f));
            Part(post, PrimitiveType.Cylinder, new Vector3(0f, 0.85f, 0f), new Vector3(0.07f, 0.85f, 0.07f), "#4a3a28");
            Part(post, PrimitiveType.Cube,     new Vector3(0f, 1.78f, 0f), new Vector3(0.2f, 0.26f, 0.2f),   k_glow, name: "Glow");
            Part(post, PrimitiveType.Cube,     new Vector3(0f, 1.94f, 0f), new Vector3(0.24f, 0.05f, 0.24f), k_gold, name: "Cap");
            AddPointLight(post, new Vector3(0f, 1.78f, 0f), Hex("#ffe2a8"), 1.4f, 6f);

            // 장작 더미
            Transform pile = Group(env, "LogPile", new Vector3(4.3f, 0f, 2.6f));
            Part(pile, PrimitiveType.Cylinder, new Vector3(0f, 0.12f, 0f),    new Vector3(0.12f, 0.45f, 0.12f), k_woodDark,  new Vector3(90f, 0f, 0f));
            Part(pile, PrimitiveType.Cylinder, new Vector3(0.26f, 0.12f, 0f), new Vector3(0.12f, 0.45f, 0.12f), k_woodDark2, new Vector3(90f, 0f, 0f));
            Part(pile, PrimitiveType.Cylinder, new Vector3(0.13f, 0.32f, 0f), new Vector3(0.12f, 0.45f, 0.12f), k_woodDark,  new Vector3(90f, 0f, 0f));

            // 디딤돌 길 (스폰 → 포탈 보드)
            for (int i = 0; i < 4; i++)
            {
                Part(env, PrimitiveType.Cylinder,
                     new Vector3(i % 2 == 0 ? 0.25f : -0.25f, 0.015f, -1.2f + i * 0.9f),
                     new Vector3(0.4f, 0.02f, 0.4f), "#8d8d90", name: "PathStone");
            }

            // 바위·수풀
            Part(env, PrimitiveType.Sphere, new Vector3(-1.8f, 0.12f, 0.3f),  new Vector3(0.5f, 0.3f, 0.45f), k_stone, name: "Rock");
            Part(env, PrimitiveType.Sphere, new Vector3(5.2f, 0.1f, -1.0f),   new Vector3(0.4f, 0.25f, 0.4f), k_stone, name: "Rock");
            Part(env, PrimitiveType.Sphere, new Vector3(-5.5f, 0.15f, -2.5f), new Vector3(0.7f, 0.4f, 0.6f),  k_stone, name: "Rock");
            Part(env, PrimitiveType.Sphere, new Vector3(0.8f, 0.1f, 6.5f),    new Vector3(0.45f, 0.28f, 0.4f), k_stone, name: "Rock");
            Part(env, PrimitiveType.Sphere, new Vector3(-1.6f, 0.25f, 5.4f),  new Vector3(0.8f, 0.5f, 0.8f),  k_leafLight, name: "Bush");
            Part(env, PrimitiveType.Sphere, new Vector3(4.6f, 0.22f, 4.6f),   new Vector3(0.7f, 0.45f, 0.7f), k_leafDark,  name: "Bush");

            // 둘레 숲
            for (int i = 0; i < k_treePositions.Length; i++)
            {
                Transform tree = Group(env, "Tree", k_treePositions[i]);
                tree.localScale = Vector3.one * (0.9f + (i % 3) * 0.15f); // 크기 변주(결정적)
                Part(tree, PrimitiveType.Cylinder, new Vector3(0f, 0.9f, 0f),  new Vector3(0.35f, 0.9f, 0.35f),  k_trunk);
                Part(tree, PrimitiveType.Sphere,   new Vector3(0f, 2.1f, 0f),  new Vector3(1.7f, 1.5f, 1.7f),    k_leafDark);
                Part(tree, PrimitiveType.Sphere,   new Vector3(0f, 3.05f, 0f), new Vector3(1.15f, 1.05f, 1.15f), k_leafLight);
            }
        }

        // ── 스폰포인트 ────────────────────────────────────────────────

        private static void EnsureSpawnPoint()
        {
            // PersistentPlayer가 씬 로드 시 이 지점으로 이동 — 없으면 리그가 직전 위치에 남아 맵 밖에 뜰 수 있다
            if (Object.FindFirstObjectByType<PlayerSpawnPoint>() != null) return;

            var spawn = new GameObject("PlayerSpawnPoint");
            spawn.AddComponent<PlayerSpawnPoint>();
            spawn.transform.SetPositionAndRotation(new Vector3(0f, 0f, -2f), Quaternion.identity); // +Z = 포탈 보드 방향
        }

        // ── 라이팅 (석양 + 포그) ──────────────────────────────────────

        private static void ConfigureLighting()
        {
            var sunGo = GameObject.Find("Directional Light");
            Light sun = sunGo != null ? sunGo.GetComponent<Light>() : null;
            if (sun == null)
            {
                sunGo = new GameObject("Directional Light");
                sun = sunGo.AddComponent<Light>();
                sun.type = LightType.Directional;
            }
            sunGo.transform.rotation = Quaternion.Euler(26f, -38f, 0f); // 낮은 석양 각
            sun.color = Hex("#ffd9b3");
            sun.intensity = 0.85f;
            RenderSettings.sun = sun;

            Material sky = SkyMaterial();
            if (sky != null) RenderSettings.skybox = sky;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 15f;
            RenderSettings.fogEndDistance = 45f;
            RenderSettings.fogColor = Hex("#8d7a68");
        }

        private static Material SkyMaterial()
        {
            string path = $"{k_materialFolder}/Mat_LobbySky.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            Shader shader = Shader.Find("Skybox/Procedural");
            if (shader == null)
            {
                Debug.LogWarning("[MakeAssets] Skybox/Procedural 셰이더 없음 — 스카이박스 생략");
                return null;
            }

            EnsureMaterialFolder();
            mat = new Material(shader);
            mat.SetFloat("_AtmosphereThickness", 1.3f);
            mat.SetFloat("_Exposure", 1.15f);
            mat.SetColor("_SkyTint", Hex("#a4785c"));      // 노을 톤
            mat.SetColor("_GroundColor", Hex("#473d33"));
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // ── VR 포탈 UI ───────────────────────────────────────────────

        private static void EnsurePortalUI()
        {
            // 씬당 EventSystem 1개 (PointableCanvasModule 포함 프리팹)
            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var esPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(k_eventSystemPrefabPath);
                if (esPrefab != null) PrefabUtility.InstantiatePrefab(esPrefab);
                else Debug.LogWarning($"[MakeAssets] {k_eventSystemPrefabPath} 없음 — VR UI 클릭 불가. " +
                                      "Tools > Make Assets > VR UI (Create All) 먼저 실행 후 재빌드.");
            }

            DestroyRootIfExists("PortalBoard");

            var panelPrefab  = AssetDatabase.LoadAssetAtPath<GameObject>(k_panelPrefabPath);
            var buttonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(k_buttonPrefabPath);
            if (panelPrefab == null || buttonPrefab == null)
            {
                Debug.LogWarning("[MakeAssets] VR UI 프리팹 없음 — 포탈 UI 생략(환경만 구성). " +
                                 "Tools > Make Assets > VR UI (Create All) 실행 후 Build Lobby Map (VR) 재실행.");
                return;
            }

            // 나무 안내판 프레임 (패널 뒤쪽 +Z — 패널은 -Z 방향에서 읽힘 = 스폰 쪽)
            Transform board = new GameObject("PortalBoard").transform;
            board.position = new Vector3(0f, 0f, 2.6f);
            Part(board, PrimitiveType.Cylinder, new Vector3(0.55f, 1.0f, 0.06f),  new Vector3(0.09f, 1.0f, 0.09f), k_wood);
            Part(board, PrimitiveType.Cylinder, new Vector3(-0.55f, 1.0f, 0.06f), new Vector3(0.09f, 1.0f, 0.09f), k_wood);
            Part(board, PrimitiveType.Cube,     new Vector3(0f, 2.06f, 0.06f),    new Vector3(1.35f, 0.1f, 0.12f), k_woodDark);

            // 패널 — 씬 전용 구성이라 완전 언팩(샘플 버튼 치환·컨트롤러 추가 자유, 프리팹 링크 불필요)
            var panelGo = (GameObject)PrefabUtility.InstantiatePrefab(panelPrefab);
            PrefabUtility.UnpackPrefabInstance(panelGo, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            panelGo.name = "PortalPanel";
            panelGo.transform.SetParent(board, false);
            panelGo.transform.localPosition = new Vector3(0f, 1.45f, 0f);
            panelGo.transform.localScale = Vector3.one * 0.002f; // 400x520px → 0.8 x 1.04 m (원거리 가독)
            var panel = panelGo.GetComponent<VRUIPanel>();

            // 샘플 버튼 제거 후 목적지 버튼 3개 배치
            RectTransform content = panel.Content;
            for (int i = content.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(content.GetChild(i).gameObject);

            (string label, string sceneName)[] dests =
            {
                ("낚시터", "FishingGround"),
                ("사냥터", "HuntZone_A"),
                ("영지",   "EstateTemplate"),
            };

            var buttons = new VRUIButton[dests.Length];
            for (int i = 0; i < dests.Length; i++)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(buttonPrefab);
                PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                inst.transform.SetParent(content, false);

                var rt = inst.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 1f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot     = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, -16f - i * 84f);

                var tmp = inst.GetComponentInChildren<TextMeshProUGUI>(true);
                if (tmp != null) tmp.text = dests[i].label; // 에디터에서도 보이게 굽기 (런타임 Push와 별개)

                buttons[i] = inst.GetComponent<VRUIButton>();
            }

            // 컨트롤러 배선 — 라벨 Push + 클릭 → SceneManager.LoadScene
            var controller = panelGo.AddComponent<PortalPanelController>();
            var so = new SerializedObject(controller);
            so.FindProperty("m_panel").objectReferenceValue = panel;
            so.FindProperty("m_title").stringValue = "Camp Lantern";

            var btnProp = so.FindProperty("m_buttons");
            btnProp.arraySize = buttons.Length;
            for (int i = 0; i < buttons.Length; i++)
                btnProp.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];

            var destProp = so.FindProperty("m_destinations");
            destProp.arraySize = dests.Length;
            for (int i = 0; i < dests.Length; i++)
            {
                var element = destProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("label").stringValue = dests[i].label;
                element.FindPropertyRelative("sceneName").stringValue = dests[i].sceneName;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // 데스크톱(비-VR) 프리뷰용 — VR에선 리그 카메라가 사용됨
        private static void PlacePreviewCamera()
        {
            var camera = GameObject.Find("Main Camera");
            if (camera != null)
                camera.transform.SetPositionAndRotation(new Vector3(0f, 1.7f, -4f), Quaternion.Euler(6f, 0f, 0f));
        }

        // ── 헬퍼 ─────────────────────────────────────────────────────

        private static void DestroyRootIfExists(string name)
        {
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                if (root.name == name) Object.DestroyImmediate(root);
        }

        private static Transform Group(Transform parent, string name, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return go.transform;
        }

        private static GameObject Part(Transform parent, PrimitiveType shape, Vector3 pos, Vector3 scale,
                                       string hex, Vector3 euler = default, string name = "Part")
        {
            var go = GameObject.CreatePrimitive(shape);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>()); // 장식용 — 콜라이더 불필요
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale    = scale;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.GetComponent<Renderer>().sharedMaterial = Mat(hex);
            // 정적 배칭만 — ContributeGI까지 켜면 라이트맵 베이크 대상이 되므로 제외
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        private static void AddPointLight(Transform parent, Vector3 localPos, Color color, float intensity, float range)
        {
            var go = new GameObject("Light");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None; // Quest 성능 — 포인트 그림자 금지
        }

        // 색상별 공유 머티리얼 캐시 — PlaceholderArtFactory와 동일 경로(Mat_PH_*) 재사용, 드로우콜 절약
        private static Material Mat(string hex)
        {
            string safe = hex.Replace("#", "");
            string path = $"{k_materialFolder}/Mat_PH_{safe}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            EnsureMaterialFolder();
            Shader shader = GraphicsSettings.currentRenderPipeline != null
                ? Shader.Find("Universal Render Pipeline/Lit")
                : Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Standard");

            mat = new Material(shader) { color = Hex(hex) };
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static void EnsureMaterialFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            if (!AssetDatabase.IsValidFolder(k_materialFolder))
                AssetDatabase.CreateFolder("Assets/Prefabs", "Materials");
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color c);
            return c;
        }
    }
}
#endif
