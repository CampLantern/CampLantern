#if UNITY_EDITOR
using CampLantern.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CampLantern.EditorTools
{
    /// <summary>
    /// 전투 로컬 검증 샌드박스 씬 팩토리 (design/combat-detailed step-04).
    ///
    /// **네트워크 없음** — SessionLauncher/NetworkRunner 배치 금지. VR 리그는 PersistentPlayer가
    /// 어느 씬에서든 자동 스폰(리그 2겹 구조)하므로 씬에는 PlayerSpawnPoint 마커만 둔다.
    ///
    /// **멱등(Ensure)** — 씬이 있으면 열어서 구성 요소를 이름으로 찾아 없는 것만 추가한다
    /// (RoomScenesFactory의 "있으면 early-return"과 달리, 이후 단계 산출물이 생길 때마다 재실행해 확장).
    /// 기존 오브젝트의 수동 조정(위치 등)은 덮어쓰지 않는다.
    ///
    /// 무기는 4종 전부 배치 — Bow/Arrow의 전투 배선(step-08)은 프리팹에 들어가므로
    /// 씬의 프리팹 인스턴스는 자동 갱신된다(팩토리 재수정 불필요).
    ///
    /// EditorBuildSettings 등록은 하지 않는다 — 이 씬은 에디터에서만 열리고 런타임 이름 로드가 없다.
    /// RULE-02: .unity 직접 작성 금지 — EditorSceneManager API로만 생성/저장.
    /// </summary>
    public static class CombatSandboxFactory
    {
        private const string k_scenePath = "Assets/Scenes/CombatSandbox.unity";
        private const string k_materialFolder = "Assets/Prefabs/Materials";
        private const string k_bearPrefabPath = "Assets/Prefabs/Combat/CombatMonster_Bear.prefab";

        private static readonly (string name, string path, Vector3 pos)[] k_weapons =
        {
            ("Weapon_Sword", "Assets/Prefabs/Weapons/Sword.prefab", new Vector3(-1.5f, 1.1f, 1f)),
            ("Weapon_Spear", "Assets/Prefabs/Weapons/Spear.prefab", new Vector3(-2.1f, 1.1f, 1f)),
            ("Weapon_Bow",   "Assets/Prefabs/Weapons/Bow.prefab",   new Vector3(-2.7f, 1.1f, 1f)),
            ("Weapon_Arrow", "Assets/Prefabs/Weapons/Arrow.prefab", new Vector3(-3.3f, 1.1f, 1f)),
        };

        [MenuItem("Tools/Make Assets/Combat Sandbox Scene (Create or Update)")]
        public static void CreateOrUpdate()
        {
            EditorSceneManager.SaveOpenScenes();

            bool isNew = AssetDatabase.LoadAssetAtPath<SceneAsset>(k_scenePath) == null;
            Scene scene = isNew
                ? EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single)
                : EditorSceneManager.OpenScene(k_scenePath, OpenSceneMode.Single);

            int added = 0;

            if (isNew)
            {
                var camera = GameObject.Find("Main Camera");
                if (camera != null)
                    camera.transform.SetPositionAndRotation(new Vector3(0f, 8f, -10f), Quaternion.Euler(30f, 0f, 0f));
            }

            // 지면 — 60x60m (감지 6m·추격 15m 반경 테스트 여유)
            if (FindRoot(scene, "Ground") == null)
            {
                var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.name = "Ground";
                ground.transform.localScale = new Vector3(6f, 1f, 6f);
                SetMaterial(ground, new Color(0.42f, 0.45f, 0.32f), "Mat_CombatGround");
                added++;
            }

            // 스폰 포인트 — PersistentPlayer가 씬 로드 시 이동
            if (FindRoot(scene, "PlayerSpawnPoint") == null)
            {
                var spawn = new GameObject("PlayerSpawnPoint");
                spawn.AddComponent<PlayerSpawnPoint>();
                spawn.transform.position = Vector3.zero;
                added++;
            }

            // 곰 몬스터 — 스폰 지점 = 배치 위치 (MonsterController.Awake가 캡처)
            if (FindRoot(scene, "CombatMonster_Bear") == null)
            {
                var bearPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(k_bearPrefabPath);
                if (bearPrefab == null)
                {
                    Debug.LogError($"[MakeAssets] 곰 프리팹 없음: {k_bearPrefabPath} — 먼저 Tools > Make Assets > Combat Monster — Bear");
                }
                else
                {
                    var bear = (GameObject)PrefabUtility.InstantiatePrefab(bearPrefab);
                    bear.transform.position = new Vector3(0f, 0f, 10f); // 감지(6m) 밖에서 시작 — 배회 관찰 가능
                    added++;
                }
            }

            // 무기 테이블 + 무기 4종 (프리팹 인스턴스 — 이후 단계의 프리팹 배선이 자동 반영됨)
            if (FindRoot(scene, "WeaponTable") == null)
            {
                var table = GameObject.CreatePrimitive(PrimitiveType.Cube);
                table.name = "WeaponTable";
                table.transform.position = new Vector3(-2.4f, 0.5f, 1f);
                table.transform.localScale = new Vector3(2.4f, 1f, 0.8f);
                SetMaterial(table, new Color(0.4f, 0.3f, 0.2f), "Mat_CombatTable");
                added++;
            }
            foreach (var (name, path, pos) in k_weapons)
            {
                if (FindRoot(scene, name) != null) continue;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    Debug.LogWarning($"[MakeAssets] 무기 프리팹 없음, 건너뜀: {path}");
                    continue;
                }
                var weapon = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                weapon.name = name;
                weapon.transform.position = pos; // 테이블 위 — 플레이 시 중력으로 안착
                added++;
            }

            // 더미 플레이어 마커 2개 (비활성) — step-05 어그로 2인/step-09 소생/step-11 하네스가 활성화·등록
            for (int i = 1; i <= 2; i++)
            {
                string markerName = $"DummyPlayer_{i}";
                if (FindRoot(scene, markerName) != null) continue;
                var marker = new GameObject(markerName);
                marker.transform.position = new Vector3(2f * i, 0f, 2f);
                marker.SetActive(false);
                added++;
            }

            EditorSceneManager.SaveScene(scene, k_scenePath);
            Debug.Log($"[MakeAssets] CombatSandbox {(isNew ? "created" : "updated")}: {k_scenePath} (added {added})");
        }

        [MenuItem("Tools/Make Assets/Open Combat Sandbox Scene")]
        public static void OpenScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(k_scenePath) == null)
            {
                Debug.LogError($"[MakeAssets] 씬 없음: {k_scenePath} — Combat Sandbox Scene (Create or Update) 먼저 실행");
                return;
            }
            EditorSceneManager.SaveOpenScenes();
            EditorSceneManager.OpenScene(k_scenePath, OpenSceneMode.Single);
        }

        /// <summary>씬 루트에서 이름으로 탐색 — 비활성 오브젝트 포함 (GameObject.Find는 비활성을 못 찾는다).</summary>
        private static GameObject FindRoot(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == name) return root;
            return null;
        }

        // RoomScenesFactory.SetMaterial과 동일 패턴 (private라 최소 복제)
        private static void SetMaterial(GameObject go, Color color, string matName)
        {
            string matPath = $"{k_materialFolder}/{matName}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                Shader shader = GraphicsSettings.currentRenderPipeline != null
                    ? Shader.Find("Universal Render Pipeline/Lit")
                    : Shader.Find("Standard");
                if (shader == null) shader = Shader.Find("Standard");
                mat = new Material(shader) { color = color };
                AssetDatabase.CreateAsset(mat, matPath);
            }
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }
}
#endif
