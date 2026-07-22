#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CampLantern.EditorTools
{
    /// <summary>
    /// 프리미티브 조합으로 플레이스홀더 3D 형태를 개선한다(진짜 모델 전까지).
    ///
    /// 방식 = "아트 스왑": 프리팹의 "Visual" 자식만 교체하고 루트(NetworkObject/HuntTarget/PlacedObject 등)와
    /// 베이킹은 보존한다 → 프리팹 GUID·네트워크 참조 불변. 실제 FBX가 생기면 같은 "Visual" 지점을 교체하면 된다.
    ///
    /// Unity 기본 프리미티브는 원뿔/피라미드가 없어 큐브/실린더/스피어/캡슐 조합으로 실루엣만 맞춘다.
    /// RULE-02: .prefab 직접 편집 없음 — PrefabUtility.LoadPrefabContents + SaveAsPrefabAsset.
    /// </summary>
    public static class PlaceholderArtFactory
    {
        private const string k_materialFolder = "Assets/Prefabs/Materials";

        // 부위 하나 = 프리미티브 + 로컬 위치/스케일/회전 + 색.
        private struct Part
        {
            public PrimitiveType shape;
            public Vector3 pos, scale, euler;
            public string hex;
        }

        private static Part P(PrimitiveType s, Vector3 pos, Vector3 scale, string hex, Vector3 euler = default)
            => new Part { shape = s, pos = pos, scale = scale, hex = hex, euler = euler };

        [MenuItem("Tools/Make Assets/Upgrade Placeholder Visuals (Animals)")]
        public static void UpgradeAnimals()
        {
            const string brown = "#6b4a32";     // 몸통
            const string darkBrown = "#4a3222";  // 다리/발굽
            const string antler = "#d9cba8";     // 뿔
            const string tusk = "#efe6cf";       // 엄니
            const string snout = "#7d5942";      // 주둥이

            // ── 큰뿔사슴: 키 크고 뿔 큼 (forward = +Z) ──
            var elk = new List<Part>
            {
                P(PrimitiveType.Cube,     new Vector3(0f, 1.15f, 0f),   new Vector3(0.75f, 0.7f, 1.5f),  brown),      // 몸통
                P(PrimitiveType.Cube,     new Vector3(0f, 1.45f, 0.7f), new Vector3(0.42f, 0.6f, 0.5f),  brown),      // 목
                P(PrimitiveType.Cube,     new Vector3(0f, 1.7f, 0.95f), new Vector3(0.42f, 0.42f, 0.6f), brown),      // 머리
                P(PrimitiveType.Cube,     new Vector3(0f, 1.62f, 1.25f),new Vector3(0.22f, 0.22f, 0.3f), snout),      // 주둥이
                // 다리 4
                P(PrimitiveType.Cylinder, new Vector3(0.28f, 0.55f, 0.5f),  new Vector3(0.13f, 0.55f, 0.13f), darkBrown),
                P(PrimitiveType.Cylinder, new Vector3(-0.28f, 0.55f, 0.5f), new Vector3(0.13f, 0.55f, 0.13f), darkBrown),
                P(PrimitiveType.Cylinder, new Vector3(0.28f, 0.55f, -0.5f), new Vector3(0.13f, 0.55f, 0.13f), darkBrown),
                P(PrimitiveType.Cylinder, new Vector3(-0.28f, 0.55f, -0.5f),new Vector3(0.13f, 0.55f, 0.13f), darkBrown),
                // 뿔 (양쪽 기둥 + 가지)
                P(PrimitiveType.Cylinder, new Vector3(0.16f, 2.05f, 0.95f), new Vector3(0.06f, 0.35f, 0.06f), antler),
                P(PrimitiveType.Cylinder, new Vector3(-0.16f, 2.05f, 0.95f),new Vector3(0.06f, 0.35f, 0.06f), antler),
                P(PrimitiveType.Cylinder, new Vector3(0.3f, 2.15f, 1.05f),  new Vector3(0.05f, 0.22f, 0.05f), antler, new Vector3(0f, 0f, -40f)),
                P(PrimitiveType.Cylinder, new Vector3(-0.3f, 2.15f, 1.05f), new Vector3(0.05f, 0.22f, 0.05f), antler, new Vector3(0f, 0f, 40f)),
                P(PrimitiveType.Cylinder, new Vector3(0.22f, 2.2f, 0.8f),   new Vector3(0.05f, 0.18f, 0.05f), antler, new Vector3(30f, 0f, -20f)),
                P(PrimitiveType.Cylinder, new Vector3(-0.22f, 2.2f, 0.8f),  new Vector3(0.05f, 0.18f, 0.05f), antler, new Vector3(30f, 0f, 20f)),
                // 꼬리
                P(PrimitiveType.Cube,     new Vector3(0f, 1.2f, -0.78f), new Vector3(0.12f, 0.25f, 0.12f), brown),
            };

            // ── 멧돼지: 낮고 다부짐, 엄니 (forward = +Z) ──
            var boar = new List<Part>
            {
                P(PrimitiveType.Cube,     new Vector3(0f, 0.62f, 0f),   new Vector3(0.85f, 0.62f, 1.25f), brown),     // 몸통
                P(PrimitiveType.Cube,     new Vector3(0f, 0.6f, 0.72f), new Vector3(0.52f, 0.55f, 0.5f),  brown),     // 머리(큼)
                P(PrimitiveType.Cube,     new Vector3(0f, 0.5f, 1.02f), new Vector3(0.3f, 0.26f, 0.26f),  snout),     // 주둥이
                // 귀
                P(PrimitiveType.Cube,     new Vector3(0.18f, 0.9f, 0.62f), new Vector3(0.12f, 0.14f, 0.06f), brown),
                P(PrimitiveType.Cube,     new Vector3(-0.18f, 0.9f, 0.62f),new Vector3(0.12f, 0.14f, 0.06f), brown),
                // 다리 4 (짧음)
                P(PrimitiveType.Cylinder, new Vector3(0.28f, 0.3f, 0.4f),  new Vector3(0.14f, 0.3f, 0.14f), darkBrown),
                P(PrimitiveType.Cylinder, new Vector3(-0.28f, 0.3f, 0.4f), new Vector3(0.14f, 0.3f, 0.14f), darkBrown),
                P(PrimitiveType.Cylinder, new Vector3(0.28f, 0.3f, -0.4f), new Vector3(0.14f, 0.3f, 0.14f), darkBrown),
                P(PrimitiveType.Cylinder, new Vector3(-0.28f, 0.3f, -0.4f),new Vector3(0.14f, 0.3f, 0.14f), darkBrown),
                // 엄니 2 (앞으로 휘게 회전)
                P(PrimitiveType.Cylinder, new Vector3(0.12f, 0.46f, 1.12f), new Vector3(0.045f, 0.13f, 0.045f), tusk, new Vector3(60f, 0f, 15f)),
                P(PrimitiveType.Cylinder, new Vector3(-0.12f, 0.46f, 1.12f),new Vector3(0.045f, 0.13f, 0.045f), tusk, new Vector3(60f, 0f, -15f)),
            };

            const string bearBrown = "#4a3526";

            // ── 곰: 크고 둥글며 두꺼운 다리 (forward = +Z) ──
            var bear = new List<Part>
            {
                P(PrimitiveType.Cube,   new Vector3(0f, 0.85f, 0f),   new Vector3(1.0f, 0.85f, 1.5f), bearBrown), // 몸통(큼)
                P(PrimitiveType.Sphere, new Vector3(0f, 1.0f, 0.9f),  new Vector3(0.6f, 0.6f, 0.6f),  bearBrown), // 머리(둥금)
                P(PrimitiveType.Cube,   new Vector3(0f, 0.9f, 1.2f),  new Vector3(0.28f, 0.25f, 0.3f), snout),     // 주둥이
                P(PrimitiveType.Sphere, new Vector3(0.22f, 1.35f, 0.85f),  new Vector3(0.2f, 0.2f, 0.15f), bearBrown), // 귀
                P(PrimitiveType.Sphere, new Vector3(-0.22f, 1.35f, 0.85f), new Vector3(0.2f, 0.2f, 0.15f), bearBrown),
                // 다리 4 (두꺼움)
                P(PrimitiveType.Cylinder, new Vector3(0.35f, 0.42f, 0.55f),  new Vector3(0.2f, 0.42f, 0.2f), darkBrown),
                P(PrimitiveType.Cylinder, new Vector3(-0.35f, 0.42f, 0.55f), new Vector3(0.2f, 0.42f, 0.2f), darkBrown),
                P(PrimitiveType.Cylinder, new Vector3(0.35f, 0.42f, -0.55f), new Vector3(0.2f, 0.42f, 0.2f), darkBrown),
                P(PrimitiveType.Cylinder, new Vector3(-0.35f, 0.42f, -0.55f),new Vector3(0.2f, 0.42f, 0.2f), darkBrown),
            };

            int n = 0;
            n += Rebuild("Assets/Prefabs/HuntTarget.prefab", elk) ? 1 : 0;
            n += Rebuild("Assets/Prefabs/HuntTarget_WildBoar.prefab", boar) ? 1 : 0;
            n += Rebuild("Assets/Prefabs/HuntTarget_Bear.prefab", bear) ? 1 : 0;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[MakeAssets] 동물 플레이스홀더 비주얼 개선 완료 — {n}개 프리팹 (사슴/멧돼지/곰)");
        }

        [MenuItem("Tools/Make Assets/Upgrade Placeholder Visuals (Estate)")]
        public static void UpgradeEstate()
        {
            const string canvas = "#c98a4a", canvasDark = "#8a5a2a";        // 텐트
            const string gold = "#c9a53a", glow = "#fff3b0", metalDark = "#7a6320"; // 랜턴
            const string green = "#6a8a5a", legDark = "#46583a";             // 의자
            const string pot = "#b5651d", soil = "#3a2a1a", leaf = "#5aa04a"; // 화분
            const string wood = "#9a6a3a", woodDark = "#6a4a25", woodDark2 = "#8a5a2f"; // 데크/장작
            const string flame = "#e8641a", flameCore = "#ffd23a";           // 화롯불

            var tent = new List<Part>
            {
                // z+회전은 윗부분을 -x로 눕힘 → 왼쪽(-x) 패널 -28°, 오른쪽 +28°이어야 꼭대기에서 만나는 Λ자.
                // 부호가 반대면 V자로 무너져 보인다 (MapBuildUtil.BuildTent와 동일 규칙 — 2026-07-22 시각 검증에서 발견)
                P(PrimitiveType.Cube, new Vector3(-0.3f, 0.55f, 0f), new Vector3(0.06f, 1.1f, 1.3f), canvas, new Vector3(0f, 0f, -28f)),
                P(PrimitiveType.Cube, new Vector3(0.3f, 0.55f, 0f),  new Vector3(0.06f, 1.1f, 1.3f), canvas, new Vector3(0f, 0f, 28f)),
                P(PrimitiveType.Cube, new Vector3(0f, 0.35f, 0.62f), new Vector3(0.35f, 0.5f, 0.04f), canvasDark), // 앞막
            };

            var lantern = new List<Part>
            {
                P(PrimitiveType.Cylinder, new Vector3(0f, 0.1f, 0f),  new Vector3(0.3f, 0.1f, 0.3f),   metalDark),
                P(PrimitiveType.Cylinder, new Vector3(0f, 0.45f, 0f), new Vector3(0.28f, 0.3f, 0.28f), glow),
                P(PrimitiveType.Cylinder, new Vector3(0f, 0.75f, 0f), new Vector3(0.32f, 0.08f, 0.32f), gold),
                P(PrimitiveType.Cube,     new Vector3(0f, 0.9f, 0f),  new Vector3(0.05f, 0.18f, 0.05f), metalDark),
            };

            var chair = new List<Part>
            {
                P(PrimitiveType.Cube, new Vector3(0f, 0.45f, 0f),    new Vector3(0.5f, 0.06f, 0.5f), green),
                P(PrimitiveType.Cube, new Vector3(0f, 0.7f, -0.22f), new Vector3(0.5f, 0.5f, 0.06f), green),
                P(PrimitiveType.Cylinder, new Vector3(0.2f, 0.22f, 0.2f),   new Vector3(0.05f, 0.22f, 0.05f), legDark),
                P(PrimitiveType.Cylinder, new Vector3(-0.2f, 0.22f, 0.2f),  new Vector3(0.05f, 0.22f, 0.05f), legDark),
                P(PrimitiveType.Cylinder, new Vector3(0.2f, 0.22f, -0.2f),  new Vector3(0.05f, 0.22f, 0.05f), legDark),
                P(PrimitiveType.Cylinder, new Vector3(-0.2f, 0.22f, -0.2f), new Vector3(0.05f, 0.22f, 0.05f), legDark),
            };

            var planter = new List<Part>
            {
                P(PrimitiveType.Cylinder, new Vector3(0f, 0.25f, 0f), new Vector3(0.4f, 0.25f, 0.4f),   pot),
                P(PrimitiveType.Cylinder, new Vector3(0f, 0.48f, 0f), new Vector3(0.36f, 0.03f, 0.36f), soil),
                P(PrimitiveType.Sphere,   new Vector3(0.12f, 0.65f, 0f),  new Vector3(0.28f, 0.28f, 0.28f), leaf),
                P(PrimitiveType.Sphere,   new Vector3(-0.12f, 0.62f, 0.08f), new Vector3(0.26f, 0.26f, 0.26f), leaf),
                P(PrimitiveType.Sphere,   new Vector3(0f, 0.78f, -0.05f), new Vector3(0.24f, 0.24f, 0.24f), leaf),
            };

            var deck = new List<Part>
            {
                P(PrimitiveType.Cube, new Vector3(0f, 0.4f, 0f), new Vector3(1.4f, 0.1f, 1.0f), wood),
                P(PrimitiveType.Cylinder, new Vector3(0.6f, 0.2f, 0.4f),   new Vector3(0.08f, 0.2f, 0.08f), woodDark),
                P(PrimitiveType.Cylinder, new Vector3(-0.6f, 0.2f, 0.4f),  new Vector3(0.08f, 0.2f, 0.08f), woodDark),
                P(PrimitiveType.Cylinder, new Vector3(0.6f, 0.2f, -0.4f),  new Vector3(0.08f, 0.2f, 0.08f), woodDark),
                P(PrimitiveType.Cylinder, new Vector3(-0.6f, 0.2f, -0.4f), new Vector3(0.08f, 0.2f, 0.08f), woodDark),
            };

            var campfire = new List<Part>
            {
                P(PrimitiveType.Cylinder, new Vector3(0f, 0.12f, 0f), new Vector3(0.1f, 0.5f, 0.1f), woodDark,  new Vector3(90f, 20f, 0f)),
                P(PrimitiveType.Cylinder, new Vector3(0f, 0.12f, 0f), new Vector3(0.1f, 0.5f, 0.1f), woodDark2, new Vector3(90f, -20f, 0f)),
                P(PrimitiveType.Capsule,  new Vector3(0f, 0.45f, 0f), new Vector3(0.25f, 0.3f, 0.25f), flame),
                P(PrimitiveType.Capsule,  new Vector3(0f, 0.4f, 0f),  new Vector3(0.14f, 0.2f, 0.14f), flameCore),
            };

            int n = 0;
            n += Rebuild("Assets/Prefabs/Estate/Estate_Tent.prefab", tent) ? 1 : 0;
            n += Rebuild("Assets/Prefabs/Estate/Estate_Lantern.prefab", lantern) ? 1 : 0;
            n += Rebuild("Assets/Prefabs/Estate/Estate_CampChair.prefab", chair) ? 1 : 0;
            n += Rebuild("Assets/Prefabs/Estate/Estate_Planter.prefab", planter) ? 1 : 0;
            n += Rebuild("Assets/Prefabs/Estate/Estate_Deck.prefab", deck) ? 1 : 0;
            n += Rebuild("Assets/Prefabs/Estate/Estate_Campfire.prefab", campfire) ? 1 : 0;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[MakeAssets] 영지 오브젝트 플레이스홀더 비주얼 개선 완료 — {n}개 프리팹");
        }

        /// <summary>프리팹의 "Visual" 자식을 parts 조합으로 재구성. 루트 컴포넌트/베이킹은 보존. 변경 시 true.</summary>
        private static bool Rebuild(string prefabPath, List<Part> parts)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            {
                Debug.LogWarning($"[MakeAssets] 프리팹 없음, 건너뜀: {prefabPath}");
                return false;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                // 기존 Visual 제거
                foreach (Transform child in root.transform.Cast<Transform>().ToList())
                    if (child.name == "Visual") Object.DestroyImmediate(child.gameObject);

                var visual = new GameObject("Visual");
                visual.transform.SetParent(root.transform, false);

                foreach (Part p in parts)
                {
                    var go = GameObject.CreatePrimitive(p.shape);
                    go.name = "Part";
                    Object.DestroyImmediate(go.GetComponent<Collider>()); // 시각용만 — 콜라이더 불필요
                    go.transform.SetParent(visual.transform, false);
                    go.transform.localPosition = p.pos;
                    go.transform.localScale    = p.scale;
                    go.transform.localRotation = Quaternion.Euler(p.euler);
                    go.GetComponent<Renderer>().sharedMaterial = Mat(p.hex);
                }

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                return true;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // 색상별 머티리얼 캐시(공유) — 드로우콜 절약.
        private static Material Mat(string hex)
        {
            string safe = hex.Replace("#", "");
            string path = $"{k_materialFolder}/Mat_PH_{safe}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            if (!AssetDatabase.IsValidFolder(k_materialFolder))
                AssetDatabase.CreateFolder("Assets/Prefabs", "Materials");

            Shader shader = GraphicsSettings.currentRenderPipeline != null
                ? Shader.Find("Universal Render Pipeline/Lit")
                : Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Standard");

            ColorUtility.TryParseHtmlString(hex, out Color c);
            mat = new Material(shader) { color = c };
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }
    }
}
#endif
