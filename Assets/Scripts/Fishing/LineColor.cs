namespace CampLantern.Fishing
{
    /// <summary>
    /// 낚싯줄 색 (§2) — 파이팅의 유일한 판단 신호. 햅틱과 동기화된다 (§8).
    /// </summary>
    public enum LineColor
    {
        None,   // 파이팅 외 (캐스팅 전/입질 대기)
        White,  // Fight-평상 — 릴링해도 안전 (체력 감소)
        Red,    // Fight-도망/비늘털이 — 릴링 시 텐션 소모, "트리거를 뗀다"
        Green,  // Hooked — 낚아올려라
    }
}
