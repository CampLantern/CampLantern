namespace CampLantern.Combat
{
    /// <summary>
    /// 데미지 적용 단일 지점 — 모든 데미지는 이 인터페이스 한 곳으로 모은다 (지시서 규칙 5).
    /// 이 지점이 step-12 네트워크 권한 분리 경계다: 로컬 단계에서는 즉시 적용,
    /// 네트워크 통합 시 "비권한 클라 → RPC → State Authority 적용"으로 분리된다
    /// (참조 모델: Hunting/HuntTarget.ApplyHit).
    /// </summary>
    public interface IDamageable
    {
        /// <summary>false면 데미지 무시 (Return 상태 무적 등). 공격 측은 판정 자체를 스킵해도 된다.</summary>
        bool IsDamageable { get; }

        void ApplyDamage(in HitInfo hit);
    }
}
