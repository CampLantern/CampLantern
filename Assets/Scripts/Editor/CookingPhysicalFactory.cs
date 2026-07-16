#if UNITY_EDITOR
using System;
using System.Reflection;
using CampLantern.Cooking;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace CampLantern.EditorTools
{
    /// <summary>
    /// 물리 요리 프리팹 생성기 — IngredientPickup(재료 프록시) / CookingLadle(국자).
    /// 런타임 스폰용이라 Assets/Resources에 저장한다(PhysicalCookingBuilder가 Resources.Load).
    /// 그랩(Rigidbody+Collider+Grabbable+GrabInteractable)은 WeaponPrefabFactory와 동일하게
    /// 풀네임/리플렉션으로 배선한다(어셈블리 직접 참조 회피 — VRUIFactory.cs 상단 주석 참조).
    /// RULE-02: .prefab 텍스트 직접 작성 금지 — PrefabUtility.SaveAsPrefabAsset만 사용.
    /// 비주얼은 P0 그레이박스 플레이스홀더(프리미티브) — 등급 색·아이콘은 런타임에 MPB/Sprite로 입힌다.
    /// </summary>
    public static class CookingPhysicalFactory
    {
        private const string k_resourcesFolder = "Assets/Resources";
        private const string k_materialFolder  = "Assets/Prefabs/Materials";

        // Meta Interaction SDK 타입 (풀네임으로 해석 — 어셈블리 직접 참조 회피)
        private const string k_grabbableType       = "Oculus.Interaction.Grabbable";
        private const string k_grabInteractableType = "Oculus.Interaction.GrabInteractable";

        [MenuItem("Tools/Make Assets/Physical Cooking (Create All)")]
        public static void CreateAll() => CreateInternal(force: false);

        [MenuItem("Tools/Make Assets/Physical Cooking (Force Recreate)")]
        public static void ForceRecreate() => CreateInternal(force: true);

        private static void CreateInternal(bool force)
        {
            EnsureFolder(k_resourcesFolder);
            EnsureFolder("Assets/Prefabs");
            EnsureFolder(k_materialFolder);

            CreateIngredientPickup(force);
            CreateLadle(force);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MakeAssets] 물리 요리 프리팹 생성 완료: " + k_resourcesFolder);
        }

        // ── 재료 프록시 ─────────────────────────────────────────────
        // 작은 구체 + 아이콘. 색(등급 틴트)·스프라이트는 IngredientPickup.Initialize가 런타임에 설정.
        private static void CreateIngredientPickup(bool force)
        {
            const string path = k_resourcesFolder + "/IngredientPickup.prefab";
            if (Skip(path, force)) return;

            var root = new GameObject("IngredientPickup");
            try
            {
                var rb = root.AddComponent<Rigidbody>();
                rb.mass = 0.2f;
                rb.useGravity = true;
                rb.isKinematic = false;

                // 구르지 않도록 박스 콜라이더 (비주얼은 구체 — 이 크기에선 어긋남이 티 나지 않음)
                var col = root.AddComponent<BoxCollider>();
                col.size = new Vector3(0.11f, 0.11f, 0.11f);

                GameObject visual = AddVisual(root, PrimitiveType.Sphere, Color.white, "Mat_Ingredient",
                                              localPos: Vector3.zero,
                                              localScale: new Vector3(0.14f, 0.14f, 0.14f));

                var iconGo = new GameObject("Icon");
                iconGo.transform.SetParent(root.transform, false);
                iconGo.transform.localPosition = new Vector3(0f, 0.1f, 0f);
                iconGo.transform.localScale = new Vector3(0.15f, 0.15f, 0.15f);
                var icon = iconGo.AddComponent<SpriteRenderer>();

                var pickup = root.AddComponent<IngredientPickup>();
                SetObjectRef(pickup, "m_visual", visual.GetComponent<MeshRenderer>());
                SetObjectRef(pickup, "m_icon", icon);

                AddGrab(root, rb);

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }

        // ── 국자 ────────────────────────────────────────────────────
        // 손잡이(+Y 방향 0.36m) + 머리(스쿱). StirTool.Tip = 스쿱 — 젓기 판정 기준점.
        private static void CreateLadle(bool force)
        {
            const string path = k_resourcesFolder + "/CookingLadle.prefab";
            if (Skip(path, force)) return;

            var root = new GameObject("CookingLadle");
            try
            {
                var rb = root.AddComponent<Rigidbody>();
                rb.mass = 0.3f;
                rb.useGravity = true;
                rb.isKinematic = false;

                var col = root.AddComponent<BoxCollider>();
                col.center = new Vector3(0f, 0.2f, 0f);
                col.size = new Vector3(0.1f, 0.5f, 0.1f);

                AddVisual(root, PrimitiveType.Cylinder, new Color(0.5f, 0.36f, 0.22f), "Mat_LadleHandle",
                          localPos: new Vector3(0f, 0.18f, 0f),
                          localScale: new Vector3(0.03f, 0.18f, 0.03f));

                GameObject scoop = AddVisual(root, PrimitiveType.Sphere, new Color(0.25f, 0.25f, 0.28f), "Mat_LadleScoop",
                                             localPos: new Vector3(0f, 0.38f, 0f),
                                             localScale: new Vector3(0.09f, 0.09f, 0.09f));
                scoop.name = "Scoop";

                var tool = root.AddComponent<StirTool>();
                SetObjectRef(tool, "m_tip", scoop.transform);

                AddGrab(root, rb);

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }

        // ── 그랩 배선 (WeaponPrefabFactory.AddGrabPhysics와 동일 패턴) ──
        private static void AddGrab(GameObject root, Rigidbody rb)
        {
            Type grabbableType = FindType(k_grabbableType);
            Type grabInteractableType = FindType(k_grabInteractableType);
            if (grabbableType == null || grabInteractableType == null)
            {
                Debug.LogWarning($"[MakeAssets] {root.name}: Grabbable/GrabInteractable 타입을 못 찾음 — Meta Interaction SDK 설치 확인. 그랩 미배선.");
                return;
            }

            Component grabbable       = root.AddComponent(grabbableType);
            Component grabInteractable = root.AddComponent(grabInteractableType);

            InvokeInject(grabInteractableType, grabInteractable, "InjectRigidbody", rb);
            InvokeInject(grabInteractableType, grabInteractable, "InjectOptionalPointableElement", grabbable);
        }

        // ── 헬퍼 (StationFactory/WeaponPrefabFactory와 동일 패턴) ─────

        private static GameObject AddVisual(GameObject parent, PrimitiveType shape, Color color, string matName,
                                            Vector3 localPos, Vector3 localScale)
        {
            var visual = GameObject.CreatePrimitive(shape);
            visual.name = "Visual";
            visual.transform.SetParent(parent.transform, false);
            visual.transform.localPosition = localPos;
            visual.transform.localScale = localScale;
            Object.DestroyImmediate(visual.GetComponent<Collider>()); // 콜라이더는 루트에서 별도 사이징
            SetMaterial(visual, color, matName);
            return visual;
        }

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

        private static void SetObjectRef(Component comp, string fieldName, Object value)
        {
            var so = new SerializedObject(comp);
            var prop = so.FindProperty(fieldName)
                       ?? throw new InvalidOperationException($"[MakeAssets] 필드 없음: {comp.GetType().Name}.{fieldName}");
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void InvokeInject(Type targetType, object target, string methodName, object arg)
        {
            MethodInfo method = targetType.GetMethod(methodName);
            if (method == null)
            {
                Debug.LogWarning($"[MakeAssets] {targetType.Name}.{methodName} 메서드 없음 — SDK 버전 확인");
                return;
            }
            method.Invoke(target, new[] { arg });
        }

        private static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName);
                if (t != null) return t;
            }
            return null;
        }

        private static bool Skip(string path, bool force)
        {
            if (!force && AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                Debug.LogWarning($"[MakeAssets] 이미 존재합니다. Force Recreate 메뉴를 사용하세요: {path}");
                return true;
            }
            return false;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
#endif
