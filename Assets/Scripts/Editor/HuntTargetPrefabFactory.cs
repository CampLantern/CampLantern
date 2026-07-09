#if UNITY_EDITOR
using System.Linq;
using CampLantern.Core;
using CampLantern.Hunting;
using Fusion;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CampLantern.EditorTools
{
    /// <summary>
    /// 사냥감(HuntTarget) 네트워크 프리팹 생성기.
    ///
    /// HuntTarget은 <see cref="HuntTarget.Def"/>를 프리팹에 직접 물고 있어(스포너가 프리팹 단위 스폰) 동물 종마다
    /// 별도 프리팹이 필요하다. 기존 큰뿔사슴(Assets/Prefabs/HuntTarget.prefab)은 P0PlaySceneFactory가 만들며,
    /// 이 팩토리는 추가 종을 만든다. 비주얼은 플레이스홀더(프리미티브+단색) — 실제 모델/텍스처는 아트 확보 후 교체.
    ///
    /// 곰(Bear)은 Blink Stylized Animals 팩(Assets/BlinkAnimals/Bear/)의 실제 리깅 아트로 교체 가능
    /// (<see cref="UpgradeBearArt"/> — Hunt Target — Bear (Real Art)). Animator+BearAnimator.controller가
    /// 그대로 딸려오지만 HuntTarget.cs는 아직 Animator를 참조하지 않음 — 애니메이션은 컨트롤러 기본 상태
    /// (Idle)로만 재생되고, 피격/사망 등 전투 트리거 배선은 전투 시스템 착수 시 별도 작업.
    ///
    /// 선행: Tools > Make Assets > P0 Data (Create All)로 HuntTargetDef가 있어야 함.
    /// 후행: 스폰하려면 Fusion 프리팹 테이블에 있어야 함(임포트로 자동 베이킹, 무음 실패 시 Fusion > Rebuild Prefab Table).
    /// RULE-02: .prefab 직접 작성 금지 — PrefabUtility.SaveAsPrefabAsset만 사용.
    /// </summary>
    public static class HuntTargetPrefabFactory
    {
        private const string k_folder         = "Assets/Prefabs";
        private const string k_materialFolder = "Assets/Prefabs/Materials";

        [MenuItem("Tools/Make Assets/Hunt Target — Wild Boar")]
        public static void CreateWildBoar()
        {
            // 사슴(캡슐, 밝은 갈색)과 실루엣·색으로 구분 — 큐브 + 짙은 갈색.
            CreateHuntTarget(
                defPath:    "Assets/Data/Hunt/Hunt_WildBoar.asset",
                prefabPath: k_folder + "/HuntTarget_WildBoar.prefab",
                shape:      PrimitiveType.Cube,
                color:      new Color(0.35f, 0.25f, 0.2f),
                matName:    "Mat_HuntTarget_WildBoar",
                visualLocalPos: new Vector3(0f, 0.5f, 0f)); // 큐브(높이1)가 바닥에 놓이도록
        }

        [MenuItem("Tools/Make Assets/Hunt Target — Bear")]
        public static void CreateBear()
        {
            // 초기 플레이스홀더(단일). 실제 형태는 PlaceholderArtFactory.UpgradeAnimals가 다중 프리미티브로 교체.
            CreateHuntTarget(
                defPath:    "Assets/Data/Hunt/Hunt_Bear.asset",
                prefabPath: k_folder + "/HuntTarget_Bear.prefab",
                shape:      PrimitiveType.Cube,
                color:      new Color(0.29f, 0.21f, 0.15f),
                matName:    "Mat_HuntTarget_Bear",
                visualLocalPos: new Vector3(0f, 0.6f, 0f));
        }

        // Blink "Stylized Animals" 곰(Bear_4) 실제 아트로 HuntTarget_Bear의 Visual 교체.
        // PlaceholderArtFactory.Rebuild와 동일한 "아트 스왑" 방식 — 루트(NetworkObject/HuntTarget/
        // HuntLedger)와 프리팹 GUID·네트워크 참조는 보존하고 "Visual" 자식만 통째로 갈아 끼운다.
        // 선행: Hunt Target — Bear 로 HuntTarget_Bear.prefab이 이미 있어야 함.
        private const string k_bearArtPrefabPath = "Assets/BlinkAnimals/Bear/Prefabs/Bear_4.prefab";
        private const string k_bearMaterialPath  = "Assets/BlinkAnimals/Bear/Materials/Bear_4.mat";
        private const string k_bearTextureFolder = "Assets/BlinkAnimals/Bear/Textures";

        [MenuItem("Tools/Make Assets/Hunt Target — Bear (Real Art)")]
        public static void UpgradeBearArt()
        {
            string prefabPath = k_folder + "/HuntTarget_Bear.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            {
                Debug.LogError($"[MakeAssets] {prefabPath} 없음 — 먼저 Tools > Make Assets > Hunt Target — Bear 실행");
                return;
            }

            var sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(k_bearArtPrefabPath);
            if (sourcePrefab == null)
            {
                Debug.LogError($"[MakeAssets] Bear_4.prefab 원본 없음: {k_bearArtPrefabPath}");
                return;
            }

            // Bear_4.mat도 다른 임포트 팩과 동일하게 Built-in 셰이더(fileID 46) — URP/Lit로 제자리 업그레이드.
            UpgradeMaterialToUrp(k_bearMaterialPath,
                baseMapPath:      $"{k_bearTextureFolder}/Stylized_Bear_Albedo4.png",
                normalMapPath:    $"{k_bearTextureFolder}/Stylized_Bear_Normal.png",
                emissionMapPath:  $"{k_bearTextureFolder}/Stylized_Bear_Emissive.png",
                occlusionMapPath: $"{k_bearTextureFolder}/Stylized_Bear_AO.png");

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                foreach (Transform child in root.transform.Cast<Transform>().ToList())
                    if (child.name == "Visual") Object.DestroyImmediate(child.gameObject);

                var visual = (GameObject)PrefabUtility.InstantiatePrefab(sourcePrefab, root.transform);
                visual.name = "Visual";
                // Bear_4.prefab 루트는 데모 씬에 배치됐던 위치/회전이 그대로 남아있음(스케일은 1 — 안전).
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale    = Vector3.one;

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[MakeAssets] {prefabPath} 비주얼을 실제 아트(Bear_4, Animator 포함)로 교체 완료");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // 기존 .mat 에셋을 URP/Lit 셰이더로 제자리 교체 — WeaponPrefabFactory.UpgradeMaterialAt과 동일 취지,
        // 여기선 Emission/Occlusion 맵까지 배선(Bear_4.mat이 사용).
        private static void UpgradeMaterialToUrp(string materialPath, string baseMapPath, string normalMapPath,
                                                  string emissionMapPath = null, string occlusionMapPath = null)
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

            var normalMap = AssetDatabase.LoadAssetAtPath<Texture2D>(normalMapPath);
            if (normalMap != null)
            {
                mat.SetTexture("_BumpMap", normalMap);
                mat.EnableKeyword("_NORMALMAP");
            }

            if (!string.IsNullOrEmpty(emissionMapPath))
            {
                var emissionMap = AssetDatabase.LoadAssetAtPath<Texture2D>(emissionMapPath);
                if (emissionMap != null)
                {
                    mat.SetTexture("_EmissionMap", emissionMap);
                    mat.SetColor("_EmissionColor", Color.white);
                    mat.EnableKeyword("_EMISSION");
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
            }

            if (!string.IsNullOrEmpty(occlusionMapPath))
            {
                var occlusionMap = AssetDatabase.LoadAssetAtPath<Texture2D>(occlusionMapPath);
                if (occlusionMap != null) mat.SetTexture("_OcclusionMap", occlusionMap);
            }

            EditorUtility.SetDirty(mat);
        }

        /// <summary>defPath의 HuntTargetDef를 물린 사냥감 프리팹을 만든다. 종 추가 시 이 메서드 재사용.</summary>
        public static void CreateHuntTarget(string defPath, string prefabPath, PrimitiveType shape,
                                            Color color, string matName, Vector3 visualLocalPos)
        {
            var def = AssetDatabase.LoadAssetAtPath<HuntTargetDef>(defPath);
            if (def == null)
            {
                Debug.LogError($"[MakeAssets] HuntTargetDef 없음: {defPath} — 먼저 Tools > Make Assets > P0 Data (Create All) 실행");
                return;
            }

            EnsureFolder(k_folder);
            EnsureFolder(k_materialFolder);

            var root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(prefabPath));
            try
            {
                // 마스터 클라이언트 소유 + 마스터 이탈 시 파괴 방지(권한 이전) — 큰뿔사슴 프리팹과 동일 규칙.
                var networkObject = root.AddComponent<NetworkObject>();
                networkObject.Flags = (networkObject.Flags | NetworkObjectFlags.MasterClientObject)
                                      & ~NetworkObjectFlags.DestroyWhenStateAuthorityLeaves;

                var target = root.AddComponent<HuntTarget>();
                target.Def = def;
                root.AddComponent<HuntLedger>();

                AddVisual(root, shape, color, matName, visualLocalPos);

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log($"[MakeAssets] 사냥감 프리팹 생성: {prefabPath} (Def={def.DisplayName}, HP {def.MaxHealth}, 필요 인원 {def.RequiredParticipants})");
            }
            finally { Object.DestroyImmediate(root); }
            AssetDatabase.Refresh();
        }

        // Visual 자식: localScale은 one 유지(크기는 프리미티브 기본), 위치만 조정. Collider는 프리미티브 자동 추가분 제거.
        private static void AddVisual(GameObject parent, PrimitiveType shape, Color color, string matName, Vector3 localPos)
        {
            var visual = GameObject.CreatePrimitive(shape);
            visual.name = "Visual";
            visual.transform.SetParent(parent.transform, false);
            visual.transform.localPosition = localPos;
            Object.DestroyImmediate(visual.GetComponent<Collider>());

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
            visual.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parts = path.Split('/');
            var cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }
    }
}
#endif
