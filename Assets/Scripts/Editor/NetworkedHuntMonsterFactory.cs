#if UNITY_EDITOR
using System.Threading;
using CampLantern.Combat.Data;
using CampLantern.Combat.Monsters;
using CampLantern.Core;
using CampLantern.Hunting;
using CampLantern.Networking;
using Fusion;
using UnityEditor;
using UnityEngine;

namespace CampLantern.EditorTools
{
    /// <summary>
    /// 전투 곰 네트워크판 프리팹 생성기 (combat-detailed-network step-02).
    ///
    /// HuntMonster_Bear = NetworkObject(마스터 소유 + 권한 이전 시 파괴 방지 — HuntTargetPrefabFactory 규칙)
    /// + NetworkTransform(비권한 위치 수신) + NetworkedHuntMonster(권한 라우터) + HuntLedger(기여·보상)
    /// + Combat 조립(CombatMonsterPrefabFactory와 동일: Controller/Health/Aggro/Skills/HitCheck/AnimDriver)
    /// + Bear_4 실제 아트 + 피격/약점 볼륨.
    ///
    /// **기존 HuntTarget 3종(사슴/멧돼지/곰 단순 HP)과 병행** — 씬 스폰 4번째 슬롯(RoomScenesFactory.SetHuntPrefabs).
    /// Monster_Bear_Test.asset의 huntDef에 Hunt_Bear.asset을 주입한다(2인 협동 게이트·보상 재료).
    ///
    /// 선행: Combat Data / Combat Monster — Bear 경로의 에셋들. 스폰은 Fusion 프리팹 테이블 자동 베이킹 —
    /// 무음 실패 시 Fusion > Rebuild Prefab Table (tech-stack-decisions).
    /// RULE-02: PrefabUtility.SaveAsPrefabAsset만 사용.
    /// </summary>
    public static class NetworkedHuntMonsterFactory
    {
        private const string k_prefabPath = "Assets/Prefabs/HuntMonster_Bear.prefab";
        private const string k_dataPath = "Assets/Data/Combat/Monster_Bear_Test.asset";
        private const string k_balancePath = "Assets/Data/Combat/CombatBalance.asset";
        private const string k_huntDefPath = "Assets/Data/Hunt/Hunt_Bear.asset";
        private const string k_bearArtPrefabPath = "Assets/BlinkAnimals/Bear/Prefabs/Bear_4.prefab";

        [MenuItem("Tools/Make Assets/Hunt Monster — Bear (Networked)")]
        public static void CreateBear()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(k_prefabPath) != null)
            {
                Debug.LogWarning($"[MakeAssets] 이미 존재합니다 (수동 조정 보존 — 재생성하려면 삭제 후 실행): {k_prefabPath}");
                return;
            }

            var data = AssetDatabase.LoadAssetAtPath<MonsterData>(k_dataPath);
            var balance = AssetDatabase.LoadAssetAtPath<CombatBalanceData>(k_balancePath);
            var huntDef = AssetDatabase.LoadAssetAtPath<HuntTargetDef>(k_huntDefPath);
            var bearArt = AssetDatabase.LoadAssetAtPath<GameObject>(k_bearArtPrefabPath);
            if (data == null || balance == null || huntDef == null || bearArt == null)
            {
                Debug.LogError("[MakeAssets] 선행 에셋 없음 — Combat Data (Create All) / P0 Data / Bear_4 확인");
                return;
            }

            // 사냥 통합 데이터 연결 — 보상·필요 인원은 HuntTargetDef 소관 (README 결정)
            if (data.huntDef != huntDef)
            {
                data.huntDef = huntDef;
                EditorUtility.SetDirty(data);
            }

            var root = new GameObject("HuntMonster_Bear");
            try
            {
                // 마스터 클라이언트 소유 + 마스터 이탈 시 파괴 방지 — HuntTargetPrefabFactory와 동일 규칙
                var networkObject = root.AddComponent<NetworkObject>();
                networkObject.Flags = (networkObject.Flags | NetworkObjectFlags.MasterClientObject)
                                      & ~NetworkObjectFlags.DestroyWhenStateAuthorityLeaves;
                root.AddComponent<NetworkTransform>(); // 비권한 클라 위치 수신 (몬스터는 권한자가 이동)

                var controller = root.AddComponent<MonsterController>(); // RequireComponent가 MonsterHealth 선부착
                var health = root.GetComponent<MonsterHealth>();
                var aggro = root.AddComponent<AggroController>();
                var skills = root.AddComponent<SkillRunner>();
                root.AddComponent<SkillHitCheck>();
                var animDriver = root.AddComponent<MonsterAnimationDriver>();

                controller.Configure(data);
                health.Configure(data, balance); // 스케일링 계수 — 기여 신원은 NetworkedHuntMonster가 PlayerRef당 1:1 보장
                aggro.Configure(data);
                skills.Configure(data);

                root.AddComponent<HuntLedger>();          // 기여·보상 (무수정 부착)
                root.AddComponent<NetworkedHuntMonster>(); // 권한 라우터 — HitVolume Owner override는 Spawned에서

                // 실제 아트 (Animator 포함) — 머티리얼 URP 재적용은 멱등
                HuntTargetPrefabFactory.UpgradeMaterialToUrp("Assets/BlinkAnimals/Bear/Materials/Bear_4.mat",
                    baseMapPath: "Assets/BlinkAnimals/Bear/Textures/Stylized_Bear_Albedo4.png",
                    normalMapPath: "Assets/BlinkAnimals/Bear/Textures/Stylized_Bear_Normal.png",
                    emissionMapPath: "Assets/BlinkAnimals/Bear/Textures/Stylized_Bear_Emissive.png",
                    occlusionMapPath: "Assets/BlinkAnimals/Bear/Textures/Stylized_Bear_AO.png");
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(bearArt, root.transform);
                visual.name = "Visual";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one;

                animDriver.Configure(root.GetComponentInChildren<Animator>(),
                                     CombatMonsterPrefabFactory.LoadBearAttackClips());

                CombatMonsterPrefabFactory.AddHitVolumes(root); // 몸통/머리(약점) — 휴리스틱, 육안 확인 필요

                PrefabUtility.SaveAsPrefabAsset(root, k_prefabPath);
                Debug.Log($"[MakeAssets] HuntMonster_Bear created: {k_prefabPath} " +
                          $"(hp {data.baseHp}, coop {huntDef.RequiredParticipants}인, reward={huntDef.RewardMaterials?.Length ?? 0}종)");
            }
            finally { Object.DestroyImmediate(root); }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// 플레이 중 세션 접속 디버그 헬퍼 — IMGUI 버튼을 누를 수 없는 무인 검증(브릿지)용.
        /// 열린 씬의 SessionLauncher를 찾아 hunt_zone_a에 접속한다. fire-and-forget 태스크의
        /// 예외는 무관찰로 삼켜지므로 반드시 try/catch로 로깅한다 (진단 가능성).
        /// </summary>
        public static async void DebugJoinHuntZone()
        {
            if (!Application.isPlaying)
            {
                Debug.LogError("[MakeAssets] DebugJoinHuntZone은 플레이 중에만");
                return;
            }
            var launcher = Object.FindFirstObjectByType<SessionLauncher>();
            if (launcher == null)
            {
                Debug.LogError("[MakeAssets] SessionLauncher 없음 — HuntZone_A 씬인지 확인");
                return;
            }

            Debug.Log("[MakeAssets] DebugJoinHuntZone — hunt_zone_a 접속 시도");
            try
            {
                await launcher.StartHuntZone("a", CancellationToken.None);
                Debug.Log("[MakeAssets] DebugJoinHuntZone — 접속 성공");
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}
#endif
