#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CampLantern.EditorTools
{
    /// <summary>
    /// 인터랙션 리그(OVRComprehensiveInteractionRig)의 로코모션/터널링 컴포넌트 참조 복구.
    ///
    /// 문제: 리그를 추가하면(VRUIFactory.AddInteractionToVrRig) OVRCameraRigRef만 배선되고,
    ///   - FirstPersonLocomotor._playerOrigin (이동시킬 플레이어 루트 Transform)
    ///   - TunnelingEffect._centerEyeCamera (컴포트 비네트 기준 카메라)
    /// 가 미할당으로 남는다. 플레이 시 AssertField 경고가 뜨고, 실제 이동/터널링 사용 시 NullRef가 난다.
    ///
    /// 이 두 필드를 OVRCameraRig 계층에서 찾아 채운다. 대상은 런타임에 실제 스폰되는
    /// PersistentPlayer.prefab (씬엔 in-scene 리그가 없으므로 이게 유일한 런타임 리그다).
    /// FirstPersonLocomotor/TunnelingEffect는 Oculus.Interaction 어셈블리라 타입 직접 참조 대신
    /// 컴포넌트 단순 이름으로 찾는다(VRUIFactory.FindComponentBySimpleName과 동일 발상).
    ///
    /// RULE-02: .prefab 직접 편집 금지 — PrefabUtility.LoadPrefabContents + SerializedObject로만.
    /// </summary>
    public static class InteractionRigRepair
    {
        private const string k_persistentPlayerPath = "Assets/Resources/PersistentPlayer.prefab";

        [MenuItem("Tools/Make Assets/Fix Interaction Rig References")]
        public static void FixPersistentPlayer()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(k_persistentPlayerPath) == null)
            {
                Debug.LogError($"[MakeAssets] 프리팹 없음: {k_persistentPlayerPath}");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(k_persistentPlayerPath);
            try
            {
                var camRig = root.GetComponentInChildren<OVRCameraRig>(true);
                if (camRig == null)
                {
                    Debug.LogError($"[MakeAssets] {k_persistentPlayerPath}에 OVRCameraRig 없음 — 리그 구조 확인");
                    return;
                }

                int fixedCount = WireRig(root, camRig);
                if (fixedCount > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, k_persistentPlayerPath);
                    Debug.Log($"[MakeAssets] 인터랙션 리그 참조 복구: {k_persistentPlayerPath} — {fixedCount}개 필드 배선");
                }
                else
                {
                    Debug.Log($"[MakeAssets] 복구할 미할당 참조 없음(이미 배선됨): {k_persistentPlayerPath}");
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// searchRoot 하위의 FirstPersonLocomotor._playerOrigin / TunnelingEffect._centerEyeCamera 중
        /// 미할당인 것을 camRig 기준값으로 채운다. 리그 생성기(VRUIFactory)와 패치 메뉴가 공통 호출. 배선한 필드 수 반환.
        /// </summary>
        public static int WireRig(GameObject searchRoot, OVRCameraRig camRig)
        {
            Transform playerOrigin = camRig.transform;         // 이동 대상 = 카메라 리그 루트
            Transform playerEyes   = camRig.centerEyeAnchor;    // 플레이어 눈(머리) = 중앙눈 앵커
            Camera centerEye = camRig.centerEyeAnchor != null
                ? camRig.centerEyeAnchor.GetComponent<Camera>()
                : null;

            int fixedCount = 0;

            // FirstPersonLocomotor: _playerOrigin/_playerEyes는 짝 — 하나만 넣으면 나머지도 필수(AssertField).
            foreach (Component comp in FindAllBySimpleName(searchRoot, "FirstPersonLocomotor"))
            {
                if (SetRefIfEmpty(comp, "_playerOrigin", playerOrigin)) fixedCount++;
                if (SetRefIfEmpty(comp, "_playerEyes",   playerEyes))   fixedCount++;
            }

            if (centerEye != null)
            {
                foreach (Component comp in FindAllBySimpleName(searchRoot, "TunnelingEffect"))
                    if (SetRefIfEmpty(comp, "_centerEyeCamera", centerEye)) fixedCount++;
            }
            else
            {
                Debug.LogWarning("[MakeAssets] OVRCameraRig.centerEyeAnchor 카메라를 못 찾음 — TunnelingEffect 건너뜀");
            }

            return fixedCount;
        }

        // 이미 값이 있으면 건드리지 않음(idempotent). 미할당일 때만 채우고 nested 오버라이드로 기록. 배선 시 true.
        private static bool SetRefIfEmpty(Component comp, string field, Object value)
        {
            if (value == null) return false;

            var so = new SerializedObject(comp);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogWarning($"[MakeAssets] {comp.GetType().Name}.{field} 필드 없음 — SDK 버전 확인");
                return false;
            }
            if (prop.objectReferenceValue != null) return false; // 이미 배선됨

            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(comp); // nested prefab 인스턴스 오버라이드로 저장
            return true;
        }

        private static List<Component> FindAllBySimpleName(GameObject root, string simpleName)
        {
            var result = new List<Component>();
            foreach (Component c in root.GetComponentsInChildren<Component>(true))
                if (c != null && c.GetType().Name == simpleName)
                    result.Add(c);
            return result;
        }
    }
}
#endif
