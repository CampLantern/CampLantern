#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace CampLantern.EditorTools
{
    /// <summary>
    /// "Free medieval weapons" 에셋(Assets/FreeMedievalWeapons/Prefabs)의 원본 비주얼 프리팹을 감싸
    /// VR 그랩 가능한 게임 프리팹(Sword/Bow/Arrow)을 만든다.
    ///
    /// 원본 비주얼은 PrefabUtility.InstantiatePrefab으로 중첩 프리팹 연결한 채 자식으로 붙인다
    /// (메시/머티리얼/본을 손으로 재조립하지 않음 — Wooden Bow는 SkinnedMeshRenderer+Avatar 포함).
    /// 그랩은 Meta Interaction SDK의 물리 그랩(Rigidbody+Collider+Grabbable+GrabInteractable)만 배선한다.
    /// 손 포즈(HandGrabInteractable)는 포즈 데이터가 없어 미배선 — 그립 위치·손모양은 Editor에서 후조정 필요.
    ///
    /// Meta Interaction 타입은 VRUIFactory와 동일하게 풀네임/리플렉션으로 참조한다(어셈블리 직접
    /// 참조 회피로 컴파일 격리 — VRUIFactory.cs 상단 주석 참조).
    ///
    /// 전투 시스템(MeleeWeaponData/BowData/ArrowData)은 아직 미착수 — 이 프리팹은 순수 비주얼+그랩이며,
    /// 전투 착수 시 별도로 데이터/스크립트를 배선한다 (.claude/domain/gdd/전투_시스템_구현_지시서.md).
    ///
    /// 원본 팩(Free medieval weapons·AdobeMano Medieval Weaponry) 머티리얼은 전부 Built-in 셰이더
    /// (fileID 46)라 이 프로젝트(URP)에서 마젠타로 보인다 — UpgradeMaterialAt으로 기존 .mat 에셋을
    /// URP/Lit 셰이더로 제자리 교체한다(새 에셋 생성 아님, Material.shader만 코드로 갱신 — RULE-02 대상
    /// 아님: .prefab/.asset 텍스트를 손으로 쓰는 게 아니라 Material API + AssetDatabase로만 조작).
    ///
    /// RULE-02: .prefab 직접 작성 금지 — PrefabUtility.SaveAsPrefabAsset만 사용.
    /// </summary>
    public static class WeaponPrefabFactory
    {
        private const string k_sourceFolder = "Assets/FreeMedievalWeapons/Prefabs";
        private const string k_destFolder   = "Assets/Prefabs/Weapons";

        private const string k_spearModelPath      = "Assets/AdobeManoWeapons/Models/Spear_01.fbx";
        private const string k_spearMaterialFolder = "Assets/AdobeManoWeapons/Materials/Spear_01";
        private const string k_spearTextureFolder  = "Assets/AdobeManoWeapons/Textures/Spear_01";
        private static readonly string[] k_spearParts = { "Blade", "Bottom", "Handle", "Steel Rings" };

        // Meta Interaction SDK 타입 (풀네임으로 해석 — 어셈블리 직접 참조 회피)
        private const string k_grabbableType        = "Oculus.Interaction.Grabbable";
        private const string k_grabInteractableType  = "Oculus.Interaction.GrabInteractable";

        [MenuItem("Tools/Make Assets/Weapons (Create All)")]
        public static void CreateAll() => CreateInternal(force: false);

        [MenuItem("Tools/Make Assets/Weapons (Force Recreate)")]
        public static void ForceRecreate() => CreateInternal(force: true);

        private static void CreateInternal(bool force)
        {
            EnsureFolder("Assets/Prefabs");
            EnsureFolder(k_destFolder);

            UpgradeFreeMedievalWeaponsMaterials();

            CreateGrabbableWeapon("Sword", k_sourceFolder + "/Sword_DH.prefab", force);
            CreateGrabbableWeapon("Bow",   k_sourceFolder + "/Wooden Bow.prefab", force);
            CreateGrabbableWeapon("Arrow", k_sourceFolder + "/Arrow.prefab", force);
            CreateSpear(force);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MakeAssets] 무기 프리팹 생성 완료: " + k_destFolder);
        }

        // 지난 세션에 가져온 Sword_DH/Wooden Bow/Arrow 머티리얼(Built-in 셰이더)을 URP/Lit로 제자리 업그레이드.
        // 매번 CreateAll에서 실행 — 이미 URP인 경우도 재적용은 무해(멱등).
        private static void UpgradeFreeMedievalWeaponsMaterials()
        {
            const string matDir = "Assets/FreeMedievalWeapons/Materials";
            const string texDir = "Assets/FreeMedievalWeapons/Textures";

            UpgradeMaterialAt($"{matDir}/Swords_DH_low_Sword_DH_AlbedoTransparency.mat",
                               $"{texDir}/Swords_DH_low_Sword_DH_AlbedoTransparency.png",
                               $"{texDir}/Swords_DH_low_Sword_DH_Normal_001.png");

            UpgradeMaterialAt($"{matDir}/Wooden Bow_1_Wooden Bow_1_AlbedoTransparency.mat",
                               $"{texDir}/Wooden Bow_1_Wooden Bow_1_AlbedoTransparency.png",
                               $"{texDir}/Wooden Bow_1_Wooden Bow_1_Normal.png");

            UpgradeMaterialAt($"{matDir}/Arrows_Arrows_AlbedoTransparency.mat",
                               $"{texDir}/Arrows_Arrows_AlbedoTransparency.png",
                               $"{texDir}/Arrows_Arrows_Normal.png");
        }

        private static void CreateGrabbableWeapon(string name, string sourcePrefabPath, bool force)
        {
            string destPath = $"{k_destFolder}/{name}.prefab";
            if (Skip(destPath, force)) return;

            var sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePrefabPath);
            if (sourcePrefab == null)
            {
                Debug.LogError($"[MakeAssets] 원본 비주얼 프리팹 없음: {sourcePrefabPath}");
                return;
            }

            var root = new GameObject(name);
            try
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(sourcePrefab, root.transform);
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale    = Vector3.one;

                AddGrabPhysics(root);

                PrefabUtility.SaveAsPrefabAsset(root, destPath);
                Debug.Log($"[MakeAssets] {name} 생성: {destPath} (원본: {sourcePrefabPath})");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        // AdobeMano Medieval Weaponry의 Spear_01.fbx(서브메시 4파트: Blade/Bottom/Handle/Steel Rings)로
        // 창 프리팹을 만든다. 원본 프리팹이 없는 팩이라 FBX를 직접 인스턴스화하고, 머티리얼 슬롯은
        // 임포트된 서브머티리얼 이름(파트명)으로 매칭해 URP 업그레이드본을 재배정한다.
        private static void CreateSpear(bool force)
        {
            string destPath = $"{k_destFolder}/Spear.prefab";
            if (Skip(destPath, force)) return;

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(k_spearModelPath);
            if (model == null)
            {
                Debug.LogError($"[MakeAssets] 창 모델 없음: {k_spearModelPath}");
                return;
            }

            foreach (var part in k_spearParts)
                UpgradeMaterialAt($"{k_spearMaterialFolder}/Spear_01 {part}.mat",
                                   $"{k_spearTextureFolder}/Spear_01 {part}_BaseColor.png",
                                   $"{k_spearTextureFolder}/Spear_01 {part}_Normal.png");

            var root = new GameObject("Spear");
            try
            {
                // 주의: Spear_01.fbx는 FBX Z-up/cm 보정용 루트 트랜스폼(scale=100, rotation=-90°X)이
                // Model 프리팹 인스턴스 자체의 로컬 트랜스폼에 실려 있다(메시 정점이 아니라 트랜스폼에 보정
                // 값이 있음) — Sword/Bow/Arrow(완성된 .prefab)와 달리 이 보정을 identity로 덮어쓰면 메시가
                // 1/100 크기로, 축이 뒤바뀐 채 렌더링된다. 그래서 이 비주얼만 로컬 트랜스폼을 건드리지 않는다.
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);

                var renderer = visual.GetComponentInChildren<MeshRenderer>();
                if (renderer == null)
                {
                    Debug.LogError("[MakeAssets] Spear_01 모델에서 MeshRenderer를 찾지 못함");
                    return;
                }

                var slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    string part = MatchSpearPart(slots[i] != null ? slots[i].name : null);
                    if (part == null)
                    {
                        Debug.LogWarning($"[MakeAssets] Spear 머티리얼 슬롯 매칭 실패: {(slots[i] != null ? slots[i].name : "null")}");
                        continue;
                    }

                    var upgraded = AssetDatabase.LoadAssetAtPath<Material>($"{k_spearMaterialFolder}/Spear_01 {part}.mat");
                    if (upgraded != null) slots[i] = upgraded;
                }
                renderer.sharedMaterials = slots;

                AddGrabPhysics(root);

                PrefabUtility.SaveAsPrefabAsset(root, destPath);
                Debug.Log($"[MakeAssets] Spear 생성: {destPath} (원본: {k_spearModelPath})");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static string MatchSpearPart(string materialName)
        {
            if (string.IsNullOrEmpty(materialName)) return null;
            foreach (var part in k_spearParts)
                if (materialName.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0) return part;
            return null;
        }

        // 기존 .mat 에셋을 URP/Lit 셰이더로 제자리 교체(BaseMap/BumpMap만 배선 — Metallic/Roughness는
        // P0 그레이박스 수준에서 생략, 필요 시 후속 작업으로 채움).
        private static void UpgradeMaterialAt(string materialPath, string baseMapPath, string normalMapPath)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (mat == null)
            {
                Debug.LogWarning($"[MakeAssets] 머티리얼 없음: {materialPath}");
                return;
            }

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                Debug.LogWarning("[MakeAssets] Universal Render Pipeline/Lit 셰이더를 못 찾음 — URP 패키지 설치 확인");
                return;
            }

            mat.shader = urpLit;

            var baseMap = AssetDatabase.LoadAssetAtPath<Texture2D>(baseMapPath);
            if (baseMap != null) mat.SetTexture("_BaseMap", baseMap);
            else Debug.LogWarning($"[MakeAssets] BaseColor 텍스처 없음: {baseMapPath}");

            var normalMap = AssetDatabase.LoadAssetAtPath<Texture2D>(normalMapPath);
            if (normalMap != null)
            {
                mat.SetTexture("_BumpMap", normalMap);
                mat.EnableKeyword("_NORMALMAP");
            }

            EditorUtility.SetDirty(mat);
        }

        // Rigidbody + 렌더러 바운즈 기반 BoxCollider + Grabbable + GrabInteractable(물리 그랩).
        private static void AddGrabPhysics(GameObject root)
        {
            var rb = root.AddComponent<Rigidbody>();
            rb.useGravity  = true;
            rb.isKinematic = false;

            // MeshFilter/SkinnedMeshRenderer의 로컬 메시 바운즈를 각 렌더러의 실제 트랜스폼
            // (localToWorldMatrix)으로 변환해 합산한다 — Spear처럼 FBX 축 보정(scale/rotation)이
            // 트랜스폼에 실려 있는 경우까지 정확히 반영하기 위함. root는 원점·identity로 생성되므로
            // world == root-local.
            bool  hasBounds = false;
            var   combined  = new Bounds();
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                if (!TryGetLocalMeshBounds(r, out var localBounds)) continue;
                EncapsulateLocalBounds(ref combined, ref hasBounds, r.transform, localBounds);
            }

            if (hasBounds)
            {
                var box = root.AddComponent<BoxCollider>();
                box.center = combined.center;
                box.size   = combined.size;
            }
            else
            {
                Debug.LogWarning($"[MakeAssets] {root.name}: 메시 바운즈를 찾지 못해 Collider를 생성하지 못함 — GrabInteractable에 최소 1개 Collider 필요.");
            }

            Type grabbableType = FindType(k_grabbableType);
            Type grabInteractableType = FindType(k_grabInteractableType);
            if (grabbableType == null || grabInteractableType == null)
            {
                Debug.LogWarning($"[MakeAssets] {root.name}: Grabbable/GrabInteractable 타입을 못 찾음 — Meta Interaction SDK 설치 확인. 그랩 미배선.");
                return;
            }

            Component grabbable        = root.AddComponent(grabbableType);
            Component grabInteractable = root.AddComponent(grabInteractableType);

            InvokeInject(grabInteractableType, grabInteractable, "InjectRigidbody", rb);
            InvokeInject(grabInteractableType, grabInteractable, "InjectOptionalPointableElement", grabbable);
        }

        private static bool TryGetLocalMeshBounds(Renderer r, out Bounds bounds)
        {
            switch (r)
            {
                case MeshRenderer _ when r.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh != null:
                    bounds = mf.sharedMesh.bounds;
                    return true;
                case SkinnedMeshRenderer smr when smr.sharedMesh != null:
                    bounds = smr.sharedMesh.bounds;
                    return true;
                default:
                    bounds = default;
                    return false;
            }
        }

        // localBounds(트랜스폼 로컬 공간)의 8개 꼭짓점을 transform.localToWorldMatrix로 변환해
        // combined에 합산. Renderer.bounds 캐시에 의존하지 않는 즉시 정확한 바운즈 계산.
        private static void EncapsulateLocalBounds(ref Bounds combined, ref bool hasBounds, Transform transform, Bounds localBounds)
        {
            var matrix = transform.localToWorldMatrix;
            var center = localBounds.center;
            var extents = localBounds.extents;
            for (int xi = -1; xi <= 1; xi += 2)
            for (int yi = -1; yi <= 1; yi += 2)
            for (int zi = -1; zi <= 1; zi += 2)
            {
                var corner = center + Vector3.Scale(extents, new Vector3(xi, yi, zi));
                var world  = matrix.MultiplyPoint3x4(corner);
                if (!hasBounds) { combined = new Bounds(world, Vector3.zero); hasBounds = true; }
                else combined.Encapsulate(world);
            }
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
