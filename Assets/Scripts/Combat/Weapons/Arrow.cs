using System.Collections.Generic;
using CampLantern.Combat.Data;
using UnityEngine;

namespace CampLantern.Combat.Weapons
{
    /// <summary>
    /// 화살 — 발사/비행/명중/박힘/회수 대상 (§1-4).
    ///
    /// [비행·판정 기법 — 자체 운동학 적분 + 경로 스윕]
    /// 중력 궤적은 v += g·dt 자체 적분으로 유지하고, 명중 감지는 "이전 위치 → 새 위치" SphereCast
    /// (판정 규약: 물리 콜백 금지, 경로 기반 — 빠른 화살 터널링 방지). 물리 엔진 대신 자체 적분을
    /// 선택한 근거: 비행 위치와 판정 경로가 단일 소스가 되고(Update 적분 ↔ FixedUpdate 물리의 위상차
    /// 제거), RULE-03(Rigidbody 조작은 FixedUpdate) 제약 자체가 소거된다. 그랩용 Rigidbody는
    /// Launch에서 isKinematic 1회 구성으로 비활성화(콜라이더도 꺼서 타 화살 스윕 오탐 배제).
    ///
    /// 명중 시 접촉 시점 실측 속도 |v|로 데미지: clamp(|v|, vMin, vMax) × K × (1 + b×W).
    /// 약점 배율은 근접과 동일 — 수신 측(몬스터) 적용, HitInfo.IsWeakpoint만 전달.
    /// 명중 → 대상에 부모 연결로 박힘(사망 후에도 유지·회수 가능 — DeathOverlay는 화살을 파괴하지 않음).
    /// 빗나가 지면 등(비-HitVolume) 접촉 → 소멸.
    /// Spec: 지시서 6단계 (§1-4)
    /// </summary>
    public class Arrow : MonoBehaviour
    {
        [SerializeField] private ArrowData m_data;
        [SerializeField] private CombatBalanceData m_balance;
        [Tooltip("경로 스윕 반경(m) — 화살촉 두께. TODO(TUNING)")]
        [SerializeField] private float m_sweepRadius = 0.02f;
        [Tooltip("비행 수명(초) — 무한 낙하 방지. TODO(TUNING)")]
        [SerializeField] private float m_maxLifetime = 10f;

        // 박힌 화살 전역 목록 — 회수(ArrowQuiver) 스캔용. RULE-01: Domain Reload 비활성 대응 초기화.
        private static readonly List<Arrow> s_stuckArrows = new List<Arrow>();
        public static IReadOnlyList<Arrow> StuckArrows => s_stuckArrows;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_stuckArrows.Clear();

        private static readonly RaycastHit[] s_castBuffer = new RaycastHit[8];

        private Vector3 m_velocity;
        private GameObject m_attacker;
        private float m_lifetime;
        private bool m_flying;

        public ArrowData Data => m_data;
        public CombatBalanceData Balance => m_balance;
        public bool IsStuck { get; private set; }

        /// <summary>Editor 팩토리·런타임 조립용.</summary>
        public void Configure(ArrowData data, CombatBalanceData balance)
        {
            m_data = data;
            m_balance = balance;
        }

        /// <summary>발사 — Bow.TryFire가 호출. v0는 당김 기반 초속 (§1-4).</summary>
        public void Launch(Vector3 origin, Vector3 direction, float v0, GameObject attacker)
        {
            transform.position = origin;
            transform.rotation = Quaternion.LookRotation(direction);
            m_velocity = direction.normalized * v0;
            m_attacker = attacker;
            m_lifetime = m_maxLifetime;
            m_flying = true;
            IsStuck = false;

            // 그랩용 물리 비활성 — 비행은 자체 적분이 소유 (1회 구성, RULE-03 주석 참조)
            var rb = GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;
            foreach (var col in GetComponentsInChildren<Collider>())
                col.enabled = false;
        }

        private void Update()
        {
            if (!m_flying) return;

            float dt = Time.deltaTime;
            m_velocity += Physics.gravity * dt; // 중력 궤적
            Vector3 from = transform.position;
            Vector3 to = from + m_velocity * dt;

            if (TrySweep(from, to, out RaycastHit hit, out HitVolume volume))
            {
                if (volume != null) HitTarget(hit, volume);
                else Destroy(gameObject); // 지면 등 비-HitVolume — 소멸 (§1-4)
                return;
            }

            transform.position = to;
            if (m_velocity.sqrMagnitude > 1e-4f)
                transform.rotation = Quaternion.LookRotation(m_velocity); // 궤적 정렬

            m_lifetime -= dt;
            if (m_lifetime <= 0f) Destroy(gameObject);
        }

        private bool TrySweep(Vector3 from, Vector3 to, out RaycastHit bestHit, out HitVolume bestVolume)
        {
            bestHit = default;
            bestVolume = null;
            Vector3 delta = to - from;
            float dist = delta.magnitude;
            if (dist < 1e-6f) return false;

            int count = Physics.SphereCastNonAlloc(from, m_sweepRadius, delta / dist,
                s_castBuffer, dist, Physics.AllLayers, QueryTriggerInteraction.Collide);

            float nearest = float.MaxValue;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                var h = s_castBuffer[i];
                if (h.collider.GetComponentInParent<Arrow>() == this) continue;                 // 자기 자신 제외
                if (m_attacker != null && h.collider.transform.IsChildOf(m_attacker.transform)) continue; // 사수 제외
                if (h.distance >= nearest) continue;

                nearest = h.distance;
                bestHit = h;
                bestVolume = h.collider.GetComponent<HitVolume>();
                found = true;
            }
            return found;
        }

        private void HitTarget(RaycastHit hit, HitVolume volume)
        {
            float speed = m_velocity.magnitude; // 접촉 시점 실측 속도 (§1-4)

            if (volume.Owner != null && volume.Owner.IsDamageable && m_balance != null && m_data != null)
            {
                int damage = Mathf.RoundToInt(
                    Mathf.Clamp(speed, m_balance.arrowVMin, m_balance.arrowVMax)
                    * m_balance.arrowDamageK * (1f + m_balance.arrowWeightBonusB * m_data.weight));

                var info = new HitInfo
                {
                    BaseDamage = damage,
                    IsWeakpoint = volume.IsWeakpoint,
                    Speed = speed,
                    Point = hit.point,
                    Attacker = m_attacker,
                };
                volume.Owner.ApplyDamage(in info);
            }

            // 박힘 — 대상에 부모 연결 (몬스터 사망 후에도 유지, 회수 가능)
            m_flying = false;
            IsStuck = true;
            transform.position = hit.point;
            transform.SetParent(hit.collider.transform, worldPositionStays: true);
            s_stuckArrows.Add(this);
        }

        /// <summary>회수 — ArrowQuiver가 호출. 박힌 오브젝트 제거.</summary>
        public void RemoveFromWorld()
        {
            Destroy(gameObject); // OnDestroy가 s_stuckArrows 정리
        }

        private void OnDestroy()
        {
            s_stuckArrows.Remove(this);
        }
    }
}
