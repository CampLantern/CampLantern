#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using CampLantern.Fishing;
using UnityEditor;
using UnityEngine;

namespace CampLantern.EditorTools
{
    /// <summary>
    /// RealVRFishing 태클 에셋(2026-07-09 병합, Assets/RealVRFishing/)을 낚시 비주얼로 이식한다.
    ///
    /// - RVRF의 .prefab은 이 프로젝트에 없는 게임 스크립트를 물고 있어(missing script) 쓰지 않는다 —
    ///   **FBX 모델만** 비주얼 소스로 사용.
    /// - RVRF 머티리얼은 빌트인 셰이더(Standard/Diffuse)라 URP에서 마젠타 → URP/Lit으로 변환
    ///   (_MainTex→_BaseMap, _Color→_BaseColor). 팀 공용 수정이며 idempotent.
    /// - 교체는 "Visual 자식 스왑" 원칙(PlaceholderArtFactory와 동일): Stations/FishingRod.prefab의
    ///   로직(FishingRod+BoxCollider)은 보존하고 Visual만 Rod+Reel FBX로 재구성 → 프리팹 인스턴스를 쓰는
    ///   모든 씬(FishingGround/P0Playground)에 자동 반영.
    /// RULE-02: 에셋 편집은 전부 Unity API(Material/PrefabUtility)로만.
    /// </summary>
    public static class RvrfTackleFactory
    {
        private const string k_rodPrefabPath = "Assets/Prefabs/Stations/FishingRod.prefab";
        private const string k_rodFbxPath    = "Assets/RealVRFishing/Models/Tackles/Rod/Rod_Float_01.fbx";
        private const string k_reelFbxPath   = "Assets/RealVRFishing/Models/Tackles/Reel/Reel_Float_01.fbx";
        private const string k_rodMatPath    = "Assets/RealVRFishing/Models/Tackles/Rod/Materials/Rod_Float_01.mat";
        private const string k_reelMatPath   = "Assets/RealVRFishing/Models/Tackles/Reel/Materials/Reel_Float_01.mat";
        private const string k_bobberFbxPath = "Assets/RealVRFishing/Models/Tackles/Bobbers/Bobber0.fbx";
        private const string k_bobberMatPath = "Assets/RealVRFishing/Models/Tackles/Bobbers/Materials/Bobber0.mat";
        private const string k_baitFbxPath   = "Assets/RealVRFishing/Models/Tackles/Baits/Waxworm_ani.fbx";
        private const string k_baitMatPath   = "Assets/RealVRFishing/Models/Tackles/Baits/Materials/Waxworm.mat";

        // 빌트인 → URP 변환 대상 (태클 4종 본체 머티리얼)
        private static readonly string[] k_builtinMats =
        {
            k_rodMatPath,
            k_reelMatPath,
            "Assets/RealVRFishing/Models/Tackles/Bobbers/Materials/Bobber0.mat",
            "Assets/RealVRFishing/Models/Tackles/Baits/Materials/Waxworm.mat",
        };

        // ── 배치 튜닝 상수 — LogModelBounds 실측 기반 ──
        // Rod FBX: +Y로 4.51m, 피벗=손잡이 끝(밑동). Reel FBX: 0.21m, 피벗=마운트 지점(릴이 아래로 매달림).
        private const float k_rodScale = 0.45f;                            // 4.5m 실물 → ~2.0m 코지 비율
        private static readonly Vector3 k_rodLocalPos   = new Vector3(0f, 0.7f, 0.1f);  // 밑동 = 손 높이
        private static readonly Vector3 k_rodLocalEuler = new Vector3(28f, 0f, 0f);     // 구 플레이스홀더와 동일한 전방 기울기
        private const float k_reelHeightAlongRod = 0.35f;                  // 손잡이 위 릴 마운트 거리(월드 m)

        [MenuItem("Tools/Make Assets/RVRF Tackle — Swap Rod Visual")]
        public static void SwapRodVisual()
        {
            ConvertMaterialsToUrp();

            var rodFbx  = AssetDatabase.LoadAssetAtPath<GameObject>(k_rodFbxPath);
            var reelFbx = AssetDatabase.LoadAssetAtPath<GameObject>(k_reelFbxPath);
            if (rodFbx == null || AssetDatabase.LoadAssetAtPath<GameObject>(k_rodPrefabPath) == null)
            {
                Debug.LogError($"[MakeAssets] 필수 에셋 없음 — rodFbx:{rodFbx != null}, rodPrefab 존재 확인 필요");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(k_rodPrefabPath);
            try
            {
                // 재실행 안전 — 이전 스왑 산출물 전부 제거
                foreach (Transform child in root.transform.Cast<Transform>().ToList())
                    if (child.name == "Visual" || child.name == "Bobber" || child.name == "Line")
                        Object.DestroyImmediate(child.gameObject);
                var oldVisual = root.GetComponent<FishingLineVisual>();
                if (oldVisual != null) Object.DestroyImmediate(oldVisual);

                var visual = new GameObject("Visual");
                visual.transform.SetParent(root.transform, false);

                GameObject rodInst = AttachModel(rodFbx, visual.transform, "Rod",
                    k_rodLocalPos, k_rodLocalEuler, Vector3.one * k_rodScale,
                    AssetDatabase.LoadAssetAtPath<Material>(k_rodMatPath));

                Transform tipAnchor = null;
                if (rodInst != null)
                {
                    if (reelFbx != null)
                    {
                        // 릴은 로드의 자식 — 로드 기울기를 그대로 상속. 부모 스케일(0.45)을 역보정해 실물 크기 유지.
                        AttachModel(reelFbx, rodInst.transform, "Reel",
                            new Vector3(0f, k_reelHeightAlongRod / k_rodScale, 0f),
                            Vector3.zero,
                            Vector3.one / k_rodScale,
                            AssetDatabase.LoadAssetAtPath<Material>(k_reelMatPath));
                    }

                    // 로드 팁 앵커 — 줄 시작점 (모델 로컬 상단 4.511m, 스케일 상속으로 실제 ~2.03m)
                    var tip = new GameObject("TipAnchor");
                    tip.transform.SetParent(rodInst.transform, false);
                    tip.transform.localPosition = new Vector3(0f, 4.511f, 0f);
                    tipAnchor = tip.transform;
                }

                // 찌 — 프리팹 루트 자식(월드 위치는 런타임에 FishingLineVisual이 지정). 초기 비표시는 소유 컴포넌트 Awake가 관리.
                GameObject bobber = null;
                GameObject bait = null;
                var bobberFbx = AssetDatabase.LoadAssetAtPath<GameObject>(k_bobberFbxPath);
                if (bobberFbx != null)
                {
                    bobber = AttachModel(bobberFbx, root.transform, "Bobber",
                        Vector3.zero, Vector3.zero, Vector3.one,
                        AssetDatabase.LoadAssetAtPath<Material>(k_bobberMatPath));

                    // 미끼 왁스웜 — 찌 아래 수중에 매달림. 실측 3cm라 가시성 위해 ×2 (연출 과장, 코지 톤).
                    var baitFbx = AssetDatabase.LoadAssetAtPath<GameObject>(k_baitFbxPath);
                    if (baitFbx != null && bobber != null)
                        bait = AttachModel(baitFbx, bobber.transform, "Bait",
                            new Vector3(0f, -0.14f, 0f), Vector3.zero, Vector3.one * 2f,
                            AssetDatabase.LoadAssetAtPath<Material>(k_baitMatPath));
                }

                // 낚싯줄 — LineRenderer (§2 줄 색 신호의 실체). Sprites/Default는 URP에서도 버텍스 컬러 지원.
                var lineGo = new GameObject("Line");
                lineGo.transform.SetParent(root.transform, false);
                var line = lineGo.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.positionCount = 2;
                line.widthMultiplier = 0.008f;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.sharedMaterial = GetOrCreateLineMaterial();

                // 연출 컴포넌트 배선
                var lineVisual = root.AddComponent<CampLantern.Fishing.FishingLineVisual>();
                SetRef(lineVisual, "m_rod", root.GetComponent<CampLantern.Fishing.FishingRod>());
                SetRef(lineVisual, "m_tipAnchor", tipAnchor);
                SetRef(lineVisual, "m_bobber", bobber);
                SetRef(lineVisual, "m_bait", bait);
                SetRef(lineVisual, "m_line", line);

                PrefabUtility.SaveAsPrefabAsset(root, k_rodPrefabPath);
                Debug.Log($"[MakeAssets] RVRF 낚싯대 비주얼 스왑 완료(로드+릴+찌+줄): {k_rodPrefabPath}");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // FBX를 자식으로 인스턴스화(원본 링크 유지)하고 머티리얼을 강제 배선(FBX 내장 머티리얼 마젠타 방지).
        // 콜라이더가 딸려오면 제거 — 상호작용 볼륨은 프리팹 루트의 BoxCollider 하나만 쓴다.
        private static GameObject AttachModel(GameObject fbx, Transform parent, string name,
                                              Vector3 pos, Vector3 euler, Vector3 scale, Material mat)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(fbx, parent.gameObject.scene);
            inst.name = name;
            inst.transform.SetParent(parent, false);
            inst.transform.localPosition = pos;
            inst.transform.localRotation = Quaternion.Euler(euler);
            inst.transform.localScale    = scale;

            foreach (Collider col in inst.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(col);

            if (mat != null)
            {
                foreach (Renderer r in inst.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = new Material[r.sharedMaterials.Length];
                    for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                    r.sharedMaterials = mats;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(r);
                }
            }
            return inst;
        }

        // 낚싯줄 머티리얼 — Sprites/Default(버텍스 컬러 지원, URP 호환). LineRenderer의 start/endColor로 §2 줄 색 표현.
        private static Material GetOrCreateLineMaterial()
        {
            const string path = "Assets/Prefabs/Materials/Mat_FishingLine.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            mat = new Material(Shader.Find("Sprites/Default"));
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static void SetRef(Component comp, string field, Object value)
        {
            var so = new SerializedObject(comp);
            var prop = so.FindProperty(field);
            if (prop == null)
                throw new System.InvalidOperationException($"[MakeAssets] 필드 없음: {comp.GetType().Name}.{field}");
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>FBX 실측 — 렌더러 합산 바운드를 로그(배치 상수 튜닝용 진단).</summary>
        [MenuItem("Tools/Make Assets/RVRF Tackle — Log Model Bounds")]
        public static void LogModelBounds()
        {
            LogBounds(k_rodFbxPath);
            LogBounds(k_reelFbxPath);
            LogBounds(k_bobberFbxPath);
            LogBounds(k_baitFbxPath);
        }

        private static void LogBounds(string path)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (fbx == null) { Debug.LogWarning($"[MakeAssets] 없음: {path}"); return; }

            var inst = Object.Instantiate(fbx);
            try
            {
                Renderer[] renderers = inst.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) { Debug.Log($"[MakeAssets] {path}: 렌더러 없음"); return; }
                Bounds b = renderers[0].bounds;
                foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
                Debug.Log($"[MakeAssets] {System.IO.Path.GetFileName(path)}: renderers={renderers.Length} " +
                          $"size=({b.size.x:F3}, {b.size.y:F3}, {b.size.z:F3}) center=({b.center.x:F3}, {b.center.y:F3}, {b.center.z:F3})");
            }
            finally { Object.DestroyImmediate(inst); }
        }

        /// <summary>빌트인 셰이더 머티리얼 → URP/Lit 변환 (idempotent). 텍스처·색 보존.</summary>
        [MenuItem("Tools/Make Assets/RVRF Tackle — Convert Materials To URP")]
        public static void ConvertMaterialsToUrp()
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                Debug.LogError("[MakeAssets] URP Lit 셰이더 없음 — URP 프로젝트인지 확인");
                return;
            }

            int converted = 0;
            foreach (string path in k_builtinMats)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) { Debug.LogWarning($"[MakeAssets] 머티리얼 없음: {path}"); continue; }
                if (mat.shader == urpLit) continue; // 이미 변환됨

                // 변환 전에 원본 값 채집 (셰이더 교체 후엔 프로퍼티가 사라짐)
                Texture mainTex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
                Color color = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;

                mat.shader = urpLit;
                if (mainTex != null) mat.SetTexture("_BaseMap", mainTex);
                mat.SetColor("_BaseColor", color);
                mat.SetFloat("_Smoothness", 0.25f); // 코지 톤 — 과한 반사 방지
                EditorUtility.SetDirty(mat);
                converted++;
            }

            if (converted > 0) AssetDatabase.SaveAssets();
            Debug.Log($"[MakeAssets] RVRF 머티리얼 URP 변환 — {converted}개 변환(총 {k_builtinMats.Length}개 대상)");
        }
    }
}
#endif
