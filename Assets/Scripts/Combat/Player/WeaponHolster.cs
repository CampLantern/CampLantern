using System;
using CampLantern.Combat.Weapons;
using UnityEngine;

namespace CampLantern.Combat.Player
{
    public enum HolsterSlot { Back, HipLeft, HipRight }

    /// <summary>
    /// 무기 홀스터 (§1-6) — 슬롯 3개: 등(검/창/활 허용), 허리 좌/우(검 전용). 슬롯별 허용 타입 검사.
    /// 좌우 배치는 설정값(기본 오른손잡이 — 주 슬롯 = 허리 오른쪽).
    ///
    /// 내구도 0 처리 (§1-5): RegisterWeapon으로 구독한 무기의 WeaponBroken 수신 시 홀스터로 강제 귀환 +
    /// 사용 불가(콜라이더 비활성 — Grabbable이 콜라이더를 요구하므로 집기 시도가 물리적으로 거부된다.
    /// SDK 타입 직접 참조 없이 동작하는 가장 단순한 차단, 근거 주석). 수리(Blacksmith) 후 복구.
    ///
    /// 수납 순간이동은 MeleeWeapon.NotifyTeleported 규약 호출 — 텔레포트 경로 유령 스윕 차단 (step-02).
    /// 앵커는 주입형 — PersistentPlayer 리그 배선(RULE-02: 프리팹 직접 편집 금지)은 step-11 팩토리/체크리스트.
    /// Spec: 지시서 7단계 (§1-5, §1-6)
    /// </summary>
    public class WeaponHolster : MonoBehaviour
    {
        private static readonly WeaponKind[] k_backAllowed = { WeaponKind.Sword, WeaponKind.Spear, WeaponKind.Bow };
        private static readonly WeaponKind[] k_hipAllowed = { WeaponKind.Sword };

        [Tooltip("등 슬롯 앵커 (검/창/활)")]
        [SerializeField] private Transform m_backAnchor;
        [Tooltip("허리 왼쪽 앵커 (검 전용)")]
        [SerializeField] private Transform m_hipLeftAnchor;
        [Tooltip("허리 오른쪽 앵커 (검 전용)")]
        [SerializeField] private Transform m_hipRightAnchor;
        [Tooltip("오른손잡이 기준 배치 — 주 허리 슬롯 = 오른쪽 (지시서: 설정값, 기본 오른손잡이)")]
        [SerializeField] private bool m_rightHanded = true;

        private readonly GameObject[] m_stowed = new GameObject[3];
        private readonly System.Collections.Generic.List<MeleeWeapon> m_registeredMelee = new System.Collections.Generic.List<MeleeWeapon>();
        private readonly System.Collections.Generic.List<Bow> m_registeredBows = new System.Collections.Generic.List<Bow>();

        /// <summary>파손 무기 강제 귀환 발생 — (무기 루트, 슬롯). UI/사운드 훅.</summary>
        public event Action<GameObject, HolsterSlot> BrokenWeaponReturned;

        public void Configure(Transform back, Transform hipLeft, Transform hipRight, bool rightHanded = true)
        {
            m_backAnchor = back;
            m_hipLeftAnchor = hipLeft;
            m_hipRightAnchor = hipRight;
            m_rightHanded = rightHanded;
        }

        public GameObject GetStowed(HolsterSlot slot) => m_stowed[(int)slot];

        /// <summary>수납 — 허용 타입의 빈 슬롯에만. 등 우선, 검은 주손 허리 → 반대 허리 → 등 순.</summary>
        public bool TryStow(GameObject weaponRoot, WeaponKind kind)
        {
            if (weaponRoot == null) return false;

            foreach (HolsterSlot slot in PreferredOrder(kind))
            {
                if (!IsAllowed(slot, kind) || m_stowed[(int)slot] != null) continue;
                Stow(weaponRoot, slot);
                return true;
            }
            return false;
        }

        /// <summary>인출 — 파손(사용 불가) 무기는 거부 (§1-5 집기 거부).</summary>
        public bool TryWithdraw(HolsterSlot slot, out GameObject weapon)
        {
            weapon = m_stowed[(int)slot];
            if (weapon == null) return false;
            if (IsBroken(weapon)) { weapon = null; return false; }

            m_stowed[(int)slot] = null;
            weapon.transform.SetParent(null, true);
            SetPhysicsEnabled(weapon, true);
            return true;
        }

        /// <summary>파손 무기 자동 귀환 구독 (§1-5). 구독 해제는 UnregisterWeapon/OnDestroy(일괄).</summary>
        public void RegisterWeapon(MeleeWeapon melee)
        {
            melee.WeaponBroken -= OnMeleeBroken;
            melee.WeaponBroken += OnMeleeBroken;
            if (!m_registeredMelee.Contains(melee)) m_registeredMelee.Add(melee);
        }

        public void RegisterWeapon(Bow bow)
        {
            bow.WeaponBroken -= OnBowBroken;
            bow.WeaponBroken += OnBowBroken;
            if (!m_registeredBows.Contains(bow)) m_registeredBows.Add(bow);
        }

        public void UnregisterWeapon(MeleeWeapon melee)
        {
            melee.WeaponBroken -= OnMeleeBroken;
            m_registeredMelee.Remove(melee);
        }

        public void UnregisterWeapon(Bow bow)
        {
            bow.WeaponBroken -= OnBowBroken;
            m_registeredBows.Remove(bow);
        }

        private void OnDestroy()
        {
            // 구독 잔존 방지 — 파괴된 홀스터의 핸들러가 무기 파손 이벤트로 호출되는 것 차단 (rules/scripts.md)
            foreach (var melee in m_registeredMelee)
                if (melee != null) melee.WeaponBroken -= OnMeleeBroken;
            foreach (var bow in m_registeredBows)
                if (bow != null) bow.WeaponBroken -= OnBowBroken;
            m_registeredMelee.Clear();
            m_registeredBows.Clear();
        }

        private void OnMeleeBroken(MeleeWeapon weapon) => ForceReturn(weapon.gameObject, weapon.Data != null ? weapon.Data.kind : WeaponKind.Sword);
        private void OnBowBroken(Bow bow) => ForceReturn(bow.gameObject, WeaponKind.Bow);

        /// <summary>내구도 0 강제 귀환 — 빈 허용 슬롯 우선, 전부 차 있으면 등에 겹쳐서라도 귀환 (드랍 방지).</summary>
        public void ForceReturn(GameObject weaponRoot, WeaponKind kind)
        {
            HolsterSlot target = HolsterSlot.Back;
            foreach (HolsterSlot slot in PreferredOrder(kind))
            {
                if (IsAllowed(slot, kind) && m_stowed[(int)slot] == null) { target = slot; break; }
            }

            Stow(weaponRoot, target);
            SetCollidersEnabled(weaponRoot, false); // 사용 불가 — 집기 거부 (수리 시 Blacksmith가 복구)
            Debug.Log($"[WeaponHolster] broken weapon returned to {target} — {weaponRoot.name}");
            BrokenWeaponReturned?.Invoke(weaponRoot, target);
        }

        /// <summary>수리 완료 후 사용 가능 상태 복구 — Blacksmith가 호출.</summary>
        public static void RestoreUsable(GameObject weaponRoot)
        {
            SetCollidersEnabled(weaponRoot, true);
        }

        private void Stow(GameObject weaponRoot, HolsterSlot slot)
        {
            Transform anchor = AnchorOf(slot);
            m_stowed[(int)slot] = weaponRoot;

            weaponRoot.transform.SetParent(anchor, false);
            weaponRoot.transform.localPosition = Vector3.zero;
            weaponRoot.transform.localRotation = Quaternion.identity;
            SetPhysicsEnabled(weaponRoot, false);

            // 순간이동 통지 — 유령 스윕 차단 규약 (step-02)
            weaponRoot.GetComponent<MeleeWeapon>()?.NotifyTeleported();
        }

        // 검은 주손 허리 우선(빠른 인출), 창/활은 등 전용
        private HolsterSlot[] PreferredOrder(WeaponKind kind)
        {
            if (kind == WeaponKind.Sword)
            {
                return m_rightHanded
                    ? new[] { HolsterSlot.HipRight, HolsterSlot.HipLeft, HolsterSlot.Back }
                    : new[] { HolsterSlot.HipLeft, HolsterSlot.HipRight, HolsterSlot.Back };
            }
            return new[] { HolsterSlot.Back };
        }

        private static bool IsAllowed(HolsterSlot slot, WeaponKind kind)
        {
            WeaponKind[] allowed = slot == HolsterSlot.Back ? k_backAllowed : k_hipAllowed;
            return Array.IndexOf(allowed, kind) >= 0;
        }

        private Transform AnchorOf(HolsterSlot slot)
        {
            switch (slot)
            {
                case HolsterSlot.HipLeft: return m_hipLeftAnchor != null ? m_hipLeftAnchor : transform;
                case HolsterSlot.HipRight: return m_hipRightAnchor != null ? m_hipRightAnchor : transform;
                default: return m_backAnchor != null ? m_backAnchor : transform;
            }
        }

        private static bool IsBroken(GameObject weaponRoot)
        {
            var melee = weaponRoot.GetComponent<MeleeWeapon>();
            if (melee != null && melee.IsBroken) return true;
            var bow = weaponRoot.GetComponent<Bow>();
            return bow != null && bow.IsBroken;
        }

        private static void SetPhysicsEnabled(GameObject weaponRoot, bool enabled)
        {
            var rb = weaponRoot.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = !enabled; // 수납 중 낙하 방지 (1회 구성 — RULE-03 조작 아님)
        }

        private static void SetCollidersEnabled(GameObject weaponRoot, bool enabled)
        {
            foreach (var col in weaponRoot.GetComponentsInChildren<Collider>())
                col.enabled = enabled;
        }
    }
}
