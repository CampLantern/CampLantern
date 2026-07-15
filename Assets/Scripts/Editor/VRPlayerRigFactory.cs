#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace CampLantern.EditorTools
{
    /// <summary>
    /// VR 플레이어 리그 프리팹 생성기 (Meta Quest).
    /// 생성물: Assets/Prefabs/VRPlayerRig.prefab
    ///   - 루트: OVRCameraRig + OVRManager (둘 다 글로벌 네임스페이스, Meta XR Core SDK 203.0.0)
    ///   - 앵커(TrackingSpace/눈/손)는 OVRCameraRig가 자동 생성 — EnsureGameObjectIntegrity()로 author 시점에 확정
    ///   - 좌/우 손 앵커 아래에 OVRControllerPrefab 인스턴스(LTouch/RTouch) — 컨트롤러 모델 표시
    ///
    /// RULE-02: .prefab 텍스트 직접 작성 금지 — PrefabUtility.SaveAsPrefabAsset만 사용.
    /// 손 트래킹(OVRHandPrefab)은 OVRHand/OVRSkeleton/OVRMesh의 internal 필드 3종을 맞춰야 해
    /// author 시점 자동 배선이 취약하므로 P0 기본값은 컨트롤러로 둔다 — 손 트래킹이 필요하면 추후 추가.
    ///
    /// 이 리그는 P0Harness의 데스크톱 Main Camera를 대체하는 실제 VR 카메라다. 씬에 배치 후,
    /// P0Harness가 IMGUI 버튼으로 하던 호출(Cast/Reel/Cook/Place)을 컨트롤러 입력으로 옮기는
    /// "VR 입력 어댑터"는 별도 작업 (P0Harness 주석 참조).
    /// </summary>
    public static class VRPlayerRigFactory
    {
        private const string k_folder    = "Assets/Prefabs";
        private const string k_path      = k_folder + "/VRPlayerRig.prefab";

        // UPM 패키지는 폴더 해시와 무관하게 "Packages/{패키지명}/..." 로 접근 가능 (업데이트돼도 경로 불변)
        private const string k_corePrefabs      = "Packages/com.meta.xr.sdk.core/Prefabs";
        private const string k_controllerPrefab = k_corePrefabs + "/OVRControllerPrefab.prefab";

        [MenuItem("Tools/Make Assets/VR Player Rig")]
        public static void Create() => CreateInternal(force: false);

        [MenuItem("Tools/Make Assets/VR Player Rig (Force Recreate)")]
        public static void ForceRecreate() => CreateInternal(force: true);

        private static void CreateInternal(bool force)
        {
            if (!force && AssetDatabase.LoadAssetAtPath<GameObject>(k_path) != null)
            {
                Debug.LogWarning($"[MakeAssets] 이미 존재합니다. Force Recreate 메뉴를 사용하세요: {k_path}");
                return;
            }

            if (!AssetDatabase.IsValidFolder(k_folder))
                AssetDatabase.CreateFolder("Assets", "Prefabs");

            var root = new GameObject("VRPlayerRig");
            try
            {
                var rig = root.AddComponent<OVRCameraRig>();
                root.AddComponent<OVRManager>();

                // 앵커 계층(TrackingSpace/LeftHandAnchor/RightHandAnchor/눈 앵커)을 지금 생성해 둔다.
                // 안 부르면 런타임 첫 프레임에 생겨서 author 시점엔 손 앵커가 없어 컨트롤러 부착 불가.
                rig.EnsureGameObjectIntegrity();

                AttachController(rig.leftHandAnchor,  OVRInput.Controller.LTouch, "Left");
                AttachController(rig.rightHandAnchor, OVRInput.Controller.RTouch, "Right");

                PrefabUtility.SaveAsPrefabAsset(root, k_path);
                Debug.Log($"[MakeAssets] VRPlayerRig 생성 완료: {k_path} — 씬에 배치 후 Quest에서 Play");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// 중복 컨트롤러 비주얼 정리 — OVRComprehensiveInteractionRig(Add Interaction To VR Rig)가 자체
        /// ControllerVisual을 포함하므로, 이 팩토리가 손 앵커에 붙였던 Core SDK 컨트롤러 모델
        /// (OVRControllerPrefab/OVRControllerHelper)과 이중 렌더된다. 정지 시엔 겹쳐 보이지만
        /// 이동/회전 시 두 비주얼의 갱신 타이밍 차이로 "늦게 따라오는" 고스트 컨트롤러가 보인다.
        /// 레이/그랩/버튼 애니메이션과 통합된 Interaction SDK 비주얼을 남기고 Core SDK 모델을 비활성화한다.
        /// idempotent — 이미 꺼져 있으면 변경 없음.
        /// </summary>
        [MenuItem("Tools/Make Assets/Fix Duplicate Controller Visuals (VR Rig)")]
        public static void DisableCoreControllerModels()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(k_path) == null)
            {
                Debug.LogError($"[MakeAssets] VRPlayerRig 없음: {k_path}");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(k_path);
            try
            {
                int disabled = 0;
                foreach (var helper in root.GetComponentsInChildren<OVRControllerHelper>(true))
                {
                    if (!helper.gameObject.activeSelf) continue;
                    helper.gameObject.SetActive(false);
                    disabled++;
                }

                if (disabled > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, k_path);
                    Debug.Log($"[MakeAssets] Core SDK 컨트롤러 모델 {disabled}개 비활성화 — " +
                              "Interaction SDK ControllerVisual만 렌더됨 (고스트 컨트롤러 해소)");
                }
                else
                {
                    Debug.Log("[MakeAssets] 이미 정리됨(변경 없음) — 활성 OVRControllerHelper 모델 없음");
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// 손 앵커 아래에 컨트롤러 프리팹 인스턴스를 붙이고 좌/우(m_controller)를 설정한다.
        /// 패키지 프리팹을 못 찾으면 경고만 남기고 건너뛴다 — 리그 본체는 그대로 저장된다.
        /// 주의: 인터랙션 리그(Add Interaction To VR Rig)를 쓰면 이 모델은 중복이라 꺼야 한다
        /// (Fix Duplicate Controller Visuals 메뉴).
        /// </summary>
        private static void AttachController(Transform handAnchor, OVRInput.Controller side, string label)
        {
            if (handAnchor == null)
            {
                Debug.LogWarning($"[MakeAssets] {label} 손 앵커가 없어 컨트롤러를 건너뜀 (EnsureGameObjectIntegrity 실패?)");
                return;
            }

            var controllerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(k_controllerPrefab);
            if (controllerPrefab == null)
            {
                Debug.LogWarning($"[MakeAssets] 컨트롤러 프리팹 없음: {k_controllerPrefab} — Meta XR Core SDK 설치 확인. {label} 컨트롤러 건너뜀");
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(controllerPrefab);
            instance.name = $"{label}ControllerAnchor";
            instance.transform.SetParent(handAnchor, false);

            // OVRControllerHelper.m_controller — 이 값으로 좌/우 컨트롤러 모델·애니메이터가 갈린다
            var helper = instance.GetComponentInChildren<OVRControllerHelper>(true);
            if (helper != null)
            {
                helper.m_controller = side;
                // 중첩 프리팹 인스턴스의 오버라이드를 새 프리팹에 확실히 굽는다
                PrefabUtility.RecordPrefabInstancePropertyModifications(helper);
            }
            else
            {
                Debug.LogWarning($"[MakeAssets] {label} 컨트롤러 프리팹에 OVRControllerHelper가 없어 좌/우 설정을 건너뜀");
            }
        }
    }
}
#endif
