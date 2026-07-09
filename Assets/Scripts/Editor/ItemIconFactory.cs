#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using CampLantern.Core;
using Project.Editor.ClaudeBridge;      // SpriteImportSvgArgs
using Project.Editor.ClaudeBridge.Ops;  // SvgOps
using UnityEditor;
using UnityEngine;

namespace CampLantern.EditorTools
{
    /// <summary>
    /// 아이템·영지오브젝트 2D 아이콘(Sprite) 생성 + 배선.
    ///
    /// Claude가 단순 도형 SVG를 직접 작성해 Unity Vector Graphics로 렌더→PNG→Sprite 임포트한다
    /// (SvgOps 재사용, 외부 래스터라이저 불필요). **3D 모델/사진 텍스처는 이 경로로 못 만든다 — 플랫 2D 아이콘만.**
    /// 아이콘은 Def.Id로 매칭해 ItemDef.Icon / EstateObjectDef.Icon에 배선한다(FishDef는 ItemDef 상속 → 포함).
    ///
    /// SVG 제약(VectorGraphics): path/rect/circle/ellipse/polygon + 단색 fill/stroke만. filter/mask/그라디언트/transform 회피.
    /// RULE-02: .asset/.png 직접 편집 없음 — 전부 API로 생성.
    /// </summary>
    public static class ItemIconFactory
    {
        private const string k_iconFolder = "Assets/Art/Icons";

        // Def.Id → SVG (viewBox 0 0 100 100). 아이템 11 + 영지 6 = 17.
        private static readonly Dictionary<string, string> k_svgById = new Dictionary<string, string>
        {
            // ── 어종 ──
            ["fish_crucian"] = "<svg viewBox='0 0 100 100'><ellipse cx='55' cy='50' rx='30' ry='18' fill='#7a9a6a'/><polygon points='26,50 8,34 8,66' fill='#7a9a6a'/><circle cx='72' cy='45' r='3' fill='#1c1c1c'/></svg>",
            ["fish_trout"] = "<svg viewBox='0 0 100 100'><ellipse cx='55' cy='50' rx='32' ry='16' fill='#9a7b4f'/><polygon points='24,50 6,35 6,65' fill='#9a7b4f'/><circle cx='73' cy='47' r='3' fill='#1c1c1c'/><circle cx='55' cy='45' r='2.5' fill='#5c4a2e'/><circle cx='45' cy='53' r='2.5' fill='#5c4a2e'/><circle cx='64' cy='53' r='2.5' fill='#5c4a2e'/></svg>",
            ["fish_golden_carp"] = "<svg viewBox='0 0 100 100'><ellipse cx='55' cy='50' rx='32' ry='18' fill='#e8b923'/><polygon points='24,50 5,33 5,67' fill='#d4a017'/><circle cx='73' cy='45' r='3.5' fill='#5c3d00'/></svg>",

            // ── 사냥 재료 ──
            ["item_deer_hide"] = "<svg viewBox='0 0 100 100'><path d='M32 26 Q18 42 26 62 Q32 82 50 78 Q76 84 78 60 Q86 40 68 28 Q50 18 32 26 Z' fill='#c9a56a'/></svg>",
            ["item_deer_antler"] = "<svg viewBox='0 0 100 100'><path d='M50 86 L50 40' stroke='#e6dcc2' stroke-width='6' fill='none' stroke-linecap='round'/><path d='M50 60 L34 46' stroke='#e6dcc2' stroke-width='6' fill='none' stroke-linecap='round'/><path d='M50 54 L66 42' stroke='#e6dcc2' stroke-width='6' fill='none' stroke-linecap='round'/><path d='M50 46 L40 32' stroke='#e6dcc2' stroke-width='6' fill='none' stroke-linecap='round'/><path d='M50 46 L60 32' stroke='#e6dcc2' stroke-width='6' fill='none' stroke-linecap='round'/></svg>",
            ["item_boar_hide"] = "<svg viewBox='0 0 100 100'><path d='M32 26 Q18 42 26 62 Q32 82 50 78 Q76 84 78 60 Q86 40 68 28 Q50 18 32 26 Z' fill='#6b4a32'/></svg>",
            ["item_boar_tusk"] = "<svg viewBox='0 0 100 100'><path d='M42 82 Q30 52 46 30' stroke='#efe6cf' stroke-width='9' fill='none' stroke-linecap='round'/><path d='M58 82 Q70 52 54 30' stroke='#efe6cf' stroke-width='9' fill='none' stroke-linecap='round'/></svg>",
            ["item_bear_hide"] = "<svg viewBox='0 0 100 100'><path d='M32 26 Q18 42 26 62 Q32 82 50 78 Q76 84 78 60 Q86 40 68 28 Q50 18 32 26 Z' fill='#4a3526'/></svg>",
            ["item_bear_claw"] = "<svg viewBox='0 0 100 100'><path d='M35 80 Q28 50 40 28' stroke='#2b2b2b' stroke-width='7' fill='none' stroke-linecap='round'/><path d='M50 82 Q45 50 52 26' stroke='#2b2b2b' stroke-width='7' fill='none' stroke-linecap='round'/><path d='M65 80 Q70 50 62 28' stroke='#2b2b2b' stroke-width='7' fill='none' stroke-linecap='round'/></svg>",

            // ── 요리 ──
            ["item_grilled_fish"] = "<svg viewBox='0 0 100 100'><circle cx='50' cy='55' r='38' fill='#eaeaea'/><ellipse cx='54' cy='55' rx='23' ry='12' fill='#b06a3a'/><polygon points='31,55 18,46 18,64' fill='#b06a3a'/></svg>",
            ["item_trout_steak"] = "<svg viewBox='0 0 100 100'><circle cx='50' cy='55' r='38' fill='#eaeaea'/><rect x='33' y='43' width='34' height='24' rx='10' fill='#a8542f'/><rect x='41' y='50' width='18' height='9' rx='4' fill='#c47a55'/></svg>",
            ["item_golden_carp_braise"] = "<svg viewBox='0 0 100 100'><path d='M18 52 Q50 88 82 52 Z' fill='#c9822a'/><ellipse cx='50' cy='52' rx='33' ry='9' fill='#e8b923'/></svg>",
            ["item_burnt_food"] = "<svg viewBox='0 0 100 100'><circle cx='50' cy='55' r='38' fill='#eaeaea'/><ellipse cx='50' cy='55' rx='23' ry='13' fill='#2b2b2b'/><circle cx='43' cy='51' r='3.5' fill='#555555'/><circle cx='57' cy='57' r='3' fill='#555555'/></svg>",

            // ── 영지 오브젝트 ──
            ["estate_tent"] = "<svg viewBox='0 0 100 100'><polygon points='50,20 86,80 14,80' fill='#c98a4a'/><polygon points='50,20 60,80 40,80' fill='#8a5a2a'/></svg>",
            ["estate_lantern"] = "<svg viewBox='0 0 100 100'><rect x='38' y='30' width='24' height='40' rx='6' fill='#c9a53a'/><circle cx='50' cy='50' r='9' fill='#fff3b0'/><rect x='44' y='22' width='12' height='9' rx='3' fill='#7a6320'/><rect x='40' y='68' width='20' height='6' rx='2' fill='#7a6320'/></svg>",
            ["estate_camp_chair"] = "<svg viewBox='0 0 100 100'><rect x='32' y='54' width='36' height='8' fill='#6a8a5a'/><rect x='32' y='30' width='8' height='28' fill='#6a8a5a'/><path d='M36 62 L28 84' stroke='#46583a' stroke-width='5' fill='none' stroke-linecap='round'/><path d='M64 62 L72 84' stroke='#46583a' stroke-width='5' fill='none' stroke-linecap='round'/></svg>",
            ["estate_planter"] = "<svg viewBox='0 0 100 100'><path d='M35 60 L65 60 L60 82 L40 82 Z' fill='#b5651d'/><circle cx='42' cy='46' r='10' fill='#4a8a3a'/><circle cx='58' cy='46' r='10' fill='#4a8a3a'/><circle cx='50' cy='38' r='11' fill='#5aa04a'/></svg>",
            ["estate_deck"] = "<svg viewBox='0 0 100 100'><rect x='16' y='40' width='68' height='30' fill='#9a6a3a'/><path d='M16 50 L84 50' stroke='#6a4a25' stroke-width='2'/><path d='M16 60 L84 60' stroke='#6a4a25' stroke-width='2'/><path d='M38 40 L38 70' stroke='#6a4a25' stroke-width='2'/><path d='M60 40 L60 70' stroke='#6a4a25' stroke-width='2'/></svg>",
            ["estate_campfire"] = "<svg viewBox='0 0 100 100'><polygon points='24,72 70,64 72,73 26,81' fill='#7a4a25'/><polygon points='30,64 76,72 74,81 28,73' fill='#8a5a2f'/><path d='M50 24 Q64 44 50 66 Q36 44 50 24 Z' fill='#e8641a'/><path d='M50 38 Q58 50 50 64 Q42 50 50 38 Z' fill='#ffd23a'/></svg>",
        };

        [MenuItem("Tools/Make Assets/Item Icons (Generate + Wire)")]
        public static void GenerateAndWire()
        {
            EnsureFolder(k_iconFolder);

            // 1) 모든 아이콘 렌더 → PNG → Sprite 임포트 (SvgOps 재사용)
            int made = 0;
            foreach (KeyValuePair<string, string> kv in k_svgById)
            {
                var args = new SpriteImportSvgArgs
                {
                    svgText       = kv.Value,
                    pngPath       = $"{k_iconFolder}/Icon_{kv.Key}.png",
                    width         = 128,
                    height        = 128,
                    pixelsPerUnit = 100f,
                    filterMode    = "Bilinear",
                    compression   = "None",
                    antiAliasing  = 4,
                    saveSvgSource = true, // 재편집용 .svg 원본도 함께 저장
                };
                SvgOps.ImportFromSvg(JsonUtility.ToJson(args));
                made++;
            }
            AssetDatabase.Refresh();

            // 2) Def.Id로 아이콘 매칭 배선 (ItemDef+FishDef, EstateObjectDef)
            int wired = 0;
            foreach (ItemDef def in LoadAll<ItemDef>())
                if (TryWireIcon(def, def.Id, s => def.Icon = s)) wired++;
            foreach (EstateObjectDef def in LoadAll<EstateObjectDef>())
                if (TryWireIcon(def, def.Id, s => def.Icon = s)) wired++;

            AssetDatabase.SaveAssets();
            Debug.Log($"[MakeAssets] 아이템 아이콘 생성 {made}개 / 배선 {wired}개 — {k_iconFolder}");
        }

        private static bool TryWireIcon(Object def, string id, System.Action<Sprite> setter)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{k_iconFolder}/Icon_{id}.png");
            if (sprite == null) return false;
            setter(sprite);
            EditorUtility.SetDirty(def);
            return true;
        }

        private static IEnumerable<T> LoadAll<T>() where T : Object =>
            AssetDatabase.FindAssets($"t:{typeof(T).Name}")
                .Select(g => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(x => x != null);

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
