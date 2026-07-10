using System.Collections;
using CampLantern.Combat.Data;
using CampLantern.Combat.Monsters;
using CampLantern.Combat.Weapons;
using CampLantern.Core;
using UnityEngine;

namespace CampLantern.Combat.Player
{
    /// <summary>
    /// 홀스터/내구도 귀환/수리/씬 제한 무인 검증 봇 (§1-5, §1-6 — step-10). 씬 의존 없음.
    ///
    /// 페이즈: 1) 슬롯 규칙 (등=검/창/활, 허리=검 전용, 점유 거부, 인출 후 재수납)
    ///         2) 내구도 0 → 홀스터 강제 귀환 + 콜라이더 비활성(집기 거부)
    ///         3) 수리 (무료 스텁) → 내구도 복구 + 인출 가능
    ///         4) 씬 제한 — 게이트 차단 시 근접 무판정·활 발사 불가, 허용 시 발사 가능
    /// </summary>
    public class HolsterSelfTest : MonoBehaviour
    {
        private const string k_tag = "[HolsterTest]";

        private WeaponHolster m_holster;
        private Blacksmith m_blacksmith;
        private Wallet m_wallet;
        private MonsterHealth m_target;
        private Vector3 m_origin;
        private bool m_failed;
        private bool m_brokenReturned;

        private MeleeWeapon m_sword1, m_sword2;
        private MeleeWeapon m_spear;
        private Bow m_bow1, m_bow2;
        private ArrowQuiver m_quiver;

        private void Start()
        {
            m_origin = new Vector3(2200f, 0f, 2200f);

            var playerGo = new GameObject("HolsterTest_Player");
            playerGo.transform.position = m_origin;
            var back = NewAnchor(playerGo, "Anchor_Back", new Vector3(0f, 1.5f, -0.2f));
            var hipL = NewAnchor(playerGo, "Anchor_HipL", new Vector3(-0.25f, 1f, 0f));
            var hipR = NewAnchor(playerGo, "Anchor_HipR", new Vector3(0.25f, 1f, 0f));
            m_holster = playerGo.AddComponent<WeaponHolster>();
            m_holster.Configure(back, hipL, hipR, rightHanded: true);
            m_holster.BrokenWeaponReturned += OnBrokenReturned;

            m_blacksmith = playerGo.AddComponent<Blacksmith>();
            m_wallet = new Wallet(); // 0코인 — 무료(단가 0) 수리 경로 검증

            // 표적 (파손 스윙용)
            var monsterData = ScriptableObject.CreateInstance<MonsterData>();
            monsterData.baseHp = 100000;
            var targetGo = new GameObject("HolsterTest_Target");
            targetGo.transform.position = m_origin + new Vector3(10f, 0f, 0f);
            m_target = targetGo.AddComponent<MonsterHealth>();
            m_target.Configure(monsterData);
            var body = new GameObject("Body");
            body.transform.SetParent(targetGo.transform, false);
            body.transform.localPosition = new Vector3(0f, 1f, 0f);
            var bodyCol = body.AddComponent<CapsuleCollider>();
            bodyCol.height = 1.6f;
            bodyCol.radius = 0.4f;
            body.AddComponent<HitVolume>().Configure(isWeakpoint: false);

            m_sword1 = CreateMelee("HolsterTest_Sword1", WeaponKind.Sword, maxDurability: 1);
            m_sword2 = CreateMelee("HolsterTest_Sword2", WeaponKind.Sword, maxDurability: 30);
            m_spear = CreateMelee("HolsterTest_Spear", WeaponKind.Spear, maxDurability: 30);
            (m_bow1, m_quiver) = CreateBow("HolsterTest_Bow1");
            (m_bow2, _) = CreateBow("HolsterTest_Bow2");

            Debug.Log($"{k_tag} start — 4 phases");
            StartCoroutine(Run());
        }

        private void OnDestroy()
        {
            if (m_holster != null)
            {
                m_holster.BrokenWeaponReturned -= OnBrokenReturned;
                if (m_sword1 != null) m_holster.UnregisterWeapon(m_sword1);
            }
            CombatScenes.SetTestOverride(null); // 게이트 복원 보장
        }

        private void OnBrokenReturned(GameObject weapon, HolsterSlot slot) => m_brokenReturned = true;

        private IEnumerator Run()
        {
            yield return new WaitForSeconds(3f);

            // ── 1) 슬롯 규칙 ──
            bool bowToBack = m_holster.TryStow(m_bow1.gameObject, WeaponKind.Bow)
                             && m_holster.GetStowed(HolsterSlot.Back) == m_bow1.gameObject;
            bool bow2Rejected = !m_holster.TryStow(m_bow2.gameObject, WeaponKind.Bow);       // 등 점유·허리 불허
            bool swordToHipR = m_holster.TryStow(m_sword2.gameObject, WeaponKind.Sword)
                               && m_holster.GetStowed(HolsterSlot.HipRight) == m_sword2.gameObject; // 오른손잡이 주손
            bool spearRejected = !m_holster.TryStow(m_spear.gameObject, WeaponKind.Spear);   // 창은 등 전용
            bool bowOut = m_holster.TryWithdraw(HolsterSlot.Back, out _);
            bool spearToBack = m_holster.TryStow(m_spear.gameObject, WeaponKind.Spear)
                               && m_holster.GetStowed(HolsterSlot.Back) == m_spear.gameObject;
            Report("PHASE1_SLOT_RULES", bowToBack && bow2Rejected && swordToHipR && spearRejected && bowOut && spearToBack,
                   $"bowBack={bowToBack} bow2Reject={bow2Rejected} swordHipR={swordToHipR} spearReject={spearRejected} withdraw={bowOut} spearBack={spearToBack}");

            // ── 2) 내구도 0 → 강제 귀환 + 집기 거부 ──
            m_holster.RegisterWeapon(m_sword1); // 파손 구독 (내구도 1)
            yield return SwingThrough(m_sword1.transform, 4f);
            yield return null;
            bool broken = m_sword1.IsBroken;
            bool returned = m_brokenReturned && m_sword1.transform.parent != null
                            && m_sword1.transform.parent.name.StartsWith("Anchor_");
            bool grabBlocked = !AnyColliderEnabled(m_sword1.gameObject);
            bool withdrawBlocked = !m_holster.TryWithdraw(HolsterSlot.HipLeft, out _); // 파손 무기 인출 거부
            Report("PHASE2_BROKEN_RETURN", broken && returned && grabBlocked && withdrawBlocked,
                   $"broken={broken} returned={returned}(parent={ParentName(m_sword1)}) grabBlocked={grabBlocked} withdrawBlocked={withdrawBlocked}");

            // ── 3) 수리 → 복구 + 인출 가능 ──
            bool repaired = m_blacksmith.TryRepair(m_sword1, m_wallet);
            bool durabilityOk = m_sword1.CurrentDurability == 1 && !m_sword1.IsBroken;
            bool grabRestored = AnyColliderEnabled(m_sword1.gameObject);
            bool withdrawOk = m_holster.TryWithdraw(HolsterSlot.HipLeft, out _);
            Report("PHASE3_REPAIR", repaired && durabilityOk && grabRestored && withdrawOk,
                   $"repaired={repaired} durability={m_sword1.CurrentDurability}/1 grabRestored={grabRestored} withdraw={withdrawOk}");

            // ── 4) 씬 제한 — 차단 시 근접 무판정·발사 불가 / 허용 시 발사 ──
            CombatScenes.SetTestOverride(false);
            m_sword2.RequireCombatScene = true;
            m_bow1.RequireCombatScene = true;
            m_holster.TryWithdraw(HolsterSlot.HipRight, out _); // sword2 인출
            int hpBefore = m_target.CurrentHp;
            yield return SwingThrough(m_sword2.transform, 4f);
            bool meleeGated = m_target.CurrentHp == hpBefore;
            m_bow1.Quiver = m_quiver;
            m_bow1.SetDrawRatio(1f);
            bool fireGated = !m_bow1.TryFire();
            CombatScenes.SetTestOverride(true);
            m_bow1.SetDrawRatio(1f);
            m_bow1.transform.position = m_origin + new Vector3(0f, 1f, -3f); // 표적 반대편 — 명중 무관
            bool fireAllowed = m_bow1.TryFire();
            CombatScenes.SetTestOverride(null);
            Report("PHASE4_SCENE_GATE", meleeGated && fireGated && fireAllowed,
                   $"meleeGated={meleeGated} fireGated={fireGated} fireAllowed={fireAllowed}");

            Debug.Log($"{k_tag} {(m_failed ? "RESULT FAIL" : "RESULT ALL PASS")}");
        }

        // 무기를 표적 몸통 높이에서 +X로 통과시키는 스윙 (멜레 봇 기하 재사용)
        private IEnumerator SwingThrough(Transform weaponRoot, float speed)
        {
            Vector3 bodyCenter = m_target.transform.position + new Vector3(0f, 1f, 0f);
            weaponRoot.SetParent(null, true);
            weaponRoot.rotation = Quaternion.identity;
            weaponRoot.position = bodyCenter + new Vector3(-2f, 0f, -0.65f);
            weaponRoot.GetComponent<MeleeWeapon>()?.NotifyTeleported();
            yield return null;

            while (weaponRoot.position.x < bodyCenter.x + 2f)
            {
                var pos = weaponRoot.position;
                pos.x += speed * Time.deltaTime;
                weaponRoot.position = pos;
                yield return null;
            }
        }

        private MeleeWeapon CreateMelee(string name, WeaponKind kind, int maxDurability)
        {
            var data = ScriptableObject.CreateInstance<MeleeWeaponData>();
            data.kind = kind;
            data.damageCoeff = 10f;
            data.damageConst = 0.25f;
            data.vMin = 1.5f;
            data.vMax = 6f;
            data.maxDurability = maxDurability;

            var root = new GameObject(name);
            root.transform.position = m_origin + new Vector3(0f, 0.5f, 2f);
            root.AddComponent<BoxCollider>().size = new Vector3(0.05f, 0.05f, 1f); // 집기 거부(콜라이더 비활성) 검증용
            var hitBase = NewAnchor(root, "HitRef_Base", new Vector3(0f, 0f, 0.3f));
            var hitTip = NewAnchor(root, "HitRef_Tip", new Vector3(0f, 0f, 1f));
            var melee = root.AddComponent<MeleeWeapon>();
            melee.Configure(data, hitBase, hitTip);
            return melee;
        }

        private (Bow, ArrowQuiver) CreateBow(string name)
        {
            var bowData = ScriptableObject.CreateInstance<BowData>();
            bowData.elasticity = 25f;
            bowData.maxDurability = 40;
            var arrowData = ScriptableObject.CreateInstance<ArrowData>();
            arrowData.weight = 0.1f;
            var balance = ScriptableObject.CreateInstance<CombatBalanceData>();
            balance.bowDrawConstC = 5f; balance.arrowDamageK = 1f; balance.arrowWeightBonusB = 3f;
            balance.arrowVMin = 5f; balance.arrowVMax = 25f; balance.maxArrows = 30; balance.arrowRetrieveRadius = 0.3f;

            var template = new GameObject(name + "_ArrowTemplate");
            template.AddComponent<Arrow>().Configure(arrowData, balance);
            template.SetActive(false);

            var root = new GameObject(name);
            root.transform.position = m_origin + new Vector3(0f, 1f, 3f);
            var origin = NewAnchor(root, "ArrowOrigin", Vector3.zero);
            var bow = root.AddComponent<Bow>();
            bow.Configure(bowData, balance, template, origin);

            var quiver = new GameObject(name + "_Quiver").AddComponent<ArrowQuiver>();
            quiver.Configure(balance, null);
            return (bow, quiver);
        }

        private static Transform NewAnchor(GameObject parent, string name, Vector3 localPos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent.transform, false);
            t.localPosition = localPos;
            return t;
        }

        private static bool AnyColliderEnabled(GameObject go)
        {
            foreach (var col in go.GetComponentsInChildren<Collider>())
                if (col.enabled) return true;
            return false;
        }

        private static string ParentName(Component c) => c.transform.parent != null ? c.transform.parent.name : "(none)";

        private void Report(string label, bool ok, string detail)
        {
            if (!ok) m_failed = true;
            Debug.Log($"{k_tag} {label} {(ok ? "PASS" : "FAIL")} — {detail}");
        }
    }
}
