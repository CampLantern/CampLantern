using CampLantern.Combat.Weapons;
using CampLantern.Core;
using UnityEngine;

namespace CampLantern.Combat.Player
{
    /// <summary>
    /// 수리 스텁 (§1-5) — 코인 차감 + 내구도 복구 **메서드만**. UI/대장간 공간은 추후 (지시서 명시).
    /// 수리 단가는 MeleeWeaponData.repairCost (임시 0 — TODO(SPEC)). 활 수리 단가는 스펙 미정 —
    /// BowData에 필드가 없어 임시 무료 (TODO(SPEC): 정식 스키마 때 부여).
    /// Wallet.TrySpend는 0 이하 금액에 false를 반환하므로 무료(0) 수리는 과금 경로를 타지 않는다.
    /// </summary>
    public class Blacksmith : MonoBehaviour
    {
        public bool TryRepair(MeleeWeapon weapon, Wallet wallet)
        {
            if (weapon == null || weapon.Data == null || !weapon.IsBroken) return false;

            int cost = weapon.Data.repairCost;
            if (cost > 0 && (wallet == null || !wallet.TrySpend(cost))) return false;

            weapon.RepairFull();
            WeaponHolster.RestoreUsable(weapon.gameObject);
            Debug.Log($"[Blacksmith] melee repaired (cost {cost}) — {weapon.name}");
            return true;
        }

        public bool TryRepair(Bow bow, Wallet wallet)
        {
            if (bow == null || bow.Data == null || !bow.IsBroken) return false;

            // TODO(SPEC): 활 수리 단가 미정 — 임시 무료
            bow.RepairFull();
            WeaponHolster.RestoreUsable(bow.gameObject);
            Debug.Log($"[Blacksmith] bow repaired (free — TODO(SPEC)) — {bow.name}");
            return true;
        }
    }
}
