#if UNITY_EDITOR
using CampLantern.Combat;
using CampLantern.Combat.Data;
using CampLantern.Combat.Monsters;
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
    /// 기준점 자동 배치: 렌더러 메시 바운즈 합산(로컬 바운즈 × localToWorldMatrix — Renderer.bounds
    /// 캐시 비의존, WeaponPrefabFactory.AddGrabPhysics와 동일 취지)에서 가장 긴 축을 날 방향으로 보고,
    /// 원점(그립)에서 먼 끝 = Tip, 가까운 끝에서 40% 지점 = Base. 정밀 위치는 에디터 수동 조정
    /// (조정값은 재실행에도 보존됨).
    ///
    /// Dummy Monster: 캡슐 몸통 + 구 머리(약점) 피격 볼륨을 가진 임시 표적 프리팹 — step-03에서
    /// CombatMonster_Bear로 대체 예정.
    ///
    /// RULE-02: .prefab 직접 작성 금지 — PrefabUtility.LoadPrefabContents/SaveAsPrefabAsset만 사용.
    /// 직렬화 필드 주입은 SerializedObject.FindProperty 대신 각 컴포넌트의 Configure 메서드로
    /// (필드명 문자열 의존 제거 — knowledge/unity-scripting-gotchas.md §3).
    /// </summary>
    public static class CombatWeaponWiringFactory
    {
        private const string k_weaponFolder = "Assets/Prefabs/Weapons";
        private const string k_combatPrefabFolder = "Assets/Prefabs/Combat";
        private const string k_dataFolder = "Assets/Data/Combat";

        [MenuItem("Tools/Make Assets/Combat — Wire Melee Weapons")]
        public static void WireMeleeWeapons()
        {
            WireOne(k_weaponFolder + "/Sword.prefab", k_dataFolder + "/Weapon_Sword.asset");
            WireOne(k_weaponFolder + "/Spear.prefab", k_dataFolder + "/Weapon_Spear.asset");
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/Make Assets/Combat — Dummy Monster (Prefab)")]
        public static void CreateDummyMonsterPrefab()
        {
            string prefabPath = k_combatPrefabFolder + "/DummyMonster.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
            {
                Debug.LogWarning($"[MakeAssets] 이미 존재합니다 (삭제 후 재실행 시 재생성): {prefabPath}");
                return;
            }

            EnsureFolder(k_combatPrefabFolder);

            var root = new GameObject("DummyMonster");
            try
            {
                root.AddComponent<DummyMonster>();

                // 몸통 — 프리미티브 캡슐(높이2·반경0.5, 중심 y=1). 콜라이더가 곧 피격 볼륨.
                var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.name = "Body";
                body.transform.SetParent(root.transform, false);
                body.transform.localPosition = new Vector3(0f, 1f, 0f);
                body.AddComponent<HitVolume>().Configure(isWeakpoint: false);

                // 머리(약점) — 구(스케일 0.6 → 반경 0.3), 몸통 위.
                var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                head.name = "Head_Weakpoint";
                head.transform.SetParent(root.transform, false);
                head.transform.localPosition = new Vector3(0f, 2.3f, 0f);
                head.transform.localScale = Vector3.one * 0.6f;
                head.AddComponent<HitVolume>().Configure(isWeakpoint: true);

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log($"[MakeAssets] DummyMonster prefab created: {prefabPath}");
            }
            finally { Object.DestroyImmediate(root); }
            AssetDatabase.Refresh();
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
            bool hasBounds = false;
            var combined = new Bounds();
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                if (!TryGetLocalMeshBounds(r, out var localBounds)) continue;
                EncapsulateLocalBounds(ref combined, ref hasBounds, r.transform, localBounds);
            }

            if (!hasBounds)
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

        // 아래 두 헬퍼는 WeaponPrefabFactory와 동일 로직 (해당 파일은 이번 단계 수정 범위 밖이라 복제)
        private static bool TryGetLocalMeshBounds(Renderer r, out Bounds bounds)
        {
            switch (r)
            {
                case MeshRenderer _ when r.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh != null:
                    bounds = mf.sharedMesh.bounds;
                    return true;
                case SkinnedMeshRenderer smr when smr.sharedMesh != null:
                    bounds = smr.sharedMesh.bounds;
                    return true;
                default:
                    bounds = default;
                    return false;
            }
        }

        private static void EncapsulateLocalBounds(ref Bounds combined, ref bool hasBounds, Transform transform, Bounds localBounds)
        {
            var matrix = transform.localToWorldMatrix;
            var center = localBounds.center;
            var extents = localBounds.extents;
            for (int xi = -1; xi <= 1; xi += 2)
            for (int yi = -1; yi <= 1; yi += 2)
            for (int zi = -1; zi <= 1; zi += 2)
            {
                var corner = center + Vector3.Scale(extents, new Vector3(xi, yi, zi));
                var world  = matrix.MultiplyPoint3x4(corner);
                if (!hasBounds) { combined = new Bounds(world, Vector3.zero); hasBounds = true; }
                else combined.Encapsulate(world);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parts = path.Split('/');
            var current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
#endif
