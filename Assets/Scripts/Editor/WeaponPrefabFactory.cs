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
    /// RULE-02: .prefab 직접 작성 금지 — PrefabUtility.SaveAsPrefabAsset만 사용.
    /// </summary>
    public static class WeaponPrefabFactory
    {
        private const string k_sourceFolder = "Assets/FreeMedievalWeapons/Prefabs";
        private const string k_destFolder   = "Assets/Prefabs/Weapons";

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

            CreateGrabbableWeapon("Sword", k_sourceFolder + "/Sword_DH.prefab", force);
            CreateGrabbableWeapon("Bow",   k_sourceFolder + "/Wooden Bow.prefab", force);
            CreateGrabbableWeapon("Arrow", k_sourceFolder + "/Arrow.prefab", force);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MakeAssets] 무기 프리팹 생성 완료: " + k_destFolder);
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

        // Rigidbody + 렌더러 바운즈 기반 BoxCollider + Grabbable + GrabInteractable(물리 그랩).
        private static void AddGrabPhysics(GameObject root)
        {
            var rb = root.AddComponent<Rigidbody>();
            rb.useGravity  = true;
            rb.isKinematic = false;

            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

                // root는 원점·identity로 생성되므로 월드 바운즈 == 로컬 바운즈
                var box = root.AddComponent<BoxCollider>();
                box.center = bounds.center;
                box.size   = bounds.size;
            }
            else
            {
                Debug.LogWarning($"[MakeAssets] {root.name}: Renderer를 찾지 못해 Collider를 생성하지 못함 — GrabInteractable에 최소 1개 Collider 필요.");
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
