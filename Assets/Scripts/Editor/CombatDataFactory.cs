#if UNITY_EDITOR
using System.Collections.Generic;
using CampLantern.Combat;
using CampLantern.Combat.Data;
using UnityEditor;
using UnityEngine;

namespace CampLantern.EditorTools
{
    /// <summary>
    /// 전투 테스트 데이터(.asset) 일괄 생성기 (design/combat-detailed step-01).
    /// 모든 수치는 임시값 — 튜닝은 코드가 아니라 에셋에서만 (지시서 규칙 4). TODO(TUNING).
    /// RULE-01: [InitializeOnLoad] 사용 금지 — [MenuItem] 수동 실행만.
    /// RULE-02: .asset 텍스트 직접 작성 금지 — CreateInstance + AssetDatabase.CreateAsset만 (P0DataFactory 패턴).
    /// 재실행 안전: 이미 존재하는 에셋은 덮어쓰지 않고 스킵.
    /// Id/ContentRegistry 등록 없음 — 전투 데이터는 저장 파일에서 참조되지 않음 (설계 결정).
    /// </summary>
    public static class CombatDataFactory
    {
        private const string k_folder = "Assets/Data/Combat";

        private static int s_created;
        private static int s_skipped;

        [MenuItem("Tools/Make Assets/Combat Data (Create All)")]
        public static void CreateAll()
        {
            s_created = 0;
            s_skipped = 0;

            EnsureFolder(k_folder);

            // ── 1. 테스트 몬스터 — 근거리 Melee Skill1 하나만 보유 (지시서 1단계 명시) ──
            CreateOrLoad<MonsterData>(k_folder + "/Monster_Bear_Test.asset", so =>
            {
                so.baseHp              = 300;
                so.roamRadius          = 8f;
                so.detectRadius        = 6f;
                so.chaseRadius         = 15f;
                so.idleMoveSpeed       = 1.5f;
                so.chaseMoveSpeed      = 4f;
                so.weakpointMultiplier = 1.5f;
                // 어그로 임시값은 필드 기본값(2m/5s/1.5s) 그대로
                so.skills = new List<SkillItem>
                {
                    new SkillItem
                    {
                        displayName    = "휘두르기",
                        skillType      = SkillType.Melee,
                        damage         = 15,
                        cooldown       = 5f,
                        recoverTime    = 1.5f,
                        animDuration   = 1.2f,   // Bear_Attack1 클립 확정 후 step-07 헬퍼가 동기화
                        hitWindowStart = 0.3f,
                        hitWindowEnd   = 0.6f,
                        minEngage      = 0f,
                        range          = 2.5f,
                        arcAngle       = 90f,
                    },
                };
            });

            // ── 2. 근접 무기 2종 (Assets/Prefabs/Weapons의 Sword/Spear에 step-02가 배선) ──
            CreateOrLoad<MeleeWeaponData>(k_folder + "/Weapon_Sword.asset", so =>
            {
                so.kind          = WeaponKind.Sword;
                so.damageCoeff   = 10f;
                so.damageConst   = 0.25f;
                so.vMin          = 1.5f;
                so.vMax          = 6f;
                so.maxDurability = 30;
                so.repairCost    = 0;   // TODO(SPEC): 수리 단가 미정
            });

            CreateOrLoad<MeleeWeaponData>(k_folder + "/Weapon_Spear.asset", so =>
            {
                so.kind          = WeaponKind.Spear;
                so.damageCoeff   = 12f;  // 창 — 리치 대신 계수 소폭 우위 (임시 구분값)
                so.damageConst   = 0.25f;
                so.vMin          = 2f;
                so.vMax          = 7f;
                so.maxDurability = 25;
                so.repairCost    = 0;
            });

            // ── 3. 활/화살 (step-08 배선) — 풀 드로우 v0 = 25×1 − 5×0.1 = 24.5m/s ──
            CreateOrLoad<BowData>(k_folder + "/Bow_Wooden.asset", so =>
            {
                so.elasticity    = 25f;
                so.maxDurability = 40;
            });

            CreateOrLoad<ArrowData>(k_folder + "/Arrow_Basic.asset", so =>
            {
                so.weight = 0.1f;
                so.price  = 1;   // TODO(SPEC): 가격 미정
            });

            // ── 4. 전역 밸런스 ──
            CreateOrLoad<CombatBalanceData>(k_folder + "/CombatBalance.asset", so =>
            {
                so.bowDrawConstC        = 5f;
                so.arrowDamageK         = 1f;
                so.arrowWeightBonusB    = 3f;
                so.arrowVMin            = 5f;
                so.arrowVMax            = 25f;
                so.maxArrows            = 30;
                so.arrowRetrieveRadius  = 0.3f;
                so.hpScalePerExtraPlayer = 0.5f;
                so.reviveHoldSeconds    = 3f;
                so.reviveHpRatio        = 0.5f;
            });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[MakeAssets] 전투 데이터 생성 완료 — 생성 {s_created}개 / 스킵(기존) {s_skipped}개, 경로: {k_folder}");
        }

        /// <summary>에셋이 이미 있으면 로드해서 반환(스킵), 없으면 생성 후 configure 적용 (P0DataFactory 패턴).</summary>
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

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            var parts   = path.Split('/');
            var current = parts[0];
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
