#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using CampLantern.Core;

namespace CampLantern.EditorTools
{
    /// <summary>
    /// P0 프로토타입 콘텐츠 데이터(.asset) 일괄 생성기 (step-03).
    /// RULE-01: [InitializeOnLoad] 사용 금지 — [MenuItem] 수동 실행만.
    /// RULE-02: .asset 텍스트 직접 작성 금지 — ScriptableObject.CreateInstance + AssetDatabase.CreateAsset만 사용.
    /// 재실행 안전: 이미 존재하는 에셋은 덮어쓰지 않고 스킵(로드만 해서 참조 연결에 사용).
    /// </summary>
    public static class P0DataFactory
    {
        private const string k_rootFolder = "Assets/Data";

        // 실행 결과 집계용
        private static int s_created;
        private static int s_skipped;

        [MenuItem("Tools/Make Assets/P0 Data (Create All)")]
        public static void CreateAll()
        {
            s_created = 0;
            s_skipped = 0;

            EnsureFolder(k_rootFolder);
            EnsureFolder(k_rootFolder + "/Items");
            EnsureFolder(k_rootFolder + "/Fish");
            EnsureFolder(k_rootFolder + "/Recipes");
            EnsureFolder(k_rootFolder + "/Estate");
            EnsureFolder(k_rootFolder + "/Hunt");

            // ── 1. 사냥 재료 ItemDef 2종 (HuntTargetDef 보상 / 희귀 EstateObject 재료) ──
            var hide = CreateOrLoad<ItemDef>("Assets/Data/Items/Item_DeerHide.asset", so =>
            {
                so.Id          = "item_deer_hide";
                so.DisplayName = "사슴 가죽";
                so.Rarity      = Rarity.Rare;
                so.SellPrice   = 25;
            });

            var antler = CreateOrLoad<ItemDef>("Assets/Data/Items/Item_DeerAntler.asset", so =>
            {
                so.Id          = "item_deer_antler";
                so.DisplayName = "사슴 뿔";
                so.Rarity      = Rarity.Rare;
                so.SellPrice   = 30;
            });

            // ── 2. FishDef 3종 (일반 2 + 희귀 1, 대기시간·판정창 차등) ──
            var crucian = CreateOrLoad<FishDef>("Assets/Data/Fish/Fish_Crucian.asset", so =>
            {
                so.Id                = "fish_crucian";
                so.DisplayName       = "붕어";
                so.Rarity            = Rarity.Common;
                so.SellPrice         = 10;           // economy.md 기준 스케일: 일반 어종 10코인
                so.BiteWindowSeconds = 1.2f;         // 판정 관대
                so.MinWaitSeconds    = 2f;
                so.MaxWaitSeconds    = 6f;
            });

            var trout = CreateOrLoad<FishDef>("Assets/Data/Fish/Fish_Trout.asset", so =>
            {
                so.Id                = "fish_trout";
                so.DisplayName       = "송어";
                so.Rarity            = Rarity.Common;
                so.SellPrice         = 12;
                so.BiteWindowSeconds = 0.9f;
                so.MinWaitSeconds    = 3f;
                so.MaxWaitSeconds    = 8f;
            });

            var goldenCarp = CreateOrLoad<FishDef>("Assets/Data/Fish/Fish_GoldenCarp.asset", so =>
            {
                so.Id                = "fish_golden_carp";
                so.DisplayName       = "황금 잉어";
                so.Rarity            = Rarity.Rare;
                so.SellPrice         = 40;
                so.BiteWindowSeconds = 0.5f;         // 희귀 — 판정창 짧음
                so.MinWaitSeconds    = 6f;
                so.MaxWaitSeconds    = 14f;          // 희귀 — 오래 기다림
            });

            // ── 3. 요리 결과물 ItemDef 4종 (완성 요리 3 + 실패작 1 공유) ──
            //    SellPrice는 재료 합의 1.5~2배 (economy.md)
            var grilledFish = CreateOrLoad<ItemDef>("Assets/Data/Items/Item_GrilledFish.asset", so =>
            {
                so.Id          = "item_grilled_fish";
                so.DisplayName = "생선구이";
                so.Rarity      = Rarity.Common;
                so.SellPrice   = 35;                 // 재료 합 20 (붕어x2) → x1.75
            });

            var troutSteak = CreateOrLoad<ItemDef>("Assets/Data/Items/Item_TroutSteak.asset", so =>
            {
                so.Id          = "item_trout_steak";
                so.DisplayName = "송어 스테이크";
                so.Rarity      = Rarity.Common;
                so.SellPrice   = 45;                 // 재료 합 24 (송어x2) → x1.88
            });

            var goldenBraise = CreateOrLoad<ItemDef>("Assets/Data/Items/Item_GoldenCarpBraise.asset", so =>
            {
                so.Id          = "item_golden_carp_braise";
                so.DisplayName = "황금 잉어 조림";
                so.Rarity      = Rarity.Rare;
                so.SellPrice   = 90;                 // 재료 합 50 (황금잉어+붕어) → x1.8
            });

            var burntFood = CreateOrLoad<ItemDef>("Assets/Data/Items/Item_BurntFood.asset", so =>
            {
                so.Id          = "item_burnt_food";
                so.DisplayName = "탄 요리";
                so.Rarity      = Rarity.Common;
                so.SellPrice   = 3;                  // 실패작 — 저가 판매용, 3레시피 공유
            });

            // ── 4. RecipeDef 3종 (재료는 낚시 풀 재사용, 실패작 공유) ──
            CreateOrLoad<RecipeDef>("Assets/Data/Recipes/Recipe_GrilledFish.asset", so =>
            {
                so.Id          = "recipe_grilled_fish";
                so.DisplayName = "생선구이";
                so.Ingredients = new ItemDef[] { crucian, crucian };   // 수량은 배열 중복으로 표현
                so.Result      = grilledFish;
                so.FailResult  = burntFood;
            });

            CreateOrLoad<RecipeDef>("Assets/Data/Recipes/Recipe_TroutSteak.asset", so =>
            {
                so.Id          = "recipe_trout_steak";
                so.DisplayName = "송어 스테이크";
                so.Ingredients = new ItemDef[] { trout, trout };
                so.Result      = troutSteak;
                so.FailResult  = burntFood;
            });

            CreateOrLoad<RecipeDef>("Assets/Data/Recipes/Recipe_GoldenCarpBraise.asset", so =>
            {
                so.Id          = "recipe_golden_carp_braise";
                so.DisplayName = "황금 잉어 조림";
                so.Ingredients = new ItemDef[] { goldenCarp, crucian };
                so.Result      = goldenBraise;
                so.FailResult  = burntFood;
            });

            // ── 5. EstateObjectDef 6종 (일반 5 + 희귀 1, 일반 오브젝트 100코인 스케일) ──
            CreateOrLoad<EstateObjectDef>("Assets/Data/Estate/Estate_Tent.asset", so =>
            {
                so.Id             = "estate_tent";
                so.DisplayName    = "텐트";
                so.Rarity         = Rarity.Common;
                so.CoinCost       = 100;
                so.CapacityWeight = 3;
            });

            CreateOrLoad<EstateObjectDef>("Assets/Data/Estate/Estate_Lantern.asset", so =>
            {
                so.Id             = "estate_lantern";
                so.DisplayName    = "랜턴";
                so.Rarity         = Rarity.Common;
                so.CoinCost       = 60;
                so.CapacityWeight = 1;
            });

            CreateOrLoad<EstateObjectDef>("Assets/Data/Estate/Estate_CampChair.asset", so =>
            {
                so.Id             = "estate_camp_chair";
                so.DisplayName    = "캠핑 의자";
                so.Rarity         = Rarity.Common;
                so.CoinCost       = 80;
                so.CapacityWeight = 1;
            });

            CreateOrLoad<EstateObjectDef>("Assets/Data/Estate/Estate_Planter.asset", so =>
            {
                so.Id             = "estate_planter";
                so.DisplayName    = "화분";
                so.Rarity         = Rarity.Common;
                so.CoinCost       = 50;
                so.CapacityWeight = 1;
            });

            CreateOrLoad<EstateObjectDef>("Assets/Data/Estate/Estate_Deck.asset", so =>
            {
                so.Id             = "estate_deck";
                so.DisplayName    = "데크";
                so.Rarity         = Rarity.Common;
                so.CoinCost       = 120;
                so.CapacityWeight = 2;
            });

            CreateOrLoad<EstateObjectDef>("Assets/Data/Estate/Estate_Campfire.asset", so =>
            {
                so.Id                    = "estate_campfire";
                so.DisplayName           = "화롯불";
                so.Rarity                = Rarity.Rare;
                so.CoinCost              = 200;
                so.RequiredMaterial      = hide;   // 희귀 — 사냥 재료 요구 (코인 대체 불가)
                so.RequiredMaterialCount = 2;
                so.CapacityWeight        = 2;
            });

            // ── 6. HuntTargetDef 1종 (대형 사냥감 — 2인 협동 필수) ──
            CreateOrLoad<HuntTargetDef>("Assets/Data/Hunt/Hunt_GreatElk.asset", so =>
            {
                so.Id                   = "hunt_great_elk";
                so.DisplayName          = "큰뿔사슴";
                so.MaxHealth            = 300;
                so.RequiredParticipants = 2;
                so.RewardMaterials      = new ItemDef[] { hide, antler };
            });

            // ── 7. 멧돼지 (솔로 사냥감) — 큰뿔사슴(2인 협동)과 대비되는 1인 사냥감 ──
            //    소형·솔로라 보상은 Common (economy: 협동 큰뿔사슴 Rare 재료보다 저가).
            //    협동 필수는 '대형'만이라는 원칙(social-cooperation.md ②)에 맞춰 RequiredParticipants=1.
            var boarHide = CreateOrLoad<ItemDef>("Assets/Data/Items/Item_BoarHide.asset", so =>
            {
                so.Id          = "item_boar_hide";
                so.DisplayName = "멧돼지 가죽";
                so.Rarity      = Rarity.Common;
                so.SellPrice   = 15;
            });

            var boarTusk = CreateOrLoad<ItemDef>("Assets/Data/Items/Item_BoarTusk.asset", so =>
            {
                so.Id          = "item_boar_tusk";
                so.DisplayName = "멧돼지 엄니";
                so.Rarity      = Rarity.Common;
                so.SellPrice   = 18;
            });

            CreateOrLoad<HuntTargetDef>("Assets/Data/Hunt/Hunt_WildBoar.asset", so =>
            {
                so.Id                   = "hunt_wild_boar";
                so.DisplayName          = "멧돼지";
                so.MaxHealth            = 120;
                so.RequiredParticipants = 1;
                so.RewardMaterials      = new ItemDef[] { boarHide, boarTusk };
            });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[MakeAssets] P0 데이터 생성 완료 — 생성 {s_created}개 / 스킵(기존) {s_skipped}개, 경로: {k_rootFolder}");
        }

        /// <summary>
        /// 에셋이 이미 있으면 로드해서 반환(스킵), 없으면 생성 후 configure 적용.
        /// 기존 에셋은 절대 덮어쓰지 않는다 (재실행 안전).
        /// </summary>
        private static T CreateOrLoad<T>(string path, System.Action<T> configure) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                s_skipped++;
                return existing;
            }

            var so = ScriptableObject.CreateInstance<T>();
            configure(so);
            AssetDatabase.CreateAsset(so, path);
            s_created++;
            return so;
        }

        /// <summary>Assets/... 경로의 폴더를 단계적으로 보장한다 (AssetDatabase API만 사용).</summary>
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            var parts   = path.Split('/');
            var current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
#endif
