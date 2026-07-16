#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CampLantern.EditorTools
{
    /// <summary>
    /// 환경 에셋 팩 프루닝 — 팩을 통임포트한 뒤 실제로 쓰는 것만 남기고 삭제한다.
    /// 유지 기준: ① 4개 공간 씬의 의존성 그래프(재귀) ② 맵 팩토리가 이름으로 참조하는
    /// 프리팹(+의존성 — 스캐터 랜덤이라 씬에 안 깔렸어도 유지) ③ 스크립트/asmdef.
    /// 삭제 대상 루트 밖(CFXR Assets의 셰이더·스크립트 등)은 건드리지 않는다.
    /// 원본은 D:\UnityProjects\Assets 프로젝트에 있으므로 언제든 재복사 가능.
    /// </summary>
    public static class EnvAssetPruner
    {
        private static readonly string[] k_pruneRoots =
        {
            "Assets/FantasyEnvironments",
            "Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs",
            "Assets/JMO Assets/Cartoon FX Remaster/CFXR Assets/Graphics",
        };

        private static readonly string[] k_scenePaths =
        {
            "Assets/Scenes/Lobby.unity",
            "Assets/Scenes/FishingGround.unity",
            "Assets/Scenes/HuntZone_A.unity",
            "Assets/Scenes/EstateTemplate.unity",
        };

        // 맵 팩토리(MapBuildUtil/LobbyMapFactory/RoomMapsFactory)가 이름으로 참조하는 프리팹.
        // 세트를 바꾸면 여기도 갱신할 것 — 프루닝 후 재빌드가 조용히 빈자리를 만들지 않게.
        private static readonly string[] k_codeReferencedPrefabs =
        {
            // 나무 (k_treeSet)
            "Pine_tree1", "Oak_tree1", "Pine_tree2", "Birch_tree1", "Deciduous_tree2", "Pine_tree3", "Oak_tree3", "Birch_tree3",
            // 바위 (k_rockSet)
            "Rock1", "Rock2", "Rock3", "Stone1", "Stone2", "Stone3",
            // 수풀 (k_bushSet)
            "Bush1", "Fern1", "Plant2", "Fern3",
            // 지피식물 (k_coverSet)
            "Grass1", "Grass2", "Grass3", "Grass4", "Fern2", "Flower1", "Flower3", "Flower5", "Flower8", "Plant1", "Mushroom1", "Mushroom3",
            // 소품 (팩토리 PlaceEnv 직접 호출)
            "storage_barrel", "storage_basket", "cart1", "storage_bag", "Windmill", "Sunflower",
            "storage_barrel_fish", "storage_basket_small", "storage_jug", "Pine_stump", "Deciduous_tree_stump",
            // CFXR 파티클 (PlaceCfxr)
            "CFXR Fire", "CFXR3 LightGlow A (Loop)",
        };

        [MenuItem("Tools/Make Assets/Prune Unused Env Assets")]
        public static void Prune()
        {
            var keep = new HashSet<string>();

            // ① 씬 의존성 (재귀 — 프리팹→머티리얼→텍스처→FBX 전부)
            foreach (string scene in k_scenePaths)
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scene) != null)
                    keep.UnionWith(AssetDatabase.GetDependencies(scene, true));

            // ② 코드 참조 프리팹 + 의존성
            var nameSet = new HashSet<string>(k_codeReferencedPrefabs);
            foreach (string root in k_pruneRoots)
            {
                if (!AssetDatabase.IsValidFolder(root)) continue;
                foreach (string guid in AssetDatabase.FindAssets("t:prefab", new[] { root }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!nameSet.Contains(Path.GetFileNameWithoutExtension(path))) continue;
                    keep.Add(path);
                    keep.UnionWith(AssetDatabase.GetDependencies(path, true));
                }
            }

            // ③ 삭제 후보 수집 (스크립트·asmdef은 무조건 유지)
            var doomed = new List<string>();
            foreach (string root in k_pruneRoots)
            {
                if (!AssetDatabase.IsValidFolder(root)) continue;
                foreach (string guid in AssetDatabase.FindAssets("t:Object", new[] { root }).Distinct())
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (AssetDatabase.IsValidFolder(path)) continue;
                    if (keep.Contains(path)) continue;
                    if (path.EndsWith(".cs") || path.EndsWith(".asmdef") || path.EndsWith(".shader")) continue;
                    doomed.Add(path);
                }
            }
            doomed = doomed.Distinct().ToList();

            long bytes = doomed.Sum(p => { var f = new FileInfo(p); return f.Exists ? f.Length : 0L; });
            var failed = new List<string>();
            AssetDatabase.DeleteAssets(doomed.ToArray(), failed);

            DeleteEmptyFolders();
            AssetDatabase.Refresh();
            Debug.Log($"[Prune] 미사용 에셋 {doomed.Count - failed.Count}개 삭제 ({bytes / (1024f * 1024f):F1} MB), 실패 {failed.Count}건" +
                      (failed.Count > 0 ? "\n" + string.Join("\n", failed) : ""));
        }

        /// <summary>배치모드용: 프루닝 → 맵 재빌드 → 감사 (프루닝이 씬을 깨지 않았는지 검증).</summary>
        public static void PruneAndVerify()
        {
            Prune();
            MapAuditRunner.BuildAndAudit();
        }

        private static void DeleteEmptyFolders()
        {
            foreach (string root in k_pruneRoots)
            {
                if (!AssetDatabase.IsValidFolder(root)) continue;
                var folders = Directory.GetDirectories(root, "*", SearchOption.AllDirectories)
                                       .OrderByDescending(d => d.Count(c => c == '/' || c == '\\'));
                foreach (string dir in folders)
                {
                    string assetPath = dir.Replace('\\', '/');
                    if (AssetDatabase.FindAssets("t:Object", new[] { assetPath }).Length == 0)
                        AssetDatabase.DeleteAsset(assetPath);
                }
                if (AssetDatabase.FindAssets("t:Object", new[] { root }).Length == 0)
                    AssetDatabase.DeleteAsset(root);
            }
        }
    }
}
#endif
