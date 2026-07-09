using UnityEngine;

namespace CampLantern.Combat
{
    /// <summary>
    /// 피격 볼륨 마커 — 몬스터의 몸통/머리 등 부위 콜라이더에 붙여 "쿼리 대상"임을 선언한다.
    /// 무기의 능동 쿼리(스윕/오버랩)가 콜라이더를 맞히면 이 컴포넌트로 (수신자, 약점 여부)를 해석한다.
    ///
    /// 약점(머리) 식별을 별도 자식 볼륨으로 한 근거: 본(bone) 기준 반경 방식은 아트(리깅) 유무에
    /// 의존하지만, 볼륨 방식은 플레이스홀더 프리미티브와 실제 아트(곰 Bear_4) 모두에서 동일하게
    /// 동작하고, 부위별 판정("첫 접촉 부위만 인정" §1-2)이 콜라이더 단위로 자연스럽게 떨어진다.
    ///
    /// 판정 규약: 콜라이더는 쿼리 대상 전용 — 물리 충돌에 참여하지 않도록 Awake에서 isTrigger 강제
    /// (rules/scripts.md — 컴포넌트 초기값은 코드로 확정).
    /// Spec: 지시서 2단계 (§1-2 약점)
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class HitVolume : MonoBehaviour
    {
        [SerializeField] private bool m_isWeakpoint;

        private IDamageable m_owner;

        public bool IsWeakpoint => m_isWeakpoint;

        /// <summary>이 볼륨의 데미지 수신자 — 부모 계층에서 IDamageable을 찾는다 (Awake 캐싱).</summary>
        public IDamageable Owner => m_owner;

        /// <summary>Editor 팩토리/런타임 조립용 — 직렬화 필드 주입.</summary>
        public void Configure(bool isWeakpoint)
        {
            m_isWeakpoint = isWeakpoint;
        }

        private void Awake()
        {
            m_owner = GetComponentInParent<IDamageable>();

            var col = GetComponent<Collider>();
            col.isTrigger = true; // 쿼리 대상 전용 — 물리 충돌 비참여 (판정 규약)
        }
    }
}
