#if UNITY_EDITOR
using CampLantern.Combat;
using CampLantern.Combat.Data;
using CampLantern.Combat.Monsters;
using UnityEditor;
using UnityEngine;

namespace CampLantern.EditorTools
{
    /// <summary>
    /// 로컬 전투 몬스터 프리팹 생성기 (design/combat-detailed step-03).
    ///
    /// CombatMonster_Bear: MonsterController+MonsterHealth(Monster_Bear_Test 데이터) 루트 +
    /// Blink 곰 실제 아트(Bear_4, Animator 포함) Visual + 피격/약점(머리) 볼륨.
    /// **네트워크 프리팹 HuntTarget_Bear와 별개** — NetworkObject 없음, CombatSandbox 로컬 검증용.
    /// 통합(권한 분리·HuntLedger)은 step-12에서 결정.
    ///
    /// 비주얼 인스턴스·머티리얼 URP 업그레이드는 HuntTargetPrefabFactory.UpgradeBearArt와 동일 방식
    /// (UpgradeMaterialToUrp 재사용 — 이미 업그레이드됐어도 재적용 무해/멱등).
    /// 머리 볼륨 위치는 바운즈 휴리스틱(최장 수평축 끝·상단) — 곰이 바라보는 축을 모델에서 확정할 수 없어
    /// **씬에서 육안 확인 후 조정 필요** (수동 체크리스트). 조정값은 프리팹에 남는다(재생성은 skip 정책).
    ///
    /// RULE-02: .prefab 직접 작성 금지 — PrefabUtility.SaveAsPrefabAsset만 사용.
    /// </summary>
    public static class CombatMonsterPrefabFactory
    {
        private const string k_prefabPath = "Assets/Prefabs/Combat/CombatMonster_Bear.prefab";
        private const string k_dataPath = "Assets/Data/Combat/Monster_Bear_Test.asset";
        private const string k_bearArtPrefabPath = "Assets/BlinkAnimals/Bear/Prefabs/Bear_4.prefab";
        private const string k_bearMaterialPath = "Assets/BlinkAnimals/Bear/Materials/Bear_4.mat";
        private const string k_bearTextureFolder = "Assets/BlinkAnimals/Bear/Textures";

        [MenuItem("Tools/Make Assets/Combat Monster — Bear")]
        public static void CreateBear()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(k_prefabPath) != null)
            {
                Debug.LogWarning($"[MakeAssets] 이미 존재합니다 (수동 조정 보존 — 재생성하려면 삭제 후 실행): {k_prefabPath}");
                return;
            }

            var data = AssetDatabase.LoadAssetAtPath<MonsterData>(k_dataPath);
            if (data == null)
            {
                Debug.LogError($"[MakeAssets] MonsterData 없음: {k_dataPath} — 먼저 Tools > Make Assets > Combat Data (Create All)");
                return;
            }

            var bearArt = AssetDatabase.LoadAssetAtPath<GameObject>(k_bearArtPrefabPath);
            if (bearArt == null)
            {
                Debug.LogError($"[MakeAssets] Bear_4.prefab 없음: {k_bearArtPrefabPath}");
                return;
            }

            // Bear_4.mat URP 업그레이드 — HuntTargetPrefabFactory와 동일 배선, 멱등
            HuntTargetPrefabFactory.UpgradeMaterialToUrp(k_bearMaterialPath,
                baseMapPath:      $"{k_bearTextureFolder}/Stylized_Bear_Albedo4.png",
                normalMapPath:    $"{k_bearTextureFolder}/Stylized_Bear_Normal.png",
                emissionMapPath:  $"{k_bearTextureFolder}/Stylized_Bear_Emissive.png",
                occlusionMapPath: $"{k_bearTextureFolder}/Stylized_Bear_AO.png");

            EnsureFolder("Assets/Prefabs/Combat");

            var root = new GameObject("CombatMonster_Bear");
            try
            {
                var controller = root.AddComponent<MonsterController>(); // RequireComponent가 MonsterHealth 선부착
                var health = root.GetComponent<MonsterHealth>();
                var aggro = root.AddComponent<AggroController>();        // step-05 — 어그로 6규칙
                var skills = root.AddComponent<SkillRunner>();           // step-06 — 스킬 선택/쿨타임
                root.AddComponent<SkillHitCheck>();                      // step-06 — hit window 판정
                controller.Configure(data);
                health.Configure(data,
                    AssetDatabase.LoadAssetAtPath<CombatBalanceData>("Assets/Data/Combat/CombatBalance.asset")); // §4-2 스케일링
                aggro.Configure(data);
                skills.Configure(data);

                // 실제 아트 — 중첩 프리팹 연결 유지 (Animator+BearAnimator.controller 포함)
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(bearArt, root.transform);
                visual.name = "Visual";
                visual.transform.localPosition = Vector3.zero;   // 데모 씬 잔여 트랜스폼 제거 (HuntTargetPrefabFactory 주석 참조)
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one;

                AddHitVolumes(root);

                PrefabUtility.SaveAsPrefabAsset(root, k_prefabPath);
                Debug.Log($"[MakeAssets] CombatMonster_Bear created: {k_prefabPath} (hp {data.baseHp}, detect {data.detectRadius}m)");
            }
            finally { Object.DestroyImmediate(root); }
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// 기존 곰 프리팹에 이후 단계 컴포넌트를 보장 배선한다(멱등 — 수동 조정 보존).
        /// CreateBear는 프리팹이 있으면 skip하므로, 단계가 진행되며 컴포넌트가 늘 때 이 메뉴를 재실행한다.
        /// 현재 보장 목록: AggroController(step-05). 이후 단계 컴포넌트는 여기에 추가.
        /// </summary>
        [MenuItem("Tools/Make Assets/Combat Monster — Ensure Components")]
        public static void EnsureBearComponents()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(k_prefabPath) == null)
            {
                Debug.LogError($"[MakeAssets] 곰 프리팹 없음: {k_prefabPath} — 먼저 Combat Monster — Bear 실행");
                return;
            }

            var data = AssetDatabase.LoadAssetAtPath<MonsterData>(k_dataPath);
            if (data == null)
            {
                Debug.LogError($"[MakeAssets] MonsterData 없음: {k_dataPath}");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(k_prefabPath);
            try
            {
                int added = 0;

                var balance = AssetDatabase.LoadAssetAtPath<CombatBalanceData>("Assets/Data/Combat/CombatBalance.asset");
                root.GetComponent<MonsterHealth>().Configure(data, balance); // step-09 — HP 스케일링 계수 주입

                var aggro = root.GetComponent<AggroController>();
                if (aggro == null)
                {
                    aggro = root.AddComponent<AggroController>();
                    added++;
                }
                aggro.Configure(data); // 데이터 참조는 항상 재주입 (멱등)

                var skills = root.GetComponent<SkillRunner>();           // step-06
                if (skills == null)
                {
                    skills = root.AddComponent<SkillRunner>();
                    added++;
                }
                skills.Configure(data);

                if (root.GetComponent<SkillHitCheck>() == null)          // step-06
                {
                    root.AddComponent<SkillHitCheck>();
                    added++;
                }

                var animDriver = root.GetComponent<MonsterAnimationDriver>(); // step-07
                if (animDriver == null)
                {
                    animDriver = root.AddComponent<MonsterAnimationDriver>();
                    added++;
                }
                animDriver.Configure(root.GetComponentInChildren<Animator>(), LoadBearAttackClips());

                PrefabUtility.SaveAsPrefabAsset(root, k_prefabPath);
                Debug.Log($"[MakeAssets] CombatMonster_Bear ensure done (added {added})");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        /// <summary>
        /// 스킬 animDuration을 매핑된 클립 길이로 동기화 (지시서 5단계 헬퍼 — step-07에서 구현).
        /// 런타임 SO 변형 대신 에디터에서 에셋 자체를 갱신 — 튜닝 값은 에셋에 산다는 원칙(지시서 규칙 4) 유지,
        /// 플레이 중 SO 오염(unity-scripting-gotchas) 회피. 클립 매핑은 곰 프리팹의 MonsterAnimationDriver가 소유.
        /// </summary>
        [MenuItem("Tools/Make Assets/Combat — Sync Skill Anim Durations")]
        public static void SyncSkillAnimDurations()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(k_prefabPath);
            var data = AssetDatabase.LoadAssetAtPath<MonsterData>(k_dataPath);
            var driver = prefab != null ? prefab.GetComponent<MonsterAnimationDriver>() : null;
            if (driver == null || data == null || data.skills == null)
            {
                Debug.LogError("[MakeAssets] Sync 실패 — 곰 프리팹(드라이버 포함)과 Monster_Bear_Test가 필요. Ensure Components 먼저 실행");
                return;
            }

            AnimationClip[] clips = driver.AttackClips;
            int synced = 0;
            for (int i = 0; i < data.skills.Count && clips != null && i < clips.Length; i++)
            {
                if (clips[i] == null) continue;
                float before = data.skills[i].animDuration;
                data.skills[i].animDuration = clips[i].length;
                Debug.Log($"[MakeAssets] animDuration sync: skill[{i}] {before:F2} -> {clips[i].length:F2} ({clips[i].name})");
                synced++;
            }

            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            Debug.Log($"[MakeAssets] Sync Skill Anim Durations done (synced {synced})");
        }

        // Bear_Attack1 클립 로드 — FBX 서브에셋에서 프리뷰 제외 첫 AnimationClip
        private static AnimationClip[] LoadBearAttackClips()
        {
            const string path = "Assets/BlinkAnimals/Bear/Animations/Bear_Attack1.fbx";
            foreach (Object sub in AssetDatabase.LoadAllAssetsAtPath(path))
                if (sub is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                    return new[] { clip };

            Debug.LogWarning($"[MakeAssets] 공격 클립 없음: {path} — animDuration 동기화 불가 (임시값 유지)");
            return new AnimationClip[0];
        }

        // 피격 볼륨: 몸통 = 합산 바운즈 박스(90%), 머리 = 최장 수평축 끝·상단의 구 (휴리스틱 — 육안 확인 필요)
        private static void AddHitVolumes(GameObject root)
        {
            if (!EditorMeshBounds.TryComputeCombined(root, out Bounds bounds))
            {
                Debug.LogWarning("[MakeAssets] 렌더러 바운즈 없음 — 피격 볼륨을 기본 크기로 배치 (수동 조정 필요)");
                bounds = new Bounds(new Vector3(0f, 1f, 0f), new Vector3(1f, 2f, 2f));
            }

            var body = new GameObject("HitVolume_Body");
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = bounds.center;
            var bodyBox = body.AddComponent<BoxCollider>();
            bodyBox.size = bounds.size * 0.9f; // 머리 볼륨과 과도한 중첩 방지 — 약간 축소
            body.AddComponent<HitVolume>().Configure(isWeakpoint: false);

            // 머리 — 수평 최장축(몸 길이)의 +끝, 높이 상단 75% 지점
            int axis = bounds.size.x >= bounds.size.z ? 0 : 2;
            Vector3 headPos = bounds.center;
            headPos[axis] = bounds.max[axis];
            headPos.y = bounds.min.y + bounds.size.y * 0.75f;

            var head = new GameObject("HitVolume_Head");
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = headPos;
            var headSphere = head.AddComponent<SphereCollider>();
            headSphere.radius = bounds.size.y * 0.2f;
            head.AddComponent<HitVolume>().Configure(isWeakpoint: true);

            Debug.LogWarning("[MakeAssets] HitVolume_Head 위치는 휴리스틱 — 곰 머리 위치와 맞는지 씬에서 확인/조정할 것");
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
