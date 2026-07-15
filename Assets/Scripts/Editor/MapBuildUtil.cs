#if UNITY_EDITOR
using CampLantern.Player;
using CampLantern.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CampLantern.EditorTools
{
    /// <summary>
    /// 공간 씬 맵 구성 공용 헬퍼 — LobbyMapFactory/RoomMapsFactory가 공유한다.
    /// 프리미티브 파트 조립(PlaceholderArtFactory 방식: 색상별 공유 머티리얼 Mat_PH_*),
    /// 포탈 관문(PortalPanelController 배선), 스폰포인트/EventSystem 보장, 라이팅 프리셋.
    /// RULE-02: 전부 Unity API로만 생성 — 에셋 파일 직접 작성 없음.
    /// </summary>
    internal static class MapBuildUtil
    {
        private const string k_materialFolder        = "Assets/Prefabs/Materials";
        private const string k_panelPrefabPath       = "Assets/Prefabs/UI/VRUIPanel.prefab";
        private const string k_buttonPrefabPath      = "Assets/Prefabs/UI/VRUIButton.prefab";
        private const string k_eventSystemPrefabPath = "Assets/Prefabs/UI/VRUIEventSystem.prefab";

        // 공통 팔레트 (PlaceholderArtFactory 톤과 맞춤)
        public const string Wood      = "#9a6a3a";
        public const string WoodDark  = "#6a4a25";
        public const string WoodDark2 = "#8a5a2f";
        public const string Trunk     = "#6a4a2a";
        public const string LeafDark  = "#4a6b3a";
        public const string LeafLight = "#567a44";
        public const string Stone     = "#84848a";
        public const string Flame     = "#e8641a";
        public const string FlameCore = "#ffd23a";
        public const string Canvas    = "#c98a4a";
        public const string CanvasDk  = "#8a5a2a";
        public const string Glow      = "#fff3b0";
        public const string Gold      = "#c9a53a";
        public const string PathGray  = "#8d8d90";
        public const string LobbyGray = "#7a7a7e"; // 그림 1 로비 색 — 귀환 관문 현판용

        // ── 기본 조립 ────────────────────────────────────────────────

        public static Transform Group(Transform parent, string name, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return go.transform;
        }

        public static GameObject Part(Transform parent, PrimitiveType shape, Vector3 pos, Vector3 scale,
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

        public static void AddPointLight(Transform parent, Vector3 localPos, Color color, float intensity, float range)
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

        public static void DestroyRootIfExists(string name)
        {
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                if (root.name == name) Object.DestroyImmediate(root);
        }

        /// <summary>씬의 "Ground" 오브젝트 머티리얼을 교체한다 (그레이박스 → 테마색).</summary>
        public static void RecolorGround(string hex)
        {
            var ground = GameObject.Find("Ground");
            if (ground != null) ground.GetComponent<Renderer>().sharedMaterial = Mat(hex);
        }

        // ── 공통 소품 ────────────────────────────────────────────────

        public static void BuildTree(Transform parent, Vector3 pos, float scale)
        {
            Transform tree = Group(parent, "Tree", pos);
            tree.localScale = Vector3.one * scale;
            Part(tree, PrimitiveType.Cylinder, new Vector3(0f, 0.9f, 0f),  new Vector3(0.35f, 0.9f, 0.35f),  Trunk);
            Part(tree, PrimitiveType.Sphere,   new Vector3(0f, 2.1f, 0f),  new Vector3(1.7f, 1.5f, 1.7f),    LeafDark);
            Part(tree, PrimitiveType.Sphere,   new Vector3(0f, 3.05f, 0f), new Vector3(1.15f, 1.05f, 1.15f), LeafLight);
        }

        /// <summary>모닥불 — 돌 링 + 장작 + 불꽃 + 웜 라이트. withSeats면 통나무 의자 4개 추가.</summary>
        public static Transform BuildCampfire(Transform parent, Vector3 pos, bool withSeats)
        {
            Transform fire = Group(parent, "Campfire", pos);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI * 2f / 6f;
                Part(fire, PrimitiveType.Sphere,
                     new Vector3(Mathf.Cos(a) * 0.55f, 0.08f, Mathf.Sin(a) * 0.55f),
                     new Vector3(0.28f, 0.18f, 0.28f), "#7f7f83", name: "Stone");
            }
            Part(fire, PrimitiveType.Cylinder, new Vector3(0f, 0.12f, 0f), new Vector3(0.1f, 0.5f, 0.1f), WoodDark,  new Vector3(90f, 25f, 0f));
            Part(fire, PrimitiveType.Cylinder, new Vector3(0f, 0.12f, 0f), new Vector3(0.1f, 0.5f, 0.1f), WoodDark2, new Vector3(90f, -25f, 0f));
            Part(fire, PrimitiveType.Capsule,  new Vector3(0f, 0.42f, 0f), new Vector3(0.28f, 0.3f, 0.28f), Flame,     name: "Flame");
            Part(fire, PrimitiveType.Capsule,  new Vector3(0f, 0.38f, 0f), new Vector3(0.16f, 0.2f, 0.16f), FlameCore, name: "FlameCore");
            AddPointLight(fire, new Vector3(0f, 0.8f, 0f), Hex("#ffa052"), 2.0f, 9f);

            if (withSeats)
            {
                float[] seatAngles = { 40f, 140f, 220f, 320f };
                foreach (float deg in seatAngles)
                {
                    float a = deg * Mathf.Deg2Rad;
                    Part(fire, PrimitiveType.Cylinder,
                         new Vector3(Mathf.Cos(a) * 1.5f, 0.22f, Mathf.Sin(a) * 1.5f),
                         new Vector3(0.38f, 0.22f, 0.38f), "#7a5a34", name: "Stump");
                }
            }
            return fire;
        }

        public static Transform BuildTent(Transform parent, Vector3 pos, float yaw, float scale)
        {
            Transform tent = Group(parent, "Tent", pos);
            tent.localRotation = Quaternion.Euler(0f, yaw, 0f);
            tent.localScale = Vector3.one * scale;
            Part(tent, PrimitiveType.Cube, new Vector3(-0.3f, 0.55f, 0f), new Vector3(0.06f, 1.1f, 1.3f), Canvas, new Vector3(0f, 0f, 28f));
            Part(tent, PrimitiveType.Cube, new Vector3(0.3f, 0.55f, 0f),  new Vector3(0.06f, 1.1f, 1.3f), Canvas, new Vector3(0f, 0f, -28f));
            Part(tent, PrimitiveType.Cube, new Vector3(0f, 0.35f, 0.62f), new Vector3(0.35f, 0.5f, 0.04f), CanvasDk);
            return tent;
        }

        public static void BuildLanternPost(Transform parent, Vector3 pos)
        {
            Transform post = Group(parent, "LanternPost", pos);
            Part(post, PrimitiveType.Cylinder, new Vector3(0f, 0.85f, 0f), new Vector3(0.07f, 0.85f, 0.07f), "#4a3a28");
            Part(post, PrimitiveType.Cube,     new Vector3(0f, 1.78f, 0f), new Vector3(0.2f, 0.26f, 0.2f),   Glow, name: "Glow");
            Part(post, PrimitiveType.Cube,     new Vector3(0f, 1.94f, 0f), new Vector3(0.24f, 0.05f, 0.24f), Gold, name: "Cap");
            AddPointLight(post, new Vector3(0f, 1.78f, 0f), Hex("#ffe2a8"), 1.4f, 6f);
        }

        /// <summary>분기점→목적지 디딤돌 길 (지그재그 오프셋).</summary>
        public static void LayPathStones(Transform parent, Vector3 from, Vector3 to)
        {
            Vector3 dir = to - from;
            dir.y = 0f;
            float length = dir.magnitude;
            dir /= length;
            Vector3 side = Vector3.Cross(Vector3.up, dir);

            int i = 0;
            for (float d = 1.0f; d < length - 1.0f; d += 1.1f, i++)
            {
                Vector3 p = from + dir * d + side * (i % 2 == 0 ? 0.18f : -0.18f);
                Part(parent, PrimitiveType.Cylinder, new Vector3(p.x, 0.015f, p.z),
                     new Vector3(0.38f, 0.02f, 0.38f), PathGray, name: "PathStone");
            }
        }

        // ── 스폰포인트 / EventSystem ─────────────────────────────────

        /// <summary>씬에 PlayerSpawnPoint가 없으면 추가 (컴포넌트 존재 기준 — GO 이름에 의존하지 않음).</summary>
        public static void EnsureSpawnPoint(Vector3 position, Quaternion rotation)
        {
            if (Object.FindFirstObjectByType<PlayerSpawnPoint>() != null) return;

            var spawn = new GameObject("PlayerSpawn");
            spawn.AddComponent<PlayerSpawnPoint>();
            spawn.transform.SetPositionAndRotation(position, rotation);
        }

        /// <summary>씬당 EventSystem 1개 보장 (PointableCanvasModule 포함 프리팹 — VR UI 클릭 필수).</summary>
        public static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;

            var esPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(k_eventSystemPrefabPath);
            if (esPrefab != null) PrefabUtility.InstantiatePrefab(esPrefab);
            else Debug.LogWarning($"[MakeAssets] {k_eventSystemPrefabPath} 없음 — VR UI 클릭 불가. " +
                                  "Tools > Make Assets > VR UI (Create All) 먼저 실행 후 재빌드.");
        }

        // ── 포탈 관문 (GDD 그림 1 색 코딩) ───────────────────────────

        /// <summary>
        /// 관문 하나 — 기둥+보+테마색 현판 프레임에 단일 버튼 패널(PortalPanelController)을 건다.
        /// facingFrom(보통 스폰)을 바라보게 회전. VR UI 프리팹이 없으면 경고 후 null.
        /// </summary>
        public static Transform BuildPortalGate(Transform parent, string title, string sceneName, Vector3 position,
                                                Vector3 facingFrom, string bannerHex, Color panelTint,
                                                string buttonLabel = "이동")
        {
            var panelPrefab  = AssetDatabase.LoadAssetAtPath<GameObject>(k_panelPrefabPath);
            var buttonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(k_buttonPrefabPath);
            if (panelPrefab == null || buttonPrefab == null)
            {
                Debug.LogWarning("[MakeAssets] VR UI 프리팹 없음 — 포탈 관문 생략. " +
                                 "Tools > Make Assets > VR UI (Create All) 실행 후 맵 재빌드.");
                return null;
            }

            Transform gate = Group(parent, $"Gate_{sceneName}", position);
            // 관문 정면이 facingFrom을 향하게 — +Z를 반대로 두면 패널(-Z에서 읽힘)이 그쪽을 본다
            Vector3 away = position - facingFrom;
            away.y = 0f;
            gate.rotation = Quaternion.LookRotation(away.normalized);

            Part(gate, PrimitiveType.Cylinder, new Vector3(0.5f, 1.0f, 0.05f),  new Vector3(0.09f, 1.0f, 0.09f), Wood);
            Part(gate, PrimitiveType.Cylinder, new Vector3(-0.5f, 1.0f, 0.05f), new Vector3(0.09f, 1.0f, 0.09f), Wood);
            Part(gate, PrimitiveType.Cube,     new Vector3(0f, 2.02f, 0.05f),   new Vector3(1.3f, 0.1f, 0.12f),  WoodDark);
            Part(gate, PrimitiveType.Cube,     new Vector3(0f, 2.28f, 0.05f),   new Vector3(1.15f, 0.3f, 0.06f), bannerHex, name: "Banner");

            // 패널 — 씬 전용 구성이라 완전 언팩(크기·버튼 치환·컨트롤러 추가 자유)
            var panelGo = (GameObject)PrefabUtility.InstantiatePrefab(panelPrefab);
            PrefabUtility.UnpackPrefabInstance(panelGo, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            panelGo.name = "PortalPanel";
            panelGo.transform.SetParent(gate, false);
            panelGo.transform.localPosition = new Vector3(0f, 1.35f, 0f);
            panelGo.transform.localScale = Vector3.one * 0.0018f;

            // 단일 버튼용 축소 (400x520 → 400x300px = 0.72 x 0.54 m). 인터랙션 자식은 anchors 0-1이라 따라온다.
            var panelRt = panelGo.GetComponent<RectTransform>();
            panelRt.sizeDelta = new Vector2(400f, 300f);
            panelGo.GetComponent<Image>().color = panelTint;

            var panel = panelGo.GetComponent<VRUIPanel>();

            RectTransform content = panel.Content;
            for (int i = content.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(content.GetChild(i).gameObject);

            var btnGo = (GameObject)PrefabUtility.InstantiatePrefab(buttonPrefab);
            PrefabUtility.UnpackPrefabInstance(btnGo, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            btnGo.transform.SetParent(content, false);
            var btnRt = btnGo.GetComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0.5f, 1f);
            btnRt.anchorMax = new Vector2(0.5f, 1f);
            btnRt.pivot     = new Vector2(0.5f, 1f);
            btnRt.anchoredPosition = new Vector2(0f, -40f);

            var btnLabel = btnGo.GetComponentInChildren<TextMeshProUGUI>(true);
            if (btnLabel != null) btnLabel.text = buttonLabel; // 에디터에서도 보이게 굽기 (런타임 Push와 별개)
            var button = btnGo.GetComponent<VRUIButton>();

            // 제목도 에디터 시점에 굽는다 (버튼 제거 후 남은 TMP = Title)
            var title2 = panelGo.GetComponentInChildren<TextMeshProUGUI>(true);
            if (title2 != null && title2 != btnLabel) title2.text = title;

            var controller = panelGo.AddComponent<PortalPanelController>();
            var so = new SerializedObject(controller);
            so.FindProperty("m_panel").objectReferenceValue = panel;
            so.FindProperty("m_title").stringValue = title;

            var btnProp = so.FindProperty("m_buttons");
            btnProp.arraySize = 1;
            btnProp.GetArrayElementAtIndex(0).objectReferenceValue = button;

            var destProp = so.FindProperty("m_destinations");
            destProp.arraySize = 1;
            var element = destProp.GetArrayElementAtIndex(0);
            element.FindPropertyRelative("label").stringValue = buttonLabel;
            element.FindPropertyRelative("sceneName").stringValue = sceneName;
            so.ApplyModifiedPropertiesWithoutUndo();

            return gate;
        }

        /// <summary>로비 귀환 관문 — 활동 공간 공통 (그림 1의 로비=회색 코딩).</summary>
        public static Transform BuildLobbyReturnGate(Transform parent, Vector3 position, Vector3 facingFrom)
        {
            return BuildPortalGate(parent, "로비", "Lobby", position, facingFrom,
                                   LobbyGray, new Color(0.13f, 0.13f, 0.15f, 0.92f), "귀환");
        }

        // ── 라이팅 프리셋 ────────────────────────────────────────────

        public static void ConfigureSun(Vector3 euler, string colorHex, float intensity)
        {
            var sunGo = GameObject.Find("Directional Light");
            Light sun = sunGo != null ? sunGo.GetComponent<Light>() : null;
            if (sun == null)
            {
                sunGo = new GameObject("Directional Light");
                sun = sunGo.AddComponent<Light>();
                sun.type = LightType.Directional;
            }
            sunGo.transform.rotation = Quaternion.Euler(euler);
            sun.color = Hex(colorHex);
            sun.intensity = intensity;
            RenderSettings.sun = sun;
        }

        public static void ConfigureLinearFog(string colorHex, float start, float end)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = start;
            RenderSettings.fogEndDistance = end;
            RenderSettings.fogColor = Hex(colorHex);
        }

        /// <summary>절차적 스카이박스 머티리얼 생성/로드 후 씬에 적용 (씬 무드별 이름 분리).</summary>
        public static void ApplySkybox(string assetName, float atmosphere, float exposure,
                                       string skyTintHex, string groundHex)
        {
            string path = $"{k_materialFolder}/{assetName}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find("Skybox/Procedural");
                if (shader == null)
                {
                    Debug.LogWarning("[MakeAssets] Skybox/Procedural 셰이더 없음 — 스카이박스 생략");
                    return;
                }
                EnsureMaterialFolder();
                mat = new Material(shader);
                mat.SetFloat("_AtmosphereThickness", atmosphere);
                mat.SetFloat("_Exposure", exposure);
                mat.SetColor("_SkyTint", Hex(skyTintHex));
                mat.SetColor("_GroundColor", Hex(groundHex));
                AssetDatabase.CreateAsset(mat, path);
            }
            RenderSettings.skybox = mat;
        }

        // 데스크톱(비-VR) 프리뷰용 — VR에선 리그 카메라가 사용됨
        public static void PlacePreviewCamera(Vector3 pos, Vector3 euler)
        {
            var camera = GameObject.Find("Main Camera");
            if (camera != null)
                camera.transform.SetPositionAndRotation(pos, Quaternion.Euler(euler));
        }

        // ── 머티리얼 ─────────────────────────────────────────────────

        // 색상별 공유 머티리얼 캐시 — PlaceholderArtFactory와 동일 경로(Mat_PH_*) 재사용, 드로우콜 절약
        public static Material Mat(string hex)
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

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color c);
            return c;
        }
    }
}
#endif
