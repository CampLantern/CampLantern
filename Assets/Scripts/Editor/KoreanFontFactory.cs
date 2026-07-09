#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace CampLantern.EditorTools
{
    /// <summary>
    /// 한글 TMP 폰트 배선 — NotoSansKR TTF로 TMP 동적 폰트 에셋을 만들고 TMP 전역 폴백에 등록한다.
    ///
    /// 배경: 기본 폰트(LiberationSans SDF)에 한글 글리프가 없어 모든 한글 TMP 텍스트가 두부(□)로 렌더됐다.
    /// 전역 폴백에 한글 폰트를 넣으면 — 기존 UI 프리팹을 재생성하지 않아도 — 런타임 렌더 시 없는 글리프가
    /// 폴백으로 해결돼 한글이 정상 표시된다(폴백은 baked가 아니라 render-time 해결).
    ///
    /// 한글은 11,172 음절이라 미리 SDF 아틀라스로 굽지 않고 **동적(Dynamic)** 모드 — 런타임에 쓰는 글리프만 아틀라스에 추가.
    /// RULE-02: .asset 직접 편집 없음 — TMP API + SerializedObject로만.
    /// </summary>
    public static class KoreanFontFactory
    {
        private const string k_ttf   = "Assets/Font/NotoSansKR-VariableFont_wght.ttf";
        private const string k_asset = "Assets/Font/NotoSansKR SDF.asset";

        [MenuItem("Tools/Make Assets/Korean Font (TMP + Fallback)")]
        public static void CreateAndRegister()
        {
            TMP_FontAsset fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(k_asset);
            if (fa == null)
            {
                var source = AssetDatabase.LoadAssetAtPath<Font>(k_ttf);
                if (source == null)
                {
                    Debug.LogError($"[MakeAssets] 폰트 TTF 없음/미임포트: {k_ttf}");
                    return;
                }

                // 동적 아틀라스 — 한글 전체를 미리 굽지 않고 런타임에 필요한 글리프만 추가.
                fa = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
                    AtlasPopulationMode.Dynamic, enableMultiAtlasSupport: true);
                if (fa == null)
                {
                    Debug.LogError("[MakeAssets] TMP 폰트 에셋 생성 실패 (가변 폰트 임포트 확인)");
                    return;
                }
                fa.name = "NotoSansKR SDF";

                AssetDatabase.CreateAsset(fa, k_asset);
                // 아틀라스 텍스처 + 머티리얼을 서브에셋으로 영속화
                if (fa.atlasTextures != null && fa.atlasTextures.Length > 0 && fa.atlasTextures[0] != null)
                {
                    fa.atlasTextures[0].name = "NotoSansKR Atlas";
                    AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
                }
                if (fa.material != null)
                {
                    fa.material.name = "NotoSansKR SDF Material";
                    AssetDatabase.AddObjectToAsset(fa.material, fa);
                }
                EditorUtility.SetDirty(fa);
                AssetDatabase.SaveAssets();
                Debug.Log($"[MakeAssets] 한글 TMP 폰트 에셋 생성: {k_asset}");
            }

            RegisterGlobalFallback(fa);
        }

        // TMP 전역 폴백 목록(TMP_Settings.m_fallbackFontAssets)에 한글 폰트를 추가한다.
        private static void RegisterGlobalFallback(TMP_FontAsset fa)
        {
            var settings = TMP_Settings.instance;
            if (settings == null)
            {
                Debug.LogError("[MakeAssets] TMP_Settings 없음 — TMP Essentials 임포트 확인");
                return;
            }

            var so = new SerializedObject(settings);
            var list = so.FindProperty("m_fallbackFontAssets");
            if (list == null)
            {
                Debug.LogError("[MakeAssets] TMP_Settings.m_fallbackFontAssets 필드 없음 — TMP 버전 확인");
                return;
            }

            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == fa)
                {
                    Debug.Log("[MakeAssets] 한글 폰트가 이미 전역 폴백에 등록됨 — 변경 없음");
                    return;
                }

            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = fa;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log("[MakeAssets] 한글 폰트를 TMP 전역 폴백에 등록 완료 — 모든 한글 TMP 텍스트가 렌더됨");
        }
    }
}
#endif
