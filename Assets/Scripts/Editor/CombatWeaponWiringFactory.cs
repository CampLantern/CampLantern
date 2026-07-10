#if UNITY_EDITOR
using CampLantern.Combat.Data;
using CampLantern.Combat.Weapons;
using UnityEditor;
using UnityEngine;

namespace CampLantern.EditorTools
{
    /// <summary>
    /// 무기 프리팹 전투 배선 (design/combat-detailed step-02).
    ///
    /// Wire Melee Weapons: Sword/Spear 프리팹에 MeleeWeapon 부착 + step-01 데이터 참조 주입 +
    /// 타격부 기준점(HitRef_Base/HitRef_Tip) 자식 생성. **멱등** — 재실행 시 컴포넌트/참조는 갱신하되,
    /// 기존 기준점의 위치는 보존한다(수동 조정 유실 방지). WeaponPrefabFactory의
    /// "Weapons (Force Recreate)"가 배선을 날려도 이 메뉴 재실행으로 복구.
    ///
    /// 기준점 자동 배치: 렌더러 메시 바운즈 합산(EditorMeshBounds — Renderer.bounds 캐시 비의존)에서
    /// 가장 긴 축을 날 방향으로 보고, 원점(그립)에서 먼 끝 = Tip, 가까운 끝에서 40% 지점 = Base.
    /// 정밀 위치는 에디터 수동 조정 (조정값은 재실행에도 보존됨).
    ///
    /// (step-02의 DummyMonster 프리팹 메뉴는 step-03에서 제거 — 표적은 CombatMonster_Bear로 대체.)
    ///
    /// RULE-02: .prefab 직접 작성 금지 — PrefabUtility.LoadPrefabContents/SaveAsPrefabAsset만 사용.
    /// 직렬화 필드 주입은 SerializedObject.FindProperty 대신 각 컴포넌트의 Configure 메서드로
    /// (필드명 문자열 의존 제거 — knowledge/unity-scripting-gotchas.md §3).
    /// </summary>
    public static class CombatWeaponWiringFactory
    {
        private const string k_weaponFolder = "Assets/Prefabs/Weapons";
        private const string k_dataFolder = "Assets/Data/Combat";

        [MenuItem("Tools/Make Assets/Combat — Wire Melee Weapons")]
        public static void WireMeleeWeapons()
        {
            WireOne(k_weaponFolder + "/Sword.prefab", k_dataFolder + "/Weapon_Sword.asset");
            WireOne(k_weaponFolder + "/Spear.prefab", k_dataFolder + "/Weapon_Spear.asset");
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Bow/Arrow 프리팹 전투 배선 (step-08). Arrow 먼저(Bow가 프리팹 에셋을 참조), 멱등.
        /// ArrowQuiver는 프리팹이 아니라 플레이어 측 런타임 주입(Bow.Quiver) — 하네스(step-11)/홀스터(step-10) 소관.
        /// VR 양손 당김(시위 손-그립 거리 → SetDrawRatio)·시위 비주얼·화살집 배선은 수동 작업 체크리스트.
        /// </summary>
        [MenuItem("Tools/Make Assets/Combat — Wire Bow & Arrow")]
        public static void WireBowAndArrow()
        {
            string arrowPath = k_weaponFolder + "/Arrow.prefab";
            string bowPath = k_weaponFolder + "/Bow.prefab";

            var arrowData = AssetDatabase.LoadAssetAtPath<ArrowData>(k_dataFolder + "/Arrow_Basic.asset");
            var bowData = AssetDatabase.LoadAssetAtPath<BowData>(k_dataFolder + "/Bow_Wooden.asset");
            var balance = AssetDatabase.LoadAssetAtPath<CombatBalanceData>(k_dataFolder + "/CombatBalance.asset");
            if (arrowData == null || bowData == null || balance == null)
            {
                Debug.LogError("[MakeAssets] 활/화살 데이터 없음 — 먼저 Tools > Make Assets > Combat Data (Create All)");
                return;
            }

            // ── Arrow.prefab ──
            if (AssetDatabase.LoadAssetAtPath<GameObject>(arrowPath) == null)
            {
                Debug.LogError($"[MakeAssets] 화살 프리팹 없음: {arrowPath} — 먼저 Weapons (Create All)");
                return;
            }
            GameObject arrowRoot = PrefabUtility.LoadPrefabContents(arrowPath);
            try
            {
                var arrow = arrowRoot.GetComponent<Arrow>();
                if (arrow == null) arrow = arrowRoot.AddComponent<Arrow>();
                arrow.Configure(arrowData, balance);
                PrefabUtility.SaveAsPrefabAsset(arrowRoot, arrowPath);
                Debug.Log($"[MakeAssets] arrow wired: {arrowPath}");
            }
            finally { PrefabUtility.UnloadPrefabContents(arrowRoot); }

            // ── Bow.prefab ──
            var arrowPrefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(arrowPath);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(bowPath) == null)
            {
                Debug.LogError($"[MakeAssets] 활 프리팹 없음: {bowPath} — 먼저 Weapons (Create All)");
                return;
            }
            GameObject bowRoot = PrefabUtility.LoadPrefabContents(bowPath);
            try
            {
                Transform origin = bowRoot.transform.Find("ArrowOrigin");
                if (origin == null)
                {
                    origin = CreateChildAt(bowRoot.transform, "ArrowOrigin", new Vector3(0f, 0f, 0.2f));
                    Debug.LogWarning("[MakeAssets] Bow ArrowOrigin을 기본 위치에 생성 — 그립/시위에 맞게 씬에서 조정 필요 (조정값 보존)");
                }

                var bow = bowRoot.GetComponent<Bow>();
                if (bow == null) bow = bowRoot.AddComponent<Bow>();
                bow.Configure(bowData, balance, arrowPrefabAsset, origin);
                PrefabUtility.SaveAsPrefabAsset(bowRoot, bowPath);
                Debug.Log($"[MakeAssets] bow wired: {bowPath} (arrow={arrowPrefabAsset.name})");
            }
            finally { PrefabUtility.UnloadPrefabContents(bowRoot); }

            AssetDatabase.SaveAssets();
        }

        private static void WireOne(string prefabPath, string dataPath)
        {
            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabAsset == null)
            {
                Debug.LogError($"[MakeAssets] 무기 프리팹 없음: {prefabPath} — 먼저 Tools > Make Assets > Weapons (Create All)");
                return;
            }

            var data = AssetDatabase.LoadAssetAtPath<MeleeWeaponData>(dataPath);
            if (data == null)
            {
                Debug.LogError($"[MakeAssets] MeleeWeaponData 없음: {dataPath} — 먼저 Tools > Make Assets > Combat Data (Create All)");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                // 기준점 — 있으면 위치 보존, 없으면 바운즈 휴리스틱으로 생성
                Transform hitBase = root.transform.Find("HitRef_Base");
                Transform hitTip  = root.transform.Find("HitRef_Tip");
                if (hitBase == null || hitTip == null)
                {
                    ComputeBladeEnds(root, out Vector3 basePos, out Vector3 tipPos);
                    if (hitBase == null) hitBase = CreateChildAt(root.transform, "HitRef_Base", basePos);
                    if (hitTip == null)  hitTip  = CreateChildAt(root.transform, "HitRef_Tip", tipPos);
                }

                var weapon = root.GetComponent<MeleeWeapon>();
                if (weapon == null) weapon = root.AddComponent<MeleeWeapon>();
                weapon.Configure(data, hitBase, hitTip);

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log($"[MakeAssets] melee wired: {prefabPath} (data={data.name}, kind={data.kind})");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static Transform CreateChildAt(Transform parent, string name, Vector3 localPos)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            child.localPosition = localPos;
            return child;
        }

        /// <summary>
        /// 렌더러 합산 바운즈의 최장축을 날 방향으로 간주 — 원점(그립 피벗)에서 먼 끝을 Tip,
        /// 가까운 끝에서 40% 지점을 Base로 잡는다. root는 프리팹 콘텐츠 루트(원점·identity)라 world == 루트 로컬.
        /// </summary>
        private static void ComputeBladeEnds(GameObject root, out Vector3 basePos, out Vector3 tipPos)
        {
            if (!EditorMeshBounds.TryComputeCombined(root, out Bounds combined))
            {
                Debug.LogWarning($"[MakeAssets] {root.name}: 메시 바운즈 없음 — 기준점을 기본값으로 배치 (수동 조정 필요)");
                basePos = new Vector3(0f, 0f, 0.3f);
                tipPos  = new Vector3(0f, 0f, 1f);
                return;
            }

            int axis = 0;
            if (combined.size.y > combined.size[axis]) axis = 1;
            if (combined.size.z > combined.size[axis]) axis = 2;

            float min = combined.min[axis];
            float max = combined.max[axis];
            float far  = Mathf.Abs(max) >= Mathf.Abs(min) ? max : min; // 원점에서 먼 끝 = 칼끝
            float near = far == max ? min : max;

            Vector3 center = combined.center;
            tipPos = center; tipPos[axis] = far;
            basePos = center; basePos[axis] = Mathf.Lerp(near, far, 0.4f);
        }

    }
}
#endif
