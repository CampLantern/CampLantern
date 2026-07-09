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
