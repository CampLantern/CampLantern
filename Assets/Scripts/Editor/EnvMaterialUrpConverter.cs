#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CampLantern.EditorTools
{
    /// <summary>
    /// FantasyEnvironments 머티리얼 URP 변환기 — 팩이 빌트인 셰이더(Standard,
    /// Nature/Tree Soft Occlusion, Legacy Particles)로 제작되어 URP에선 마젠타로 깨진다.
    /// Unity API로 셰이더 교체·프로퍼티 리매핑만 수행 (RULE-02: 에셋 파일 직접 편집 없음).
    /// idempotent: 이미 URP 셰이더인 머티리얼은 건너뛴다.
    /// CFXR(카툰 FX)는 자체 ScriptedImporter가 URP를 감지해 변환하므로 대상 아님.
    /// </summary>
    public static class EnvMaterialUrpConverter
    {
        private const string k_envRoot = "Assets/FantasyEnvironments";

        [MenuItem("Tools/Make Assets/Convert Env Materials To URP")]
        public static void Convert()
        {
            Shader urpLit      = Shader.Find("Universal Render Pipeline/Lit");
            Shader urpParticle = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (urpLit == null)
            {
                Debug.LogError("[MakeAssets] URP Lit 셰이더 없음 — URP 프로젝트인지 확인");
                return;
            }
            if (!AssetDatabase.IsValidFolder(k_envRoot))
            {
                Debug.LogError($"[MakeAssets] {k_envRoot} 폴더 없음 — FantasyEnvironments 임포트 먼저");
                return;
            }

            int converted = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { k_envRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null || mat.shader == null) continue;

                string shaderName = mat.shader.name;
                if (shaderName.StartsWith("Universal Render Pipeline/")) continue; // 이미 변환됨

                bool isParticle = shaderName.Contains("Particles");
                bool isBuiltin  = shaderName == "Standard" || shaderName == "Standard (Specular setup)"
                                  || shaderName.StartsWith("Nature/") || shaderName.StartsWith("Legacy Shaders/")
                                  || shaderName == "Hidden/InternalErrorShader";
                if (!isParticle && !isBuiltin) continue;

                if (isParticle && urpParticle != null) ConvertParticle(mat, urpParticle);
                else ConvertLit(mat, urpLit, shaderName);

                EditorUtility.SetDirty(mat);
                converted++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[MakeAssets] URP 머티리얼 변환 완료: {converted}개");
        }

        private static void ConvertLit(Material mat, Shader urpLit, string oldShaderName)
        {
            // 교체 전에 빌트인 프로퍼티 캡처
            Texture baseMap  = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
            Vector2 texScale = mat.HasProperty("_MainTex") ? mat.GetTextureScale("_MainTex") : Vector2.one;
            Texture bumpMap  = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;
            Color   color    = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
            float   cutoff   = mat.HasProperty("_Cutoff") ? mat.GetFloat("_Cutoff") : 0.5f;
            float   gloss    = mat.HasProperty("_Glossiness") ? mat.GetFloat("_Glossiness") : 0.3f;
            float   mode     = mat.HasProperty("_Mode") ? mat.GetFloat("_Mode") : 0f;

            // 잎/컷아웃 판정 — Nature Leaves 셰이더, Standard Cutout 모드, 이름 휴리스틱
            string lower = mat.name.ToLowerInvariant();
            bool alphaClip = oldShaderName.Contains("Leaves") || Mathf.Approximately(mode, 1f)
                             || lower.Contains("leaves") || lower.Contains("leaf")
                             || lower.Contains("plant") || lower.Contains("grass") || lower.Contains("fern");

            mat.shader = urpLit;
            mat.SetTexture("_BaseMap", baseMap);
            mat.SetTextureScale("_BaseMap", texScale);
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", Mathf.Min(gloss, 0.35f)); // 자연물 — 과광택 방지
            mat.SetFloat("_Metallic", 0f);

            if (bumpMap != null)
            {
                mat.SetTexture("_BumpMap", bumpMap);
                mat.EnableKeyword("_NORMALMAP");
            }

            if (alphaClip)
            {
                mat.SetFloat("_AlphaClip", 1f);
                mat.SetFloat("_Cutoff", cutoff);
                mat.SetFloat("_Cull", (float)CullMode.Off); // 잎 뒷면도 렌더
                mat.EnableKeyword("_ALPHATEST_ON");
                mat.renderQueue = (int)RenderQueue.AlphaTest;
            }
        }

        private static void ConvertParticle(Material mat, Shader urpParticle)
        {
            Texture baseMap = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
            Color color = mat.HasProperty("_TintColor") ? mat.GetColor("_TintColor")
                        : mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;

            mat.shader = urpParticle;
            mat.SetTexture("_BaseMap", baseMap);
            mat.SetColor("_BaseColor", color);
            // 가산 합성 (촛불/글로우류 레거시 파티클 전제)
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 2f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.One);
            mat.SetFloat("_ZWrite", 0f);
            mat.renderQueue = (int)RenderQueue.Transparent;
        }
    }
}
#endif
