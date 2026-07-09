namespace CampLantern.Combat
{
    /// <summary>
    /// 무기 종별 — 홀스터 슬롯 허용 검사(step-10, §1-6)에 사용.
    /// 등 슬롯: Sword/Spear/Bow 허용, 허리 좌/우 슬롯: Sword 전용.
    /// </summary>
    public enum WeaponKind
    {
        Sword,
        Spear,
        Bow,
    }
}
