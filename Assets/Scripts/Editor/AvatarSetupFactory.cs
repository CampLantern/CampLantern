#if UNITY_EDITOR
using CampLantern.Networking.Avatar;
using Fusion;
using UnityEditor;
using UnityEngine;

namespace CampLantern.EditorTools
{
    /// <summary>
    /// 네트워크 아바타(남들이 보는 몸체) 배선 헬퍼.
    ///
    /// 로컬 영속 아바타(OVRCameraRig + OvrAvatarManager + SampleInputManager)는 PersistentPlayer.prefab이
    /// DontDestroyOnLoad로 어느 씬에서든 자동 스폰·유지한다 — 씬에 리그/아바타 매니저를 다시 넣으면
    /// 중복(OVRManager/OvrAvatarManager 이중 인스턴스) 충돌로 오히려 아바타가 깨진다. 그래서 여기서 배선하는
    /// 것은 세션마다 몸체를 스폰하는 <see cref="AvatarController"/> 하나뿐이다.
    ///
    /// AvatarController는 Awake에서 GetComponent&lt;SessionLauncher&gt;로 세션을 찾으므로 반드시 Network
    /// 오브젝트(SessionLauncher와 같은 GameObject)에 붙어야 한다. 스폰 프리팹은 Meta Core SDK Building
    /// Blocks의 FusionAvatarSdk28Plus.prefab(패키지) — 패키지 캐시 경로엔 해시가 붙어 불안정하므로 GUID로 로드한다.
    ///
    /// 씬 생성/패치 진입점은 <see cref="RoomScenesFactory"/>(네트워크 소셜 스택 통합 메뉴)가 담당하고,
    /// 여기는 그 공통 호출 대상인 순수 배선 헬퍼만 노출한다. RULE-02: SerializedObject로만 배선.
    /// </summary>
    public static class AvatarSetupFactory
    {
        // FusionAvatarSdk28Plus.prefab (com.meta.xr.sdk.core Building Blocks). P0Playground 배선과 동일 GUID.
        private const string k_avatarPrefabGuid = "d32bfe77913d75d4f919b5c9ae778fa7";

        /// <summary>
        /// SessionLauncher가 있는 Network GameObject에 AvatarController를 붙이고 아바타 프리팹을 배선한다.
        /// 이미 있으면 프리팹 참조만 보정한다. 씬/프리팹 팩토리에서 공통 호출. 실제 변경이 있으면 true.
        /// </summary>
        public static bool EnsureOnNetworkObject(GameObject networkGo)
        {
            NetworkObject prefab = LoadAvatarPrefab();

            bool changed = false;
            var controller = networkGo.GetComponent<AvatarController>();
            if (controller == null)
            {
                controller = networkGo.AddComponent<AvatarController>();
                changed = true;
            }

            var so = new SerializedObject(controller);
            var prop = so.FindProperty("m_avatarPrefab");
            if (prop == null)
                throw new System.InvalidOperationException("[MakeAssets] AvatarController.m_avatarPrefab 필드 없음");

            if (prop.objectReferenceValue != prefab)
            {
                prop.objectReferenceValue = prefab; // NetworkObject 컴포넌트를 그대로 참조 (P0Playground와 동일)
                so.ApplyModifiedPropertiesWithoutUndo();
                changed = true;
            }
            return changed;
        }

        /// <summary>FusionAvatarSdk28Plus.prefab의 NetworkObject를 GUID로 로드.</summary>
        public static NetworkObject LoadAvatarPrefab()
        {
            string path = AssetDatabase.GUIDToAssetPath(k_avatarPrefabGuid);
            if (string.IsNullOrEmpty(path))
                throw new System.InvalidOperationException(
                    $"[MakeAssets] FusionAvatarSdk28Plus.prefab을 찾을 수 없음 (GUID {k_avatarPrefabGuid}) — " +
                    "Meta XR Core SDK 설치 확인");

            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var netObj = go != null ? go.GetComponent<NetworkObject>() : null;
            if (netObj == null)
                throw new System.InvalidOperationException(
                    $"[MakeAssets] {path}에 NetworkObject 없음 — FusionAvatarSdk28Plus가 맞는지 확인");
            return netObj;
        }
    }
}
#endif
