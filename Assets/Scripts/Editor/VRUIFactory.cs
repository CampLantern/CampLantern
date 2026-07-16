#if UNITY_EDITOR
using System;
using CampLantern.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CampLantern.EditorTools
{
    /// <summary>
    /// VR UI 기반 세트 생성기 — 월드스페이스 Canvas 패널 + VR 버튼 + Meta Interaction(레이/포크) 배선.
    /// 나머지 모든 게임 UI가 이 위에 지어지는 토대다.
    ///
    /// 생성물:
    ///   - Assets/Prefabs/UI/VRUIButton.prefab       — UGUI Button + TMP 라벨(VRUIButton)
    ///   - Assets/Prefabs/UI/VRUIPanel.prefab        — 월드스페이스 Canvas + PointableCanvas(레이/포크) + 제목 + 샘플 버튼
    ///   - Assets/Prefabs/UI/VRUIEventSystem.prefab  — EventSystem + PointableCanvasModule (씬당 1개 필요)
    ///
    /// 레이/포크 인터랙션 원리(Interaction SDK 리서치 결과):
    ///   RayInteractor/PokeInteractor(리그) → Ray/PokeInteractable(패널의 인터랙션 자식) → PointableCanvas
    ///   → PointableCanvasModule(EventSystem) → GraphicRaycaster → UGUI Button.
    ///   인터랙션 자식은 Meta의 Template_Ray/PokeInteraction 프리팹을 "복제"(Object.Instantiate → 에디터 에셋
    ///   링크 없음)해 붙이고, PointableCanvas._canvas만 이 패널의 Canvas로 주입한다. Template의 BoundsClipper는
    ///   x/y 크기 0이라 RectTransform(anchors 0-1)에서 자동 산출 → 어떤 Canvas 크기에도 맞는다.
    ///
    /// 선행: TMP Essentials(기본 폰트). 없으면 라벨 폰트가 비어 경고만 — 먼저
    ///   Tools > Make Assets > Import TMP Essentials 실행 권장.
    /// 리그 배선(레이/포크 인터랙터)은 별도 — Tools > Make Assets > Add Interaction To VR Rig.
    ///
    /// RULE-02: .prefab 텍스트 직접 작성 금지 — PrefabUtility.SaveAsPrefabAsset만 사용.
    /// </summary>
    public static class VRUIFactory
    {
        private const string k_folder = "Assets/Prefabs/UI";
        private const string k_buttonPath      = k_folder + "/VRUIButton.prefab";
        private const string k_panelPath       = k_folder + "/VRUIPanel.prefab";
        private const string k_eventSystemPath = k_folder + "/VRUIEventSystem.prefab";

        // Meta Interaction SDK 타입/에셋 (풀네임·GUID로 해석 — 어셈블리 직접 참조 회피로 컴파일 격리)
        private const string k_pointableCanvasType       = "Oculus.Interaction.PointableCanvas";
        private const string k_pointableCanvasModuleType = "Oculus.Interaction.PointableCanvasModule";
        private const string k_rayTemplateGuid  = "8369d93f7b6b99742bbea0649a41b7b1"; // Template_RayInteraction.prefab
        private const string k_pokeTemplateGuid = "4db41829582c7d24f80ee9603868dd67"; // Template_PokeInteraction.prefab

        // 그레이박스 팔레트
        private static readonly Color k_panelBg  = new Color(0.12f, 0.12f, 0.15f, 0.92f);
        private static readonly Color k_buttonBg = new Color(0.20f, 0.28f, 0.40f, 1f);
        private static readonly Color k_text     = new Color(0.92f, 0.94f, 0.96f, 1f);

        [MenuItem("Tools/Make Assets/VR UI (Create All)")]
        public static void CreateAll() => CreateInternal(force: false);

        [MenuItem("Tools/Make Assets/VR UI (Force Recreate)")]
        public static void ForceRecreate() => CreateInternal(force: true);

        private static void CreateInternal(bool force)
        {
            EnsureFolder("Assets/Prefabs");
            EnsureFolder(k_folder);

            TMP_FontAsset font = FindDefaultFont();
            if (font == null)
                Debug.LogWarning("[MakeAssets] TMP 기본 폰트 없음 — 먼저 Tools > Make Assets > Import TMP Essentials 실행 권장. " +
                                 "라벨은 폰트 없이 생성됨(임포트 후 자동 적용되지 않으니 Force Recreate 필요).");

            GameObject buttonPrefab = CreateButton(force, font);
            CreatePanel(force, font, buttonPrefab);
            CreateEventSystem(force);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MakeAssets] VR UI 기반 세트 생성 완료: " + k_folder +
                      " — 씬에 VRUIEventSystem 1개 배치 + VRUIPanel 배치 후, VRPlayerRig에 인터랙터 추가" +
                      "(Tools > Make Assets > Add Interaction To VR Rig)");
        }

        // ── TMP Essentials 임포트 (선행) ─────────────────────────────
        // 기본 폰트/셰이더/설정이 없으면 TMP 텍스트가 렌더되지 않는다. TMP_PackageResourceImporter를
        // 리플렉션으로 호출(내부 타입). 임포트는 비동기라 완료 후 VR UI (Force Recreate)로 폰트를 굽는다.
        [MenuItem("Tools/Make Assets/Import TMP Essentials")]
        public static void ImportTmpEssentials()
        {
            if (FindDefaultFont() != null)
            {
                Debug.Log("[MakeAssets] TMP 폰트가 이미 존재 — 임포트 생략.");
                return;
            }

            Type t = FindType("TMPro.TMP_PackageResourceImporter");
            if (t == null)
            {
                Debug.LogError("[MakeAssets] TMP_PackageResourceImporter 타입을 못 찾음 — TextMeshPro 패키지 확인. " +
                               "수동: Window > TextMeshPro > Import TMP Essential Resources.");
                return;
            }

            try
            {
                object importer = Activator.CreateInstance(t, nonPublic: true);
                var m = t.GetMethod("ImportResources",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);
                if (m == null || m.GetParameters().Length != 3)
                {
                    Debug.LogError("[MakeAssets] ImportResources(bool,bool,bool) 시그니처를 못 찾음 — " +
                                   "수동: Window > TextMeshPro > Import TMP Essential Resources.");
                    return;
                }
                m.Invoke(importer, new object[] { true, false, false }); // essentials only
                Debug.Log("[MakeAssets] TMP Essentials 임포트 요청됨(비동기). 완료 후 " +
                          "Tools > Make Assets > VR UI (Force Recreate)로 라벨 폰트를 굽는다.");
            }
            catch (Exception e)
            {
                Debug.LogError("[MakeAssets] TMP Essentials 임포트 실패: " + e.Message +
                               " — 수동: Window > TextMeshPro > Import TMP Essential Resources.");
            }
        }

        // ── VR 버튼 ──────────────────────────────────────────────────
        private static GameObject CreateButton(bool force, TMP_FontAsset font)
        {
            if (!force && AssetDatabase.LoadAssetAtPath<GameObject>(k_buttonPath) != null)
            {
                Debug.LogWarning($"[MakeAssets] 이미 존재(건너뜀): {k_buttonPath}");
                return AssetDatabase.LoadAssetAtPath<GameObject>(k_buttonPath);
            }

            var root = NewUI("VRUIButton");
            GameObject saved = null;
            try
            {
                SetSize(root, 240f, 88f); // 필 스프라이트(210x90) 비율 유지 — 과도한 가로 늘림 금지

                var bg = root.AddComponent<Image>();
                bg.color = k_buttonBg;
                VRUISkin.TryApplyButton(bg); // Colorful_UI 스킨 (없으면 위 단색 유지)
                bg.raycastTarget = true; // 버튼 클릭 판정 대상

                var button = root.AddComponent<Button>();
                button.targetGraphic = bg;

                var label = AddText(root.transform, "Label", "Button", 30f, font);
                Fill(label.rectTransform);
                if (VRUISkin.HasSkin) label.color = VRUISkin.PillText(VRUISkin.PillColor.Blue); // 밝은 시안 필 위 진남색

                var vrButton = root.AddComponent<VRUIButton>();
                SetRef(vrButton, "m_button", button);
                SetRef(vrButton, "m_label", label);

                saved = PrefabUtility.SaveAsPrefabAsset(root, k_buttonPath);
                Debug.Log("[MakeAssets] VRUIButton 생성: " + k_buttonPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            return saved;
        }

        // ── VR 패널 ──────────────────────────────────────────────────
        private static void CreatePanel(bool force, TMP_FontAsset font, GameObject buttonPrefab)
        {
            if (!force && AssetDatabase.LoadAssetAtPath<GameObject>(k_panelPath) != null)
            {
                Debug.LogWarning($"[MakeAssets] 이미 존재(건너뜀): {k_panelPath}");
                return;
            }

            var root = NewUI("VRUIPanel");
            try
            {
                // 월드스페이스 Canvas: 400x520px * 0.001 = 0.4 x 0.52 m
                SetSize(root, 400f, 520f);
                root.transform.localScale = new Vector3(0.001f, 0.001f, 0.001f);

                var canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                root.AddComponent<CanvasScaler>();
                root.AddComponent<GraphicRaycaster>(); // PointableCanvas가 요구(Start에서 Assert)

                var bg = root.AddComponent<Image>();
                bg.color = k_panelBg;
                VRUISkin.TryApplyPanel(bg); // Colorful_UI 스킨 (없으면 위 단색 유지)
                bg.raycastTarget = true;

                var panel = root.AddComponent<VRUIPanel>();

                // 제목 헤더 필 — 패널 상단 모서리에 반쯤 걸치는 킷 시그니처 배치 (Title보다 먼저 = 뒤에 깔림)
                if (VRUISkin.HasSkin)
                {
                    var headerGo = NewUI("Header");
                    headerGo.transform.SetParent(root.transform, false);
                    var headerRt = headerGo.GetComponent<RectTransform>();
                    headerRt.anchorMin = new Vector2(0.5f, 1f);
                    headerRt.anchorMax = new Vector2(0.5f, 1f);
                    headerRt.pivot     = new Vector2(0.5f, 0.5f);
                    headerRt.sizeDelta = new Vector2(280f, 76f); // 필 비율 3.7:1 이내
                    headerRt.anchoredPosition = new Vector2(0f, 4f); // 상단 모서리 걸침
                    var headerImg = headerGo.AddComponent<Image>();
                    headerImg.raycastTarget = false;
                    VRUISkin.TryApplyHeader(headerImg);
                }

                // 제목 — 헤더 필 위에 겹침 (스킨 없으면 기존 상단 배치와 동일 영역)
                var title = AddText(root.transform, "Title", "Panel", 32f, font);
                var titleRt = title.rectTransform;
                titleRt.anchorMin = new Vector2(0.5f, 1f);
                titleRt.anchorMax = new Vector2(0.5f, 1f);
                titleRt.pivot     = new Vector2(0.5f, 0.5f);
                titleRt.sizeDelta = new Vector2(280f, 76f);
                titleRt.anchoredPosition = new Vector2(0f, VRUISkin.HasSkin ? 4f : -55f);
                title.alignment = TextAlignmentOptions.Center;

                // 콘텐츠 영역 (제목 아래 전체)
                var content = NewUI("Content");
                content.transform.SetParent(root.transform, false);
                var contentRt = content.GetComponent<RectTransform>();
                contentRt.anchorMin = new Vector2(0f, 0f);
                contentRt.anchorMax = new Vector2(1f, 1f);
                contentRt.pivot     = new Vector2(0.5f, 1f);
                contentRt.offsetMin = new Vector2(28f, 28f);
                contentRt.offsetMax = new Vector2(-28f, VRUISkin.HasSkin ? -64f : -96f); // 헤더 절반이 밖에 걸림

                // 샘플 버튼 2개(수직 배치) — 즉시 테스트/시각 확인용. 실사용 시 복제/치환.
                if (buttonPrefab != null)
                {
                    AddSampleButton(buttonPrefab, contentRt, "확인",   0f);
                    AddSampleButton(buttonPrefab, contentRt, "닫기", -104f); // 버튼 88 + 간격 16
                }

                // 레이/포크 인터랙션 자식 (Meta Template 복제 + _canvas 주입)
                AddCanvasInteraction(root, canvas);

                SetRef(panel, "m_canvas", canvas);
                SetRef(panel, "m_content", contentRt);
                SetRef(panel, "m_title", title);

                PrefabUtility.SaveAsPrefabAsset(root, k_panelPath);
                Debug.Log("[MakeAssets] VRUIPanel 생성: " + k_panelPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void AddSampleButton(GameObject buttonPrefab, RectTransform content, string label, float y)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(buttonPrefab);
            inst.transform.SetParent(content, false);
            var rt = inst.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, y);

            // 라벨 오버라이드 — 중첩 프리팹 인스턴스의 TMP 텍스트를 직접 굽는다
            var tmp = inst.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp != null)
            {
                tmp.text = label;
                PrefabUtility.RecordPrefabInstancePropertyModifications(tmp);
            }
        }

        // Meta Template_Ray/PokeInteraction을 "복제"해 Canvas 자식으로 붙이고 PointableCanvas._canvas 주입.
        // Object.Instantiate라 원본(에디터 폴더) 에셋 링크가 남지 않아 런타임 프리팹에 안전하게 굽힌다.
        private static void AddCanvasInteraction(GameObject canvasRoot, Canvas canvas)
        {
            InstantiateTemplate(k_rayTemplateGuid,  "Template_RayInteraction",  "RayInteraction",  canvasRoot, canvas);
            InstantiateTemplate(k_pokeTemplateGuid, "Template_PokeInteraction", "PokeInteraction", canvasRoot, canvas);
        }

        private static void InstantiateTemplate(string guid, string nameFallback, string childName,
                                                GameObject canvasRoot, Canvas canvas)
        {
            GameObject template = LoadPrefab(guid, nameFallback);
            if (template == null)
            {
                Debug.LogWarning($"[MakeAssets] {nameFallback} 템플릿을 못 찾음 — 레이/포크 인터랙션 건너뜀. " +
                                 "Meta Interaction SDK 설치 확인.");
                return;
            }

            var inst = (GameObject)UnityEngine.Object.Instantiate(template); // 복제(링크 없음)
            inst.name = childName;
            inst.transform.SetParent(canvasRoot.transform, false);
            Fill(inst.GetComponent<RectTransform>());

            // PointableCanvas._canvas 주입
            Component pc = FindComponentByType(inst, k_pointableCanvasType);
            if (pc != null)
            {
                var so = new SerializedObject(pc);
                var prop = so.FindProperty("_canvas");
                if (prop != null) { prop.objectReferenceValue = canvas; so.ApplyModifiedPropertiesWithoutUndo(); }
                else Debug.LogWarning($"[MakeAssets] {childName}: PointableCanvas._canvas 필드 없음 — SDK 버전 확인");
            }
            else
            {
                Debug.LogWarning($"[MakeAssets] {childName}: PointableCanvas 컴포넌트 없음 — 템플릿 구조 확인");
            }
        }

        // ── EventSystem + PointableCanvasModule ──────────────────────
        private static void CreateEventSystem(bool force)
        {
            if (!force && AssetDatabase.LoadAssetAtPath<GameObject>(k_eventSystemPath) != null)
            {
                Debug.LogWarning($"[MakeAssets] 이미 존재(건너뜀): {k_eventSystemPath}");
                return;
            }

            var root = new GameObject("VRUIEventSystem");
            try
            {
                root.AddComponent<EventSystem>();

                Type moduleType = FindType(k_pointableCanvasModuleType);
                if (moduleType != null)
                    root.AddComponent(moduleType); // PointerInputModule(BaseInputModule) — StandaloneInputModule 대체
                else
                    Debug.LogWarning("[MakeAssets] PointableCanvasModule 타입을 못 찾음 — Meta Interaction SDK 확인. " +
                                     "VR UI 클릭이 동작하지 않는다.");

                PrefabUtility.SaveAsPrefabAsset(root, k_eventSystemPath);
                Debug.Log("[MakeAssets] VRUIEventSystem 생성: " + k_eventSystemPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        // ── VR 리그에 레이/포크 인터랙터 배선 ─────────────────────────
        // VRPlayerRig의 OVRCameraRig 아래에 Meta의 OVRComprehensiveInteractionRig(올인원)를 자식으로
        // 붙이고 OVRCameraRigRef._ovrCameraRig만 주입한다(리서치 결과: 이 참조 하나만 null). 나머지
        // (컨트롤러/핸드 데이터소스의 _cameraRigRef 등)는 프리팹에 이미 배선돼 있다.
        //
        // 주의: 올인원 리그는 UI용 레이/포크뿐 아니라 그랩·로코모션·텔레포트 인터랙터도 포함한다.
        //   수동 조립(OVRInteraction+OVRControllers)은 교차 참조 수십 개를 손으로 배선해야 해 취약하므로
        //   견고성을 위해 올인원을 쓴다. UI 전용 경량 리그가 필요하면 후속 작업으로 분리.
        [MenuItem("Tools/Make Assets/Add Interaction To VR Rig")]
        public static void AddInteractionToVrRig()
        {
            const string rigPath = "Assets/Prefabs/VRPlayerRig.prefab";
            const string comprehensiveRigGuid = "0a7d2469f24041c4284c66706f84c45e"; // OVRComprehensiveInteractionRig.prefab

            if (AssetDatabase.LoadAssetAtPath<GameObject>(rigPath) == null)
            {
                Debug.LogError($"[MakeAssets] VRPlayerRig 없음: {rigPath} — 먼저 Tools > Make Assets > VR Player Rig 실행");
                return;
            }

            GameObject interactionRig = LoadPrefab(comprehensiveRigGuid, "OVRComprehensiveInteractionRig");
            if (interactionRig == null)
            {
                Debug.LogError("[MakeAssets] OVRComprehensiveInteractionRig 프리팹 없음 — Meta Interaction SDK(.ovr) 설치 확인");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(rigPath);
            try
            {
                var camRig = root.GetComponentInChildren<OVRCameraRig>(true);
                if (camRig == null)
                {
                    Debug.LogError("[MakeAssets] VRPlayerRig에 OVRCameraRig가 없음 — 리그 구조 확인");
                    return;
                }

                // 중복 추가 방지 (이미 인터랙션 리그가 있으면 OVRCameraRigRef가 존재)
                if (FindComponentBySimpleName(root, "OVRCameraRigRef") != null)
                {
                    Debug.LogWarning("[MakeAssets] VRPlayerRig에 이미 인터랙션 리그가 있음 — 건너뜀");
                    return;
                }

                var inst = (GameObject)PrefabUtility.InstantiatePrefab(interactionRig, camRig.gameObject.scene);
                inst.transform.SetParent(camRig.transform, false);
                inst.transform.localPosition = Vector3.zero;
                inst.transform.localRotation = Quaternion.identity;

                Component rigRef = FindComponentBySimpleName(inst, "OVRCameraRigRef");
                if (rigRef != null)
                {
                    var so = new SerializedObject(rigRef);
                    var prop = so.FindProperty("_ovrCameraRig");
                    if (prop != null)
                    {
                        prop.objectReferenceValue = camRig;
                        so.ApplyModifiedPropertiesWithoutUndo();
                        PrefabUtility.RecordPrefabInstancePropertyModifications(rigRef);
                    }
                    else Debug.LogWarning("[MakeAssets] OVRCameraRigRef._ovrCameraRig 필드 없음 — SDK 버전 확인");
                }
                else Debug.LogWarning("[MakeAssets] OVRCameraRigRef 컴포넌트를 못 찾음 — 인터랙션 리그 구조 확인");

                // 로코모션/터널링 필수 참조(_playerOrigin/_centerEyeCamera)도 함께 배선 — 안 하면 플레이 시
                // AssertField 경고 + 이동/터널링 NullRef (InteractionRigRepair 참조).
                InteractionRigRepair.WireRig(inst, camRig);

                PrefabUtility.SaveAsPrefabAsset(root, rigPath);
                Debug.Log("[MakeAssets] VRPlayerRig에 레이/포크(+그랩/로코모션) 인터랙션 리그 추가 완료: " + rigPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.Refresh();
        }

        // ── 헬퍼 ─────────────────────────────────────────────────────

        private static GameObject NewUI(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            return go;
        }

        private static void SetSize(GameObject go, float w, float h)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(w, h);
        }

        private static void Fill(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        private static TextMeshProUGUI AddText(Transform parent, string name, string text, float size, TMP_FontAsset font)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = k_text;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;   // 라벨은 클릭 대상 아님(성능)
            tmp.richText = false;        // 불필요한 파싱 비용 제거(make-assets 규칙)
            if (font != null) tmp.font = font;
            return tmp;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }

        private static void SetRef(Component comp, string fieldName, UnityEngine.Object value)
        {
            var so = new SerializedObject(comp);
            var prop = so.FindProperty(fieldName)
                       ?? throw new InvalidOperationException($"[MakeAssets] 필드 없음: {comp.GetType().Name}.{fieldName}");
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static TMP_FontAsset FindDefaultFont()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:TMP_FontAsset"))
            {
                var f = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (f != null) return f;
            }
            return null;
        }

        private static GameObject LoadPrefab(string guid, string nameFallback)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var go = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go != null) return go;

            foreach (string g in AssetDatabase.FindAssets($"{nameFallback} t:GameObject"))
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (System.IO.Path.GetFileNameWithoutExtension(p) == nameFallback)
                    return AssetDatabase.LoadAssetAtPath<GameObject>(p);
            }
            return null;
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

        private static Component FindComponentByType(GameObject go, string typeFullName)
        {
            foreach (var c in go.GetComponentsInChildren<Component>(true))
                if (c != null && c.GetType().FullName == typeFullName) return c;
            return null;
        }

        // 네임스페이스가 불확실한 Meta 타입(예: OVRCameraRigRef)은 단순 이름으로 매칭.
        private static Component FindComponentBySimpleName(GameObject go, string typeName)
        {
            foreach (var c in go.GetComponentsInChildren<Component>(true))
                if (c != null && c.GetType().Name == typeName) return c;
            return null;
        }
    }
}
#endif
