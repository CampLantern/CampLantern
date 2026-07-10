#if UNITY_EDITOR
using CampLantern.Combat.Data;
using CampLantern.Networking;
using Fusion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CampLantern.EditorTools
{
    /// <summary>
    /// 플레이어 전투 존재 프리팹 + 사냥터 배선 (combat-detailed-network step-03).
    /// CombatPlayer.prefab = NetworkObject + NetworkTransform + NetworkedCombatPlayer (비주얼 없음 —
    /// 몸체 비주얼은 Meta 아바타가 담당, 이건 전투 판정용 존재 마커).
    /// 스포너 배선은 RoomScenesFactory.EnsureVoiceOnNetworkObject 패턴 — 사냥터 Network GO에만.
    /// RULE-02: PrefabUtility/SerializedObject/EditorSceneManager만 사용.
    /// </summary>
    public static class CombatPlayerFactory
    {
        private const string k_prefabPath = "Assets/Prefabs/CombatPlayer.prefab";
        private const string k_balancePath = "Assets/Data/Combat/CombatBalance.asset";
        private const string k_huntScenePath = "Assets/Scenes/HuntZone_A.unity";

        [MenuItem("Tools/Make Assets/Combat Player (Networked) — Create + Wire Hunt Zone")]
        public static void CreateAndWire()
        {
            CreatePrefab();
            WireIntoHuntZone();
        }

        private static void CreatePrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(k_prefabPath) != null)
            {
                Debug.Log($"[MakeAssets] CombatPlayer.prefab 이미 존재 — 스킵");
                return;
            }

            var balance = AssetDatabase.LoadAssetAtPath<CombatBalanceData>(k_balancePath);
            var root = new GameObject("CombatPlayer");
            try
            {
                root.AddComponent<NetworkObject>(); // 플레이어 소유 (기본 플래그 — 소유자 이탈 시 파괴)
                root.AddComponent<NetworkTransform>();
                var player = root.AddComponent<NetworkedCombatPlayer>();

                var so = new SerializedObject(player);
                var prop = so.FindProperty("m_balance");
                if (prop != null && balance != null)
                {
                    prop.objectReferenceValue = balance;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SaveAsPrefabAsset(root, k_prefabPath);
                Debug.Log($"[MakeAssets] CombatPlayer created: {k_prefabPath}");
            }
            finally { Object.DestroyImmediate(root); }
            AssetDatabase.SaveAssets();
        }

        private static void WireIntoHuntZone()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(k_huntScenePath) == null)
            {
                Debug.LogError($"[MakeAssets] 씬 없음: {k_huntScenePath}");
                return;
            }

            EditorSceneManager.SaveOpenScenes();
            Scene scene = EditorSceneManager.OpenScene(k_huntScenePath, OpenSceneMode.Single);

            SessionLauncher launcher = null;
            foreach (var sceneRoot in scene.GetRootGameObjects())
            {
                launcher = sceneRoot.GetComponentInChildren<SessionLauncher>(true);
                if (launcher != null) break;
            }
            if (launcher == null)
            {
                Debug.LogError("[MakeAssets] HuntZone_A에 SessionLauncher 없음");
                return;
            }

            var spawner = launcher.GetComponent<CombatPlayerSpawner>();
            if (spawner == null) spawner = launcher.gameObject.AddComponent<CombatPlayerSpawner>();

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(k_prefabPath);
            var so = new SerializedObject(spawner);
            var launcherProp = so.FindProperty("m_launcher");
            var prefabProp = so.FindProperty("m_playerPrefab");
            if (launcherProp != null) launcherProp.objectReferenceValue = launcher;
            if (prefabProp != null && prefab != null) prefabProp.objectReferenceValue = prefab.GetComponent<NetworkObject>();
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene);
            Debug.Log("[MakeAssets] CombatPlayerSpawner wired into HuntZone_A");
        }
    }
}
#endif
