#if UNITY_EDITOR
using CampLantern.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CampLantern.EditorTools
{
    /// <summary>
    /// 범용 액션 리스트 패널 프리팹 생성 — IMGUI 상점·배치·요리·판매·수리 블록의 VR 대체 토대.
    ///   - ActionRow.prefab       : 라벨 + VRUIButton 2개(중첩, 오른쪽 정렬)
    ///   - ActionListPanel.prefab : VRUIPanel 베이스(레이/포크 배선 승계) 400x640 + 행 컨테이너.
    ///                              Resources 소재 — 하네스가 Resources.Load로 띄운다.
    /// RULE-02: .prefab 직접 작성 금지 — PrefabUtility API만 사용.
    /// </summary>
    public static class ActionPanelFactory
    {
        private const string k_rowPath        = "Assets/Prefabs/UI/ActionRow.prefab";
        private const string k_panelPath      = "Assets/Resources/ActionListPanel.prefab";
        private const string k_basePanelPath  = "Assets/Prefabs/UI/VRUIPanel.prefab";
        private const string k_baseButtonPath = "Assets/Prefabs/UI/VRUIButton.prefab";

        [MenuItem("Tools/Make Assets/Action Panel UI")]
        public static void CreateAll()
        {
            var basePanel  = AssetDatabase.LoadAssetAtPath<GameObject>(k_basePanelPath);
            var baseButton = AssetDatabase.LoadAssetAtPath<GameObject>(k_baseButtonPath);
            if (basePanel == null || baseButton == null)
            {
                Debug.LogError("[MakeAssets] VRUIPanel/VRUIButton 프리팹 없음 — 먼저 Tools > Make Assets > VR UI (Create All)");
                return;
            }

            TMP_FontAsset font = LoadKoreanFont();
            GameObject rowPrefab = CreateRow(baseButton, font);
            CreatePanel(basePanel, rowPrefab.GetComponent<ActionRow>());

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[MakeAssets] 액션 패널 UI 생성 완료 — {k_rowPath}, {k_panelPath}");
        }

        private static GameObject CreateRow(GameObject baseButton, TMP_FontAsset font)
        {
            var root = new GameObject("ActionRow", typeof(RectTransform));
            try
            {
                ((RectTransform)root.transform).sizeDelta = new Vector2(360f, 62f);
                VRUISkin.TryAddRowBackground(root); // 탭 바 배경 (스킨 없으면 무배경)

                // 라벨 (좌측 고정, 길면 말줄임)
                var labelGo = new GameObject("Label", typeof(RectTransform));
                labelGo.transform.SetParent(root.transform, false);
                var lrt = (RectTransform)labelGo.transform;
                lrt.anchorMin = new Vector2(0f, 0.5f);
                lrt.anchorMax = new Vector2(0f, 0.5f);
                lrt.pivot     = new Vector2(0f, 0.5f);
                lrt.sizeDelta = new Vector2(250f, 56f);
                lrt.anchoredPosition = new Vector2(4f, 0f);
                var label = labelGo.AddComponent<TextMeshProUGUI>();
                label.text          = "항목";
                label.fontSize      = 20f;
                label.alignment     = TextAlignmentOptions.MidlineLeft;
                label.color         = VRUISkin.RowText; // 시안 행 바 위 진남색
                label.raycastTarget = false;
                label.richText      = false;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.overflowMode  = TextOverflowModes.Ellipsis;
                if (font != null) label.font = font;

                // 버튼 2개 (우측 정렬, VRUIButton 중첩)
                var button1 = AddRowButton(baseButton, root.transform, new Vector2(-114f, 0f));
                var button2 = AddRowButton(baseButton, root.transform, new Vector2(-2f, 0f));

                var row = root.AddComponent<ActionRow>();
                SetRef(row, "m_label", label);
                SetRef(row, "m_button1", button1);
                SetRef(row, "m_button2", button2);

                return PrefabUtility.SaveAsPrefabAsset(root, k_rowPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static VRUIButton AddRowButton(GameObject baseButton, Transform parent, Vector2 pos)
        {
            var btnGo = (GameObject)PrefabUtility.InstantiatePrefab(baseButton);
            btnGo.transform.SetParent(parent, false);
            var rt = (RectTransform)btnGo.transform;
            rt.anchorMin = new Vector2(1f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot     = new Vector2(1f, 0.5f);
            rt.sizeDelta = new Vector2(104f, 54f); // 필 비율 유지 (1.9:1)
            rt.anchoredPosition = pos;
            PrefabUtility.RecordPrefabInstancePropertyModifications(rt);

            var label = btnGo.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
            {
                label.fontSize = 19f;
                PrefabUtility.RecordPrefabInstancePropertyModifications(label);
            }
            return btnGo.GetComponent<VRUIButton>();
        }

        private static void CreatePanel(GameObject basePanel, ActionRow rowPrefabComp)
        {
            // VRUIPanel 베이스 언팩 — 레이/포크 인터랙션 자식·GraphicRaycaster를 그대로 승계
            var root = (GameObject)PrefabUtility.InstantiatePrefab(basePanel);
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            try
            {
                root.name = "ActionListPanel";

                // UI 차별화: 액션 리스트(상점·배치·요리 등)는 초록 헤더 (소셜=보라, 인벤토리=파랑)
                var header = root.transform.Find("Header");
                if (header != null && VRUISkin.TryApplyPill(header.GetComponent<Image>(), VRUISkin.PillColor.Green))
                {
                    var panelTitle = root.GetComponentInChildren<TextMeshProUGUI>(true);
                    if (panelTitle != null) panelTitle.color = VRUISkin.PillText(VRUISkin.PillColor.Green);
                }

                // 목록형이라 세로로 길게 (400x520 → 400x640). 인터랙션 자식은 anchors 0-1이라 따라온다.
                ((RectTransform)root.transform).sizeDelta = new Vector2(400f, 640f);

                var vrui = root.GetComponent<VRUIPanel>();
                RectTransform content = vrui.Content;

                for (int i = content.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(content.GetChild(i).gameObject);

                // 행 컨테이너 — 동적 콘텐츠라 레이아웃 그룹 유지 (make-assets 예외 조항)
                var rowsGo = new GameObject("Rows", typeof(RectTransform));
                rowsGo.transform.SetParent(content, false);
                var rrt = (RectTransform)rowsGo.transform;
                rrt.anchorMin = new Vector2(0f, 1f);
                rrt.anchorMax = new Vector2(1f, 1f);
                rrt.pivot     = new Vector2(0.5f, 1f);
                rrt.sizeDelta = new Vector2(0f, 0f);
                rrt.anchoredPosition = Vector2.zero;
                var vlg = rowsGo.AddComponent<VerticalLayoutGroup>();
                vlg.spacing = 6f;
                vlg.childAlignment = TextAnchor.UpperCenter;
                vlg.childControlHeight = false; vlg.childControlWidth = false;
                vlg.childForceExpandHeight = false; vlg.childForceExpandWidth = false;
                var csf = rowsGo.AddComponent<ContentSizeFitter>();
                csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                var panel = root.AddComponent<ActionListPanel>();
                SetRef(panel, "m_panel", vrui);
                SetRef(panel, "m_rowsContent", rrt);
                SetRef(panel, "m_rowPrefab", rowPrefabComp);

                // 월드 배치형 — 항상 플레이어를 향하게
                var faceSo = new SerializedObject(vrui);
                faceSo.FindProperty("m_faceCamera").boolValue = true;
                faceSo.ApplyModifiedPropertiesWithoutUndo();

                root.transform.localScale = Vector3.one * 0.0016f; // 400px ≈ 0.64m

                PrefabUtility.SaveAsPrefabAsset(root, k_panelPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static TMP_FontAsset LoadKoreanFont()
        {
            var f = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Font/NotoSansKR SDF.asset");
            if (f != null) return f;
            return TMP_Settings.defaultFontAsset;
        }

        private static void SetRef(Component comp, string field, Object value)
        {
            var so = new SerializedObject(comp);
            var prop = so.FindProperty(field);
            if (prop == null)
                throw new System.InvalidOperationException($"[MakeAssets] 필드 없음: {comp.GetType().Name}.{field}");
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
