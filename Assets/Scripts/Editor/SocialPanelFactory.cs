#if UNITY_EDITOR
using CampLantern.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CampLantern.EditorTools
{
    /// <summary>
    /// 세션·음소거 소셜 패널 프리팹 생성 (P0 판정 항목: 음소거 토글의 실사용 VR UI).
    ///   - SocialPlayerRow.prefab : "P{id}" 라벨 + 음소거 VRUIButton(중첩)
    ///   - SocialPanel.prefab     : VRUIPanel 베이스(레이/포크 배선 승계) + 상태 라벨 + 마이크 토글 +
    ///                              참가자 행 컨테이너. Resources 소재 — 하네스가 Resources.Load로 띄운다
    ///                              (InventoryPanel과 동일 패턴).
    /// RULE-02: .prefab 직접 작성 금지 — PrefabUtility API만 사용.
    /// </summary>
    public static class SocialPanelFactory
    {
        private const string k_rowPath        = "Assets/Prefabs/UI/SocialPlayerRow.prefab";
        private const string k_panelPath      = "Assets/Resources/SocialPanel.prefab";
        private const string k_basePanelPath  = "Assets/Prefabs/UI/VRUIPanel.prefab";
        private const string k_baseButtonPath = "Assets/Prefabs/UI/VRUIButton.prefab";

        [MenuItem("Tools/Make Assets/Social Panel UI")]
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
            CreatePanel(basePanel, baseButton, rowPrefab.GetComponent<SocialPlayerRow>(), font);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[MakeAssets] 소셜 패널 UI 생성 완료 — {k_rowPath}, {k_panelPath}");
        }

        // ── 참가자 행 ────────────────────────────────────────────────

        private static GameObject CreateRow(GameObject baseButton, TMP_FontAsset font)
        {
            var root = new GameObject("SocialPlayerRow", typeof(RectTransform));
            try
            {
                ((RectTransform)root.transform).sizeDelta = new Vector2(340f, 60f);

                // 라벨 (좌)
                var labelGo = new GameObject("Label", typeof(RectTransform));
                labelGo.transform.SetParent(root.transform, false);
                var lrt = (RectTransform)labelGo.transform;
                lrt.sizeDelta = new Vector2(180f, 52f);
                lrt.anchoredPosition = new Vector2(-75f, 0f);
                var label = labelGo.AddComponent<TextMeshProUGUI>();
                label.text          = "P0";
                label.fontSize      = 26f;
                label.alignment     = TextAlignmentOptions.MidlineLeft;
                label.color         = Color.white;
                label.raycastTarget = false;
                label.richText      = false;
                if (font != null) label.font = font;

                // 음소거 버튼 (우) — VRUIButton 중첩(원본 링크 유지)
                var btnGo = (GameObject)PrefabUtility.InstantiatePrefab(baseButton);
                btnGo.transform.SetParent(root.transform, false);
                var brt = (RectTransform)btnGo.transform;
                brt.sizeDelta = new Vector2(130f, 52f);
                brt.anchoredPosition = new Vector2(95f, 0f);
                PrefabUtility.RecordPrefabInstancePropertyModifications(brt);
                var btnLabel = btnGo.GetComponentInChildren<TextMeshProUGUI>(true);
                if (btnLabel != null)
                {
                    btnLabel.text = "음소거";
                    btnLabel.fontSize = 22f;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(btnLabel);
                }

                var row = root.AddComponent<SocialPlayerRow>();
                SetRef(row, "m_label", label);
                SetRef(row, "m_muteButton", btnGo.GetComponent<VRUIButton>());

                return PrefabUtility.SaveAsPrefabAsset(root, k_rowPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        // ── 패널 ─────────────────────────────────────────────────────

        private static void CreatePanel(GameObject basePanel, GameObject baseButton,
                                        SocialPlayerRow rowPrefabComp, TMP_FontAsset font)
        {
            // VRUIPanel 베이스 언팩 — 레이/포크 인터랙션 자식·GraphicRaycaster를 그대로 승계한다
            var root = (GameObject)PrefabUtility.InstantiatePrefab(basePanel);
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            try
            {
                root.name = "SocialPanel";

                var vrui = root.GetComponent<VRUIPanel>();
                RectTransform content = vrui.Content;

                // 샘플 버튼 제거
                for (int i = content.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(content.GetChild(i).gameObject);

                // 제목 굽기
                var title = root.GetComponentInChildren<TextMeshProUGUI>(true);
                if (title != null) title.text = "소셜";

                // 상태 라벨 (콘텐츠 상단)
                var statusGo = new GameObject("Status", typeof(RectTransform));
                statusGo.transform.SetParent(content, false);
                var srt = (RectTransform)statusGo.transform;
                srt.anchorMin = new Vector2(0.5f, 1f);
                srt.anchorMax = new Vector2(0.5f, 1f);
                srt.pivot     = new Vector2(0.5f, 1f);
                srt.sizeDelta = new Vector2(340f, 44f);
                srt.anchoredPosition = new Vector2(0f, -2f);
                var status = statusGo.AddComponent<TextMeshProUGUI>();
                status.text          = "미접속";
                status.fontSize      = 24f;
                status.alignment     = TextAlignmentOptions.Center;
                status.color         = new Color(0.85f, 0.9f, 0.95f, 1f);
                status.raycastTarget = false;
                status.richText      = false;
                if (font != null) status.font = font;

                // 마이크 토글 버튼
                var micGo = (GameObject)PrefabUtility.InstantiatePrefab(baseButton);
                PrefabUtility.UnpackPrefabInstance(micGo, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                micGo.transform.SetParent(content, false);
                var mrt = (RectTransform)micGo.transform;
                mrt.anchorMin = new Vector2(0.5f, 1f);
                mrt.anchorMax = new Vector2(0.5f, 1f);
                mrt.pivot     = new Vector2(0.5f, 1f);
                mrt.anchoredPosition = new Vector2(0f, -54f);
                var micLabel = micGo.GetComponentInChildren<TextMeshProUGUI>(true);
                if (micLabel != null) micLabel.text = "마이크 끄기";

                // 참가자 행 컨테이너 — 동적 콘텐츠(조인/이탈)라 레이아웃 그룹 유지 (make-assets 예외 조항)
                var rowsGo = new GameObject("Rows", typeof(RectTransform));
                rowsGo.transform.SetParent(content, false);
                var rrt = (RectTransform)rowsGo.transform;
                rrt.anchorMin = new Vector2(0f, 1f);
                rrt.anchorMax = new Vector2(1f, 1f);
                rrt.pivot     = new Vector2(0.5f, 1f);
                rrt.sizeDelta = new Vector2(-12f, 0f);
                rrt.anchoredPosition = new Vector2(0f, -136f);
                var vlg = rowsGo.AddComponent<VerticalLayoutGroup>();
                vlg.spacing = 6f;
                vlg.childAlignment = TextAnchor.UpperCenter;
                vlg.childControlHeight = false; vlg.childControlWidth = false;
                vlg.childForceExpandHeight = false; vlg.childForceExpandWidth = false;
                var csf = rowsGo.AddComponent<ContentSizeFitter>();
                csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                // 컴포넌트 배선
                var social = root.AddComponent<SocialPanel>();
                SetRef(social, "m_statusLabel", status);
                SetRef(social, "m_micButton", micGo.GetComponent<VRUIButton>());
                SetRef(social, "m_rowsContent", rrt);
                SetRef(social, "m_rowPrefab", rowPrefabComp);

                // 월드 배치형 — 항상 플레이어를 향하게 (InventoryPanel과 동일)
                var faceSo = new SerializedObject(vrui);
                faceSo.FindProperty("m_faceCamera").boolValue = true;
                faceSo.ApplyModifiedPropertiesWithoutUndo();

                root.transform.localScale = Vector3.one * 0.0016f; // 400px ≈ 0.64m

                PrefabUtility.SaveAsPrefabAsset(root, k_panelPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        // ── 헬퍼 ─────────────────────────────────────────────────────

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
