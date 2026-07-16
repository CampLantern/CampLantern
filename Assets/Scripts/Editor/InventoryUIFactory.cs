#if UNITY_EDITOR
using CampLantern.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CampLantern.EditorTools
{
    /// <summary>
    /// VR 월드스페이스 인벤토리 UI 프리팹 생성 — 만든 아이템 아이콘(ItemDef.Icon)이 실제로 보이는 UI.
    ///   - InventoryRow.prefab  : 아이콘 Image + "이름 x수량" TMP 라벨
    ///   - InventoryPanel.prefab: 월드스페이스 Canvas + 배경 + 제목 + VerticalLayoutGroup 콘텐츠 + InventoryPanel/VRUIPanel
    /// 소유자가 InventoryPanel.Bind(inventory)로 데이터 주입 → Inventory.Changed에 자동 갱신.
    /// RULE-02: .prefab 직접 작성 금지 — PrefabUtility.SaveAsPrefabAsset만 사용.
    /// </summary>
    public static class InventoryUIFactory
    {
        private const string k_folder   = "Assets/Prefabs/UI";
        private const string k_rowPath  = k_folder + "/InventoryRow.prefab";
        // 패널은 Resources에 둬 씬 배선 없이 Resources.Load로 어디서든 띄운다(ContentRegistry/PersistentPlayer와 동일 패턴).
        private const string k_panelPath = "Assets/Resources/InventoryPanel.prefab";

        [MenuItem("Tools/Make Assets/Inventory UI")]
        public static void CreateAll()
        {
            EnsureFolder(k_folder);
            EnsureFolder("Assets/Resources");
            TMP_FontAsset font = LoadFont();

            GameObject rowPrefab = CreateRow(font);
            CreatePanel(font, rowPrefab.GetComponent<InventoryRow>());

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[MakeAssets] 인벤토리 UI 생성 완료 — {k_rowPath}, {k_panelPath}");
        }

        private static GameObject CreateRow(TMP_FontAsset font)
        {
            var root = new GameObject("InventoryRow", typeof(RectTransform));
            try
            {
                var rt = (RectTransform)root.transform;
                rt.sizeDelta = new Vector2(360f, 72f);
                VRUISkin.TryAddRowBackground(root); // 탭 바 배경 (스킨 없으면 무배경)

                // 아이콘 (좌)
                var iconGo = UIChild("Icon", root.transform, new Vector2(64f, 64f), new Vector2(-140f, 0f));
                var icon = iconGo.AddComponent<Image>();
                icon.preserveAspect = true;
                icon.raycastTarget  = false;

                // 라벨 (우)
                var labelGo = UIChild("Label", root.transform, new Vector2(260f, 64f), new Vector2(40f, 0f));
                var label = labelGo.AddComponent<TextMeshProUGUI>();
                label.text          = "아이템  x1";
                label.fontSize      = 28f;
                label.alignment     = TextAlignmentOptions.MidlineLeft;
                label.color         = VRUISkin.RowText; // 시안 행 바 위 진남색
                label.raycastTarget = false;
                if (font != null) label.font = font;

                var comp = root.AddComponent<InventoryRow>();
                SetRef(comp, "m_icon", icon);
                SetRef(comp, "m_label", label);

                return PrefabUtility.SaveAsPrefabAsset(root, k_rowPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void CreatePanel(TMP_FontAsset font, InventoryRow rowPrefabComp)
        {
            var root = new GameObject("InventoryPanel", typeof(RectTransform));
            try
            {
                var prt = (RectTransform)root.transform;
                prt.sizeDelta = new Vector2(400f, 520f);

                var canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                root.AddComponent<CanvasScaler>();

                var bg = root.AddComponent<Image>();
                bg.color = new Color(0.12f, 0.12f, 0.16f, 0.92f);
                VRUISkin.TryApplyPanel(bg); // Colorful_UI 스킨 (없으면 위 단색 유지)
                bg.raycastTarget = false;

                // 헤더 필 (파랑 — UI 차별화: 소셜=보라, 액션=초록) + 제목. 스킨 없으면 기존 상단 텍스트만.
                if (VRUISkin.HasSkin)
                {
                    var headerGo = UIChild("Header", root.transform, new Vector2(280f, 76f), new Vector2(0f, 4f));
                    var headerRt = (RectTransform)headerGo.transform;
                    headerRt.anchorMin = new Vector2(0.5f, 1f);
                    headerRt.anchorMax = new Vector2(0.5f, 1f);
                    headerRt.pivot     = new Vector2(0.5f, 0.5f);
                    var headerImg = headerGo.AddComponent<Image>();
                    headerImg.raycastTarget = false;
                    VRUISkin.TryApplyPill(headerImg, VRUISkin.PillColor.Blue);
                }

                var titleGo = UIChild("Title", root.transform, new Vector2(280f, 76f),
                                      VRUISkin.HasSkin ? new Vector2(0f, 4f) : new Vector2(0f, -38f));
                var titleRt = (RectTransform)titleGo.transform;
                titleRt.anchorMin = new Vector2(0.5f, 1f);
                titleRt.anchorMax = new Vector2(0.5f, 1f);
                titleRt.pivot     = new Vector2(0.5f, 0.5f);
                var title = titleGo.AddComponent<TextMeshProUGUI>();
                title.text          = "인벤토리";
                title.fontSize      = 32f;
                title.alignment     = TextAlignmentOptions.Center;
                title.color         = VRUISkin.HasSkin ? VRUISkin.PillText(VRUISkin.PillColor.Blue) : Color.white;
                title.raycastTarget = false;
                if (font != null) title.font = font;

                // 콘텐츠 (제목 아래, 세로 레이아웃)
                var contentGo = UIChild("Content", root.transform, new Vector2(-24f, 0f), new Vector2(0f, -76f));
                var crt = (RectTransform)contentGo.transform;
                crt.anchorMin = new Vector2(0f, 1f);
                crt.anchorMax = new Vector2(1f, 1f);
                crt.pivot     = new Vector2(0.5f, 1f);
                crt.sizeDelta = new Vector2(-24f, 0f);
                // 동적 콘텐츠(수량 변동)라 레이아웃 그룹 유지 (make-assets 예외 조항)
                var vlg = contentGo.AddComponent<VerticalLayoutGroup>();
                vlg.spacing = 6f;
                vlg.childAlignment = TextAnchor.UpperCenter;
                vlg.childControlHeight = false; vlg.childControlWidth = false;
                vlg.childForceExpandHeight = false; vlg.childForceExpandWidth = false;
                var csf = contentGo.AddComponent<ContentSizeFitter>();
                csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                var vrui = root.AddComponent<VRUIPanel>();
                SetRef(vrui, "m_canvas", canvas);
                SetRef(vrui, "m_content", crt);
                SetRef(vrui, "m_title", title);
                // 항상 플레이어(중앙 눈)를 향하게 — 월드에 고정 배치해도 정면이 보이도록.
                var faceSo = new SerializedObject(vrui);
                faceSo.FindProperty("m_faceCamera").boolValue = true;
                faceSo.ApplyModifiedPropertiesWithoutUndo();

                var invp = root.AddComponent<InventoryPanel>();
                SetRef(invp, "m_content", crt);
                SetRef(invp, "m_rowPrefab", rowPrefabComp);

                root.transform.localScale = Vector3.one * 0.0016f; // 400px ≈ 0.64m

                PrefabUtility.SaveAsPrefabAsset(root, k_panelPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        // ── 헬퍼 ─────────────────────────────────────────────────────

        private static GameObject UIChild(string name, Transform parent, Vector2 size, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta       = size;
            rt.anchoredPosition = pos;
            return go;
        }

        private static TMP_FontAsset LoadFont()
        {
            var f = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            return f != null ? f : TMP_Settings.defaultFontAsset;
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

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parts = path.Split('/');
            var cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }
    }
}
#endif
