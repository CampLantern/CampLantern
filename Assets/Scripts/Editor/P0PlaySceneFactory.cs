#if UNITY_EDITOR
using CampLantern.Bootstrap;
using CampLantern.Cooking;
using CampLantern.Core;
using CampLantern.Estate;
using CampLantern.Fishing;
using CampLantern.Hunting;
using CampLantern.Networking;
using CampLantern.Networking.Voice;
using Fusion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CampLantern.EditorTools
{
    /// <summary>
    /// P0 플레이 씬 일괄 생성기 (RULE-02: .unity/.prefab/.mat 직접 작성 금지 — Unity API로만 생성).
    /// 생성물: HuntTarget 네트워크 프리팹, 영지 비주얼 프리팹 6종(+EstateObjectDef.Prefab 연결),
    /// P0Playground.unity (전 시스템 배선 완료 — 열고 Play 하면 바로 플레이 가능).
    /// </summary>
    public static class P0PlaySceneFactory
    {
        private const string k_scenePath      = "Assets/Scenes/P0Playground.unity";
        private const string k_huntPrefabPath = "Assets/Prefabs/HuntTarget.prefab";
        private const string k_estateFolder   = "Assets/Prefabs/Estate";
        private const string k_materialFolder = "Assets/Prefabs/Materials";

        private const string k_arenaScenePath = "Assets/Scenes/P0NetArena.unity";

        [MenuItem("Tools/Make Assets/P0 Play Scene (Create All)")]
        public static void CreateAll() => CreateInternal(force: false);

        /// <summary>
        /// 멀티 피어(더미 테스트) 전용 빈 네트워크 아레나 씬 생성 + Build Settings 등록.
        /// 멀티 피어 모드는 StartGameArgs.Scene이 없으면 씬 준비 상태가 완료되지 않아 스폰 큐가 영영 처리되지 않는다.
        /// SceneRef는 빌드 인덱스 기반이라 Build Settings 등록이 필수 (EditorBuildSettings API 사용 — RULE-04 예외적 변경, 사용자 보고됨).
        /// </summary>
        [MenuItem("Tools/Make Assets/P0 Net Arena Scene")]
        public static void CreateNetArena()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(k_arenaScenePath) == null)
            {
                EditorSceneManager.SaveOpenScenes();
                string reopenPath = SceneManager.GetActiveScene().path;

                Scene arena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(arena, k_arenaScenePath);

                if (!string.IsNullOrEmpty(reopenPath))
                    EditorSceneManager.OpenScene(reopenPath, OpenSceneMode.Single);
            }

            // Build Settings 등록 (중복 방지)
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == k_arenaScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(k_arenaScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }

            Debug.Log($"[MakeAssets] P0NetArena 준비 완료 (Build Settings 등록 포함): {k_arenaScenePath}");
        }

        [MenuItem("Tools/Make Assets/P0 Play Scene (Force Recreate)")]
        public static void ForceRecreate() => CreateInternal(force: true);

        private static void CreateInternal(bool force)
        {
            if (!force && AssetDatabase.LoadAssetAtPath<SceneAsset>(k_scenePath) != null)
            {
                Debug.LogWarning($"[MakeAssets] 이미 존재합니다. Force Recreate 메뉴를 사용하세요: {k_scenePath}");
                return;
            }

            EnsureFolder("Assets/Prefabs");
            EnsureFolder(k_estateFolder);
            EnsureFolder(k_materialFolder);

            GameObject huntPrefab = CreateHuntTargetPrefab();
            CreateEstatePrefabs();
            CreatePlayScene(huntPrefab);

            Debug.Log($"[MakeAssets] P0 플레이 씬 생성 완료: {k_scenePath} — 씬을 열고 Play");
        }

        // ── 사냥감 네트워크 프리팹 ───────────────────────────────────

        private static GameObject CreateHuntTargetPrefab()
        {
            var root = new GameObject("HuntTarget");
            try
            {
                var networkObject = root.AddComponent<NetworkObject>();
                // 마스터 클라이언트 소유 — 마스터가 나가면 새 마스터로 권한 자동 이전 (서버 관리형 의도).
                // 신규 NetworkObject는 DestroyWhenStateAuthorityLeaves가 기본으로 켜져 있어
                // 마스터 이탈 시 사냥감이 파괴돼 버리므로 명시적으로 꺼서 권한 이전이 되게 한다.
                networkObject.Flags = (networkObject.Flags | NetworkObjectFlags.MasterClientObject)
                                      & ~NetworkObjectFlags.DestroyWhenStateAuthorityLeaves;

                var target = root.AddComponent<HuntTarget>();
                target.Def = LoadRequired<HuntTargetDef>("Assets/Data/Hunt/Hunt_GreatElk.asset");
                root.AddComponent<HuntLedger>();

                AddVisual(root, PrimitiveType.Capsule, new Color(0.45f, 0.3f, 0.15f), "Mat_HuntTarget",
                          localPos: new Vector3(0f, 1f, 0f));

                return PrefabUtility.SaveAsPrefabAsset(root, k_huntPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ── 영지 비주얼 프리팹 6종 + Def.Prefab 연결 ─────────────────

        private static void CreateEstatePrefabs()
        {
            CreateEstatePrefab("Estate_Tent",      PrimitiveType.Cube,     new Color(0.9f, 0.6f, 0.3f));
            CreateEstatePrefab("Estate_Lantern",   PrimitiveType.Sphere,   new Color(1f, 0.9f, 0.4f));
            CreateEstatePrefab("Estate_CampChair", PrimitiveType.Cylinder, new Color(0.5f, 0.7f, 0.4f));
            CreateEstatePrefab("Estate_Planter",   PrimitiveType.Capsule,  new Color(0.4f, 0.8f, 0.5f));
            CreateEstatePrefab("Estate_Deck",      PrimitiveType.Cube,     new Color(0.6f, 0.45f, 0.3f));
            CreateEstatePrefab("Estate_Campfire",  PrimitiveType.Sphere,   new Color(1f, 0.4f, 0.2f));
            AssetDatabase.SaveAssets();
        }

        private static void CreateEstatePrefab(string defName, PrimitiveType shape, Color color)
        {
            var def = LoadRequired<EstateObjectDef>($"Assets/Data/Estate/{defName}.asset");
            string prefabPath = $"{k_estateFolder}/{defName}.prefab";

            var root = new GameObject(defName);
            try
            {
                root.AddComponent<PlacedObject>();
                AddVisual(root, shape, color, $"Mat_{defName}", localPos: new Vector3(0f, 0.5f, 0f));

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                def.Prefab = prefab; // 배치 시 이 비주얼이 인스턴스화된다 (EstateManager.Place)
                EditorUtility.SetDirty(def);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ── 플레이 씬 ────────────────────────────────────────────────

        private static void CreatePlayScene(GameObject huntPrefab)
        {
            // 사용자의 현재 작업 씬 보존 — 저장 후 새 씬으로 전환
            EditorSceneManager.SaveOpenScenes();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // 카메라 — 캠프 전경 부감
            var camera = GameObject.Find("Main Camera");
            camera.transform.SetPositionAndRotation(new Vector3(0f, 10f, -12f), Quaternion.Euler(35f, 0f, 0f));

            // 지형
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(4f, 1f, 4f); // 40x40m
            SetMaterial(ground, new Color(0.35f, 0.55f, 0.3f), "Mat_Ground");

            var pond = GameObject.CreatePrimitive(PrimitiveType.Plane);
            pond.name = "Pond";
            pond.transform.position   = new Vector3(-8f, 0.02f, -2f);
            pond.transform.localScale = new Vector3(0.8f, 1f, 0.8f); // 8x8m
            SetMaterial(pond, new Color(0.25f, 0.45f, 0.8f), "Mat_Water");

            // 낚시
            var spotGo = new GameObject("FishingSpot");
            spotGo.transform.position = pond.transform.position;
            var spot = spotGo.AddComponent<FishingSpot>();
            SetObjectArray(spot, "m_fishTable",
                LoadRequired<FishDef>("Assets/Data/Fish/Fish_Crucian.asset"),
                LoadRequired<FishDef>("Assets/Data/Fish/Fish_Trout.asset"),
                LoadRequired<FishDef>("Assets/Data/Fish/Fish_GoldenCarp.asset"));

            var rodGo = new GameObject("FishingRod");
            rodGo.transform.position = new Vector3(-5f, 0f, -2f);
            var rod = rodGo.AddComponent<FishingRod>();
            // VR 입력·햅틱 어댑터 (design/fishing-detailed step-07) — rod/spot 배선
            var rodInput = rodGo.AddComponent<FishingRodInput>();
            SetObjectRef(rodInput, "m_rod", rod);
            SetObjectRef(rodInput, "m_spot", spot);

            // 요리
            var potGo = new GameObject("CookingPot");
            potGo.transform.position = new Vector3(0f, 0f, 2f);
            AddVisual(potGo, PrimitiveType.Cylinder, new Color(0.3f, 0.3f, 0.35f), "Mat_Pot",
                      localPos: new Vector3(0f, 0.5f, 0f));
            var pot = potGo.AddComponent<CookingPot>();
            SetObjectArray(pot, "m_recipes",
                LoadRequired<RecipeDef>("Assets/Data/Recipes/Recipe_GrilledFish.asset"),
                LoadRequired<RecipeDef>("Assets/Data/Recipes/Recipe_TroutSteak.asset"),
                LoadRequired<RecipeDef>("Assets/Data/Recipes/Recipe_GoldenCarpBraise.asset"));
            SetObjectRef(pot, "m_failResult", LoadRequired<ItemDef>("Assets/Data/Items/Item_BurntFood.asset"));

            // 영지
            var estateGo = new GameObject("EstateManager");
            var estateManager = estateGo.AddComponent<EstateManager>();

            // 네트워크 (세션 + 음성)
            var networkGo = new GameObject("Network");
            var launcher = networkGo.AddComponent<SessionLauncher>();
            var voice    = networkGo.AddComponent<VoiceController>();
            networkGo.AddComponent<PlayerMute>();
            SetObjectRef(voice, "m_voicePlayerPrefab",
                LoadRequired<NetworkObject>("Assets/Prefabs/VoicePlayer.prefab"));

            // 네트워크 아바타(세션마다 몸체 스폰) — 로컬 영속 리그는 PersistentPlayer 담당이라 여기선 배선하지 않는다.
            // 이 호출로 P0Playground 재생성 시에도 아바타 배선이 유지된다(기존엔 수동 배선이라 재생성 시 유실).
            AvatarSetupFactory.EnsureOnNetworkObject(networkGo);

            // 하네스 — 전 시스템 배선
            var harnessGo = new GameObject("P0Harness");
            var harness = harnessGo.AddComponent<P0Harness>();
            SetObjectRef(harness, "m_rod", rod);
            SetObjectRef(harness, "m_spot", spot);
            SetObjectRef(harness, "m_pot", pot);
            SetObjectRef(harness, "m_estateManager", estateManager);
            SetObjectRef(harness, "m_launcher", launcher);
            SetObjectRef(harness, "m_huntTargetPrefab", huntPrefab.GetComponent<NetworkObject>());
            SetObjectArray(harness, "m_estateCatalog",
                LoadRequired<EstateObjectDef>("Assets/Data/Estate/Estate_Tent.asset"),
                LoadRequired<EstateObjectDef>("Assets/Data/Estate/Estate_Lantern.asset"),
                LoadRequired<EstateObjectDef>("Assets/Data/Estate/Estate_CampChair.asset"),
                LoadRequired<EstateObjectDef>("Assets/Data/Estate/Estate_Planter.asset"),
                LoadRequired<EstateObjectDef>("Assets/Data/Estate/Estate_Deck.asset"),
                LoadRequired<EstateObjectDef>("Assets/Data/Estate/Estate_Campfire.asset"));

            EditorSceneManager.SaveScene(scene, k_scenePath);
        }

        // ── 헬퍼 ─────────────────────────────────────────────────────

        private static T LoadRequired<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                throw new System.InvalidOperationException(
                    $"[MakeAssets] 필수 에셋 없음: {path} — 선행 팩토리(P0 Data / Voice Player) 먼저 실행 필요");
            return asset;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }

        // 비주얼 자식 부착 — Collider 제거, localScale은 1 유지 (make-assets 규칙)
        private static void AddVisual(GameObject parent, PrimitiveType shape, Color color, string matName,
                                      Vector3 localPos = default)
        {
            var visual = GameObject.CreatePrimitive(shape);
            visual.name = "Visual";
            visual.transform.SetParent(parent.transform, false);
            visual.transform.localPosition = localPos;
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            SetMaterial(visual, color, matName);
        }

        private static void SetMaterial(GameObject go, Color color, string matName)
        {
            string matPath = $"{k_materialFolder}/{matName}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                // URP 프로젝트면 URP Lit, 아니면 Standard — 파이프라인에 맞는 셰이더 선택
                Shader shader = GraphicsSettings.currentRenderPipeline != null
                    ? Shader.Find("Universal Render Pipeline/Lit")
                    : Shader.Find("Standard");
                if (shader == null) shader = Shader.Find("Standard");

                mat = new Material(shader) { color = color };
                AssetDatabase.CreateAsset(mat, matPath);
            }
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private static void SetObjectRef(Component comp, string fieldName, Object value)
        {
            var so = new SerializedObject(comp);
            var prop = so.FindProperty(fieldName);
            if (prop == null)
                throw new System.InvalidOperationException($"[MakeAssets] 필드 없음: {comp.GetType().Name}.{fieldName}");
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjectArray(Component comp, string fieldName, params Object[] values)
        {
            var so = new SerializedObject(comp);
            var prop = so.FindProperty(fieldName);
            if (prop == null)
                throw new System.InvalidOperationException($"[MakeAssets] 필드 없음: {comp.GetType().Name}.{fieldName}");
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
