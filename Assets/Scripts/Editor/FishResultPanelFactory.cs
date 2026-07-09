#if UNITY_EDITOR
using CampLantern.Fishing;
using CampLantern.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CampLantern.EditorTools
{
    /// <summary>
    /// 낚시 획득 결과 패널 프리팹 생성 (§2-5) — 월드스페이스 Canvas + 제목/본문 TMP + 확인 VRUIButton.
    /// Resources 소재라 씬 배선 없이 Resources.Load로 사용(InventoryPanel과 동일 패턴).
    /// RULE-02: .prefab 직접 작성 금지 — PrefabUtility.SaveAsPrefabAsset만 사용.
    /// </summary>
    public static class FishResultPanelFactory
    {
        private const string k_path = "Assets/Resources/FishResultPanel.prefab";

        [MenuItem("Tools/Make Assets/Fish Result Panel")]
        public static void Create()
        {
            TMP_FontAsset font = LoadFont();

            var root = new GameObject("FishResultPanel", typeof(RectTransform));
            try
            {
                var prt = (RectTransform)root.transform;
                prt.sizeDelta = new Vector2(380f, 260f);

                var canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                root.AddComponent<CanvasScaler>();
                root.AddComponent<GraphicRaycaster>(); // 확인 버튼 클릭용

                var bg = root.AddComponent<Image>();
                bg.color = new Color(0.12f, 0.12f, 0.16f, 0.94f);
                bg.raycastTarget = false;

                var title = MakeText("Title", root.transform, font, 34f, new Vector2(0f, 92f), new Vector2(340f, 50f));
                title.text = "획득!";

                var body = MakeText("Body", root.transform, font, 26f, new Vector2(0f, 8f), new Vector2(340f, 120f));
                body.text = "어종\n길이 · 무게\n보상";

                // 확인 버튼 (UGUI Button + VRUIButton 래퍼)
                var buttonGo = new GameObject("ConfirmButton", typeof(RectTransform));
                buttonGo.transform.SetParent(root.transform, false);
                var brt = (RectTransform)buttonGo.transform;
                brt.sizeDelta = new Vector2(180f, 56f);
                brt.anchoredPosition = new Vector2(0f, -84f);
                var bImg = buttonGo.AddComponent<Image>();
                bImg.color = new Color(0.25f, 0.45f, 0.75f, 1f);
                buttonGo.AddComponent<Button>();
                var vruiButton = buttonGo.AddComponent<VRUIButton>();
                var bLabel = MakeText("Label", buttonGo.transform, font, 26f, Vector2.zero, new Vector2(170f, 46f));
                bLabel.text = "확인";

                var panel = root.AddComponent<FishResultPanel>();
                SetRef(panel, "m_title", title);
                SetRef(panel, "m_body", body);
                SetRef(panel, "m_confirm", vruiButton);

                // 항상 플레이어를 향하게 — 고정 배치 시 뒷면(거울상)이 보이는 문제 방지 (InventoryPanel과 동일 패턴)
                var vrui = root.AddComponent<VRUIPanel>();
                SetRef(vrui, "m_canvas", canvas);
                SetRef(vrui, "m_title", title);
                var faceSo = new SerializedObject(vrui);
                faceSo.FindProperty("m_faceCamera").boolValue = true;
                faceSo.ApplyModifiedPropertiesWithoutUndo();

                root.transform.localScale = Vector3.one * 0.0016f;

                PrefabUtility.SaveAsPrefabAsset(root, k_path);
                Debug.Log($"[MakeAssets] 낚시 결과 패널 생성: {k_path}");
            }
            finally { Object.DestroyImmediate(root); }
            AssetDatabase.Refresh();
        }

        private static TextMeshProUGUI MakeText(string name, Transform parent, TMP_FontAsset font,
                                                float size, Vector2 pos, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = sizeDelta;
            rt.anchoredPosition = pos;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = size;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            if (font != null) tmp.font = font;
            return tmp;
        }

        private static TMP_FontAsset LoadFont()
        {
            var f = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            return f != null ? f : TMP_Settings.defaultFontAsset; // 한글은 전역 폴백(NotoSansKR)이 해석
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
