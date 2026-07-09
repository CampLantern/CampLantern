using UnityEngine;

namespace CampLantern.Combat
{
    /// <summary>
    /// 히트 판정 결과 — 무기(근접/화살)가 산출해 <see cref="IDamageable.ApplyDamage"/>로 전달한다.
    /// 약점 배율은 여기서 곱하지 않는다: 배율 데이터(weakpointMultiplier)의 소유자가 몬스터이고,
    /// 네트워크 권한 분리(step-12) 시 "클라 감지 → 호스트가 데미지 최종 확정" 경계와 일치시키기 위함
    /// (design/combat-detailed/README.md 아키텍처 결정).
    /// Spec: 지시서 2단계 (§1-2, §1-3)
    /// </summary>
    public struct HitInfo
    {
        /// <summary>무기 산출 기본 데미지 (약점 배율 미적용 — 배율은 수신 측 소관).</summary>
        public int BaseDamage;

        /// <summary>약점(머리) 볼륨 적중 여부 — 어그로(step-05)가 소비.</summary>
        public bool IsWeakpoint;

        /// <summary>실측 타격/착탄 속도 (로그·이펙트·햅틱용).</summary>
        public float Speed;

        /// <summary>접촉 지점 (월드 좌표).</summary>
        public Vector3 Point;

        /// <summary>공격 주체 루트 — step-12에서 PlayerRef 직렬화로 확장 예정.</summary>
        public GameObject Attacker;
    }
}
