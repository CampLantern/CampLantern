#if UNITY_EDITOR
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
