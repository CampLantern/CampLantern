#if UNITY_EDITOR
using CampLantern.Player;
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

        // ── 컨트롤러 → 장갑(손) 비주얼 전환 ──────────────────────────
        // OVRManager.controllerDrivenHandPosesType(컨트롤러 입력으로 손 포즈 합성) + 인터랙션 리그에
        // OVRHands(손 데이터→Hand) + HandVisual(손 메시) 배선. 컨트롤러 모델(ControllerVisual)은 끈다.
        // 전제: OVRProjectConfig.handTrackingSupport = ControllersAndHands (여기서 함께 설정).

        private const string k_ovrHandsPrefab   = "Packages/com.meta.xr.sdk.interaction.ovr/Runtime/Prefabs/OVRHands.prefab";
        private const string k_handVisualLeft   = "Packages/com.meta.xr.sdk.interaction/Runtime/Prefabs/Hands/HandVisualLeft.prefab";
        private const string k_handVisualRight  = "Packages/com.meta.xr.sdk.interaction/Runtime/Prefabs/Hands/HandVisualRight.prefab";

        [MenuItem("Tools/Make Assets/Enable Controller-Driven Hands (VR Rig)")]
        public static void EnableControllerDrivenHands()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(k_path) == null)
            {
                Debug.LogError($"[MakeAssets] VRPlayerRig 없음: {k_path}");
                return;
            }

            // 1) 프로젝트 설정 — 핸드 트래킹 지원 (컨트롤러 기반 손 포즈의 전제)
            var config = OVRProjectConfig.CachedProjectConfig;
            if (config.handTrackingSupport != OVRProjectConfig.HandTrackingSupport.ControllersAndHands)
            {
                config.handTrackingSupport = OVRProjectConfig.HandTrackingSupport.ControllersAndHands;
                OVRProjectConfig.CommitProjectConfig(config);
                Debug.Log("[MakeAssets] OVRProjectConfig.handTrackingSupport = ControllersAndHands");
            }

            GameObject root = PrefabUtility.LoadPrefabContents(k_path);
            try
            {
                // 2) OVRManager — 컨트롤러 입력으로 손 포즈 합성 (Natural: 빈손 자연 포즈)
                var manager = root.GetComponentInChildren<OVRManager>(true);
                if (manager != null)
                    manager.controllerDrivenHandPosesType = OVRManager.ControllerDrivenHandPosesType.Natural;

                // 3) 인터랙션 리그에 OVRHands + HandVisual 배선 (idempotent — OVRHands가 이미 있으면 생략)
                Component rigRef = FindComponentBySimpleName(root, "OVRCameraRigRef");
                if (rigRef == null)
                {
                    Debug.LogError("[MakeAssets] 인터랙션 리그(OVRCameraRigRef) 없음 — 먼저 Add Interaction To VR Rig 실행");
                    return;
                }

                if (FindChildByName(root.transform, "OVRHands") == null)
                {
                    var handsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(k_ovrHandsPrefab);
                    var leftPrefab  = AssetDatabase.LoadAssetAtPath<GameObject>(k_handVisualLeft);
                    var rightPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(k_handVisualRight);
                    if (handsPrefab == null || leftPrefab == null || rightPrefab == null)
                    {
                        Debug.LogError("[MakeAssets] OVRHands/HandVisual 패키지 프리팹을 못 찾음 — Interaction SDK 확인");
                        return;
                    }

                    var hands = (GameObject)PrefabUtility.InstantiatePrefab(handsPrefab, rigRef.transform);

                    // FromOVRHandDataSource._cameraRigRef 주입 + 좌/우 Hand 식별(_handedness 0=왼 1=오른)
                    Component leftHand = null, rightHand = null;
                    foreach (var comp in hands.GetComponentsInChildren<Component>(true))
                    {
                        if (comp == null || comp.GetType().Name != "FromOVRHandDataSource") continue;
                        var so = new SerializedObject(comp);
                        var refProp = so.FindProperty("_cameraRigRef");
                        if (refProp != null)
                        {
                            refProp.objectReferenceValue = rigRef;
                            so.ApplyModifiedPropertiesWithoutUndo();
                            PrefabUtility.RecordPrefabInstancePropertyModifications(comp);
                        }
                        int handedness = so.FindProperty("_handedness") != null ? so.FindProperty("_handedness").enumValueIndex : 0;
                        Component hand = FindComponentBySimpleNameOn(comp.gameObject, "Hand");
                        if (handedness == 0) leftHand = hand; else rightHand = hand;
                    }

                    AttachHandVisual(leftPrefab,  leftHand,  "왼손");
                    AttachHandVisual(rightPrefab, rightHand, "오른손");
                }

                // 4) 컨트롤러 모델 비활성 — 장갑만 보이게
                int hidden = 0;
                foreach (var tr in root.GetComponentsInChildren<Transform>(true))
                {
                    if (!tr.name.Contains("ControllerVisual") || !tr.gameObject.activeSelf) continue;
                    tr.gameObject.SetActive(false);
                    hidden++;
                }

                // 5) 플레이스홀더 장갑 (폴백) — 시뮬레이터/Link는 손 데이터가 안 나와(실측) 진짜 손이
                //    안 뜬다. 컨트롤러 앵커에 프리미티브 장갑을 붙이고, 손 데이터가 돌면 자동으로 숨긴다
                //    (GloveFallbackVisual). 실기에서는 Meta HandVisual이 대신 렌더된다.
                var camRig = root.GetComponentInChildren<OVRCameraRig>(true);
                if (camRig != null)
                {
                    camRig.EnsureGameObjectIntegrity();
                    BuildFallbackGlove(camRig.leftHandAnchor,  mirrorX: true);
                    BuildFallbackGlove(camRig.rightHandAnchor, mirrorX: false);
                }

                PrefabUtility.SaveAsPrefabAsset(root, k_path);
                Debug.Log($"[MakeAssets] 컨트롤러 기반 손(장갑) 활성화 완료 — 컨트롤러 비주얼 {hidden}개 비활성. " +
                          "되돌리기: Tools > Make Assets > Restore Controller Visuals (VR Rig)");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// 플레이스홀더 장갑 — 컨트롤러 앵커 자식으로 프리미티브 가죽 장갑을 조립한다(idempotent).
        /// GloveFallbackVisual이 손 데이터 유무에 따라 표시를 제어한다. 움직이는 앵커 자식이므로
        /// 정적 배칭 플래그는 걸지 않는다 (MapBuildUtil.Part 미사용 이유).
        /// </summary>
        private static void BuildFallbackGlove(Transform handAnchor, bool mirrorX)
        {
            if (handAnchor == null) return;
            foreach (Transform child in handAnchor)
                if (child.name == "GloveVisual") return; // 이미 있음

            var gloveRoot = new GameObject("GloveVisual");
            gloveRoot.transform.SetParent(handAnchor, false);
            gloveRoot.AddComponent<GloveFallbackVisual>();
            float mx = mirrorX ? -1f : 1f;

            GlovePart(gloveRoot.transform, PrimitiveType.Sphere, new Vector3(0f, -0.01f, 0.02f),   new Vector3(0.075f, 0.035f, 0.1f), Vector3.zero);  // 손바닥
            GlovePart(gloveRoot.transform, PrimitiveType.Sphere, new Vector3(0f, -0.005f, -0.03f), new Vector3(0.06f, 0.045f, 0.05f), Vector3.zero);  // 손목 커프
            for (int i = 0; i < 4; i++) // 손가락 — 살짝 말린 포즈
            {
                float x = (-0.027f + i * 0.018f) * mx;
                GlovePart(gloveRoot.transform, PrimitiveType.Capsule, new Vector3(x, -0.005f, 0.075f),
                          new Vector3(0.016f, 0.022f, 0.016f), new Vector3(70f, 0f, 0f));
            }
            GlovePart(gloveRoot.transform, PrimitiveType.Capsule, new Vector3(0.045f * mx, -0.005f, 0.02f),
                      new Vector3(0.016f, 0.02f, 0.016f), new Vector3(30f, 0f, -60f * mx)); // 엄지
        }

        private static void GlovePart(Transform parent, PrimitiveType shape, Vector3 pos, Vector3 scale, Vector3 euler)
        {
            var go = GameObject.CreatePrimitive(shape);
            go.name = "Part";
            Object.DestroyImmediate(go.GetComponent<Collider>()); // 비주얼 전용
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale    = scale;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.GetComponent<Renderer>().sharedMaterial = MapBuildUtil.Mat("#a9825f"); // 가죽 장갑 톤
        }

        /// <summary>손 비주얼 프리팹을 Hand GO 아래에 붙이고, "_hand" 직렬화 필드를 가진 컴포넌트 전부에 주입.</summary>
        private static void AttachHandVisual(GameObject visualPrefab, Component hand, string label)
        {
            if (hand == null)
            {
                Debug.LogWarning($"[MakeAssets] {label} Hand 컴포넌트를 못 찾음 — 비주얼 생략");
                return;
            }

            var visual = (GameObject)PrefabUtility.InstantiatePrefab(visualPrefab, hand.transform);
            foreach (var comp in visual.GetComponentsInChildren<Component>(true))
            {
                if (comp == null) continue;
                var so = new SerializedObject(comp);
                var prop = so.FindProperty("_hand");
                if (prop == null || prop.propertyType != SerializedPropertyType.ObjectReference) continue;
                prop.objectReferenceValue = hand;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.RecordPrefabInstancePropertyModifications(comp);
            }
        }

        /// <summary>장갑 전환 되돌리기 — 컨트롤러 비주얼 재활성 + 손 포즈 합성 끔 (OVRHands는 남겨둠, 무해).</summary>
        [MenuItem("Tools/Make Assets/Restore Controller Visuals (VR Rig)")]
        public static void RestoreControllerVisuals()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(k_path) == null) return;

            GameObject root = PrefabUtility.LoadPrefabContents(k_path);
            try
            {
                var manager = root.GetComponentInChildren<OVRManager>(true);
                if (manager != null)
                    manager.controllerDrivenHandPosesType = OVRManager.ControllerDrivenHandPosesType.None;

                int shown = 0;
                foreach (var tr in root.GetComponentsInChildren<Transform>(true))
                {
                    if (!tr.name.Contains("ControllerVisual") || tr.gameObject.activeSelf) continue;
                    tr.gameObject.SetActive(true);
                    shown++;
                }
                var handsRoot = FindChildByName(root.transform, "OVRHands");
                if (handsRoot != null) handsRoot.gameObject.SetActive(false);

                // 플레이스홀더 장갑도 숨김 — 컨트롤러 모델과 겹치지 않게
                foreach (var tr in root.GetComponentsInChildren<Transform>(true))
                    if (tr.name == "GloveVisual") tr.gameObject.SetActive(false);

                PrefabUtility.SaveAsPrefabAsset(root, k_path);
                Debug.Log($"[MakeAssets] 컨트롤러 비주얼 복구 — {shown}개 재활성, 손 포즈 합성 해제");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.Refresh();
        }

        private static Transform FindChildByName(Transform root, string name)
        {
            foreach (var tr in root.GetComponentsInChildren<Transform>(true))
                if (tr.name == name) return tr;
            return null;
        }

        private static Component FindComponentBySimpleNameOn(GameObject go, string typeName)
        {
            foreach (var c in go.GetComponents<Component>())
                if (c != null && c.GetType().Name == typeName) return c;
            return null;
        }

        // 네임스페이스가 불확실한 Meta 타입은 단순 이름으로 매칭 (VRUIFactory와 동일 패턴)
        private static Component FindComponentBySimpleName(GameObject root, string typeName)
        {
            foreach (var c in root.GetComponentsInChildren<Component>(true))
                if (c != null && c.GetType().Name == typeName) return c;
            return null;
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
