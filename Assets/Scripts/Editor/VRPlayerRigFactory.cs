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

        /// <summary>
        /// 인터랙션 리그(Add Interaction To VR Rig)가 함께 들여온 Meta Interaction SDK의
        /// "이동 편의(comfort) 비네트" — Locomotor.prefab 안에 소스 프리팹 `TunnelingEffect.prefab`
        /// (카메라 앞 쿼드+셰이더 렌더)의 인스턴스가 **두 개** 있는데, 인스턴스화되며 각각 다른
        /// 이름으로 오버라이드돼 있다 (소스 파일명 "TunnelingEffect"는 씬 어디에도 GameObject
        /// 이름으로 남아있지 않음 — Locomotor.prefab 실제 확인, 2026-07-20):
        ///   1) GameObject "SmoothMovementTunneling" — LocomotionTunneling(제어, ComfortTurning/
        ///      ComfortMoving)이 사용. 이동/회전할 때마다 컴포트 비네트를 켬 — "이동 시 시야가
        ///      좁아지며 화면이 검게" 증상의 직접 원인.
        ///   2) GameObject "WallPenetrationTunneling" — WallPenetrationTunneling이 사용. 트래킹된(실제)
        ///      머리 위치와 캐릭터 컨트롤러가 강제한 논리적 머리 위치 사이를 레이캐스트해서, 그 사이에
        ///      뭐라도 걸리면(벽 관통 감지) 자기 쪽 비네트를 강제로 켠다(enabled=true, UserFOV를 관통
        ///      거리 기반으로 좁힘). **헤드셋 없이 Simulator로 테스트하면 실제 걷는 게 아니라 캐릭터만
        ///      조이스틱으로 이동하므로, 트래킹 위치(거의 고정)와 논리 위치(계속 이동) 사이 간격이 점점
        ///      벌어져 그 사이 바닥/벽에 항상 레이가 걸림** — 그래서 이동만 하면 방향 무관하게 화면이
        ///      가려진 것.
        ///
        /// 1차 시도는 GameObject 이름을 소스 프리팹 파일명 그대로("TunnelingEffect")로 착각해 찾아서
        /// **둘 다 안 걸리고 조용히 no-op**했다(changed==0이라 "이미 정리됨" 로그만 찍고 실제로는
        /// 아무것도 안 끔 — 2026-07-20 재확인, 실제 GameObject 이름 확인 후 수정).
        /// LocomotionTunneling은 되돌리기(원래 상태 유지 — 리셋 로직 보유). 두 GameObject 전부
        /// 비활성화 — 어떤 스크립트가 enabled를 토글하든 GameObject 자체가 꺼져 있으면 안 그려진다.
        /// idempotent.
        /// </summary>
        [MenuItem("Tools/Make Assets/Disable Locomotion Comfort Vignette (VR Rig)")]
        public static void DisableLocomotionComfortVignette()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(k_path) == null)
            {
                Debug.LogError($"[MakeAssets] VRPlayerRig 없음: {k_path}");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(k_path);
            try
            {
                int changed = 0;

                // LocomotionTunneling은 그대로 켜져 있어야 정상 동작(리셋 로직 보유) — 되돌리기
                foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour == null || behaviour.GetType().Name != "LocomotionTunneling") continue;
                    if (behaviour.enabled) continue;
                    behaviour.enabled = true;
                    changed++;
                }

                // 실제 고정 — 렌더러가 붙은 두 GameObject 전부 통째로 비활성화
                foreach (var tr in root.GetComponentsInChildren<Transform>(true))
                {
                    if ((tr.name != "SmoothMovementTunneling" && tr.name != "WallPenetrationTunneling") || !tr.gameObject.activeSelf) continue;
                    tr.gameObject.SetActive(false);
                    changed++;
                }

                if (changed > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, k_path);
                    Debug.Log($"[MakeAssets] 이동 편의 비네트 정리 완료 — SmoothMovementTunneling·WallPenetrationTunneling " +
                              "GameObject 비활성화(+ LocomotionTunneling 원복) — 화면 암전 해소");
                }
                else
                {
                    Debug.Log("[MakeAssets] 이미 정리됨(변경 없음)");
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
                if (child.name == "GloveVisual") { Object.DestroyImmediate(child.gameObject); break; } // 구버전 제거 후 재조립 (포즈 갱신 반영)

            var gloveRoot = new GameObject("GloveVisual");
            gloveRoot.transform.SetParent(handAnchor, false);
            float mx = mirrorX ? -1f : 1f;

            // 자연 그립 포즈 — 컨트롤러 앵커 +Z(포인팅 축) 그대로면 손이 꼬치처럼 일자로 뻗는다.
            // 시뮬레이터 실측 튜닝값(오른손 기준, 2026-07-16): 일직선 자세에서 살짝 오른쪽으로 기울어진 그립.
            // 왼손은 미러 (y·z 부호 반전).
            gloveRoot.transform.localPosition = new Vector3(0f, -0.015f, -0.02f);
            gloveRoot.transform.localRotation = Quaternion.Euler(-16.787f, -7.495f * mx, -223.904f * mx);

            gloveRoot.AddComponent<GloveFallbackVisual>();

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

        /// <summary>검증용 — 리그 프리팹의 GloveVisual 로컬 포즈를 문자열로 반환 (ClaudeBridge Reflection.Invoke 용).</summary>
        public static string DumpGlovePose()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(k_path);
            if (prefab == null) return "prefab not found: " + k_path;

            var sb = new System.Text.StringBuilder();
            foreach (var tr in prefab.GetComponentsInChildren<Transform>(true))
            {
                if (tr.name != "GloveVisual") continue;
                sb.Append($"{tr.parent.name}: pos={tr.localPosition} euler={tr.localEulerAngles} parts={tr.childCount}; ");
            }
            return sb.Length > 0 ? sb.ToString() : "GloveVisual not found";
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

        // ── 텔레포트 착지 리티클 ────────────────────────────────────
        // 아크는 뜨는데 착지점 표시가 없다는 사용자 피드백(RVRF 비교). Meta Interaction SDK가 제공하는
        // TeleportReticleMaterial은 커스텀 빌트인 RP 셰이더(Unlit/Hotspot)라 URP 프로젝트에서 안 보인다
        // (2026-07-20 실측 — 리티클을 배선했는데도 육안으로 변화 없음. RVRF 낚싯대 머티리얼과 같은
        // 부류의 문제, RvrfTackleFactory 참조). 커스텀 셰이더라 단순 URP/Lit 치환은 원형 표시 로직을
        // 잃으므로, SDK 제공 원형 텍스처(Reticle-Circle.png)로 URP 파티클 언릿 머티리얼을 새로 만든다 —
        // 링 성장/하이라이트 애니메이션은 포기하고 "여기 착지 가능" 표시만 담당.
        private const string k_teleportReticleTexGuid = "4aa4a1b9a03d9b54d80515831235fbe9"; // Reticle-Circle.png
        private const string k_urpParticleUnlitShader  = "Universal Render Pipeline/Particles/Unlit";
        private const string k_reticleMatPath          = "Assets/Prefabs/Materials/Mat_TeleportReticle.mat";

        [MenuItem("Tools/Make Assets/Add Teleport Reticle (VR Rig)")]
        public static void AddTeleportReticle()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(k_path) == null)
            {
                Debug.LogError($"[MakeAssets] VRPlayerRig 없음: {k_path}");
                return;
            }

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(k_reticleMatPath);
            if (mat == null)
            {
                Shader shader = Shader.Find(k_urpParticleUnlitShader);
                if (shader == null)
                {
                    Debug.LogError($"[MakeAssets] 셰이더 없음: {k_urpParticleUnlitShader}");
                    return;
                }
                string texPath = AssetDatabase.GUIDToAssetPath(k_teleportReticleTexGuid);
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

                mat = new Material(shader) { name = "Mat_TeleportReticle" };
                mat.SetFloat("_Surface", 1f); // Transparent
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0f);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                if (tex != null) mat.SetTexture("_BaseMap", tex);
                mat.SetColor("_BaseColor", new Color(0.3f, 1f, 0.5f, 0.85f)); // 착지 가능 = 초록

                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(k_reticleMatPath)!);
                AssetDatabase.CreateAsset(mat, k_reticleMatPath);
            }

            GameObject root = PrefabUtility.LoadPrefabContents(k_path);
            try
            {
                // 이 리그엔 TeleportInteractor가 5개 있다(2026-07-20 실측) — 좌/우 TeleportControllerInteractor
                // (컨트롤러 모드, 기본 비활성 — OVR가 런타임에 컨트롤러 감지 시 켬), 좌/우
                // TeleportMicrogestureInteractor(핸드트래킹 모드, 기본 활성이나 이 프로젝트는 미사용),
                // BodyTeleportInteractor(Locomotor 직속, 항상 활성) — 사용자가 실제로 보는 레이는 이쪽.
                var allInteractors = root.GetComponentsInChildren<Oculus.Interaction.Locomotion.TeleportInteractor>(true);
                Oculus.Interaction.Locomotion.TeleportInteractor bodyInteractor = null;
                foreach (var ti in allInteractors)
                    if (ti.gameObject.name == "BodyTeleportInteractor") { bodyInteractor = ti; break; }

                if (bodyInteractor == null)
                {
                    Debug.LogError("[MakeAssets] BodyTeleportInteractor 없음 — 먼저 Add Interaction To VR Rig 실행");
                    return;
                }

                Oculus.Interaction.DistanceReticles.TeleportReticleDrawer drawer = null;
                foreach (var d in root.GetComponentsInChildren<Oculus.Interaction.DistanceReticles.TeleportReticleDrawer>(true))
                {
                    var ip = new SerializedObject(d).FindProperty("_interactor");
                    if (ip.objectReferenceValue == (Object)bodyInteractor) { drawer = d; break; }
                }

                MeshRenderer renderer;
                if (drawer == null)
                {
                    var reticle = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    reticle.name = "TeleportReticle";
                    Object.DestroyImmediate(reticle.GetComponent<Collider>()); // 순수 표시용
                    reticle.transform.SetParent(root.transform, false);
                    reticle.transform.localScale = Vector3.one * 0.35f;

                    renderer = reticle.GetComponent<MeshRenderer>();
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                    drawer = reticle.AddComponent<Oculus.Interaction.DistanceReticles.TeleportReticleDrawer>();
                    var so = new SerializedObject(drawer);
                    so.FindProperty("_interactor").objectReferenceValue = bodyInteractor;
                    so.FindProperty("_targetRenderer").objectReferenceValue = renderer;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                else
                {
                    renderer = drawer.GetComponent<MeshRenderer>();
                }

                renderer.sharedMaterial = mat; // URP 머티리얼로 (재)적용 — 기존 SDK 빌트인 머티리얼 대체
                PrefabUtility.SaveAsPrefabAsset(root, k_path);
                Debug.Log("[MakeAssets] BodyTeleportInteractor용 텔레포트 착지 리티클 배선 완료 (URP 머티리얼)");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.Refresh();
        }
    }
}
#endif
