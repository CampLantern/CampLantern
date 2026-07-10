using CampLantern.Combat.Data;
using UnityEngine;

namespace CampLantern.Combat.Monsters
{
    /// <summary>
    /// 어그로 시스템 — MonsterController가 타겟을 여기서 조회한다 (지시서 4단계, §4-3 규칙 6개).
    ///
    /// 1. 기본 타겟팅: 가장 가까운 플레이어. 재계산 트리거는 "새로운 유효타가 들어올 때"
    ///    (MonsterHealth.Damaged 구독 — 무효타는 ApplyDamage에 도달하지 않으므로 자동 배제).
    /// 2. 히스테리시스: 현재 타겟보다 aggroHysteresisMeters(임시 2m) 이상 가까워야 전환.
    /// 3. 약점 고정: 약점 유효타 시 해당 공격자에게 weakpointLockSeconds(임시 5s) 고정.
    ///    같은 공격자의 약점 재적중은 고정 갱신(규칙 4가 "다른 플레이어"만 감쇄로 명시).
    /// 4. 감쇄: 고정 중 다른 플레이어의 약점 타격 시 잔여 -weakpointDecaySeconds(임시 1.5s).
    /// 5. 감쇄 소진: 잔여 ≤ 0이 되면 그렇게 만든 마지막 공격자에게 신규 고정.
    ///    (시간 경과로 자연 만료되면 고정만 풀리고 타겟은 유지 — 다음 유효타 재계산까지. 규칙 1의 트리거 정의.)
    /// 6. 리셋: 가장 가까운 플레이어가 추격 반경 밖 → 무조건 즉시 리셋(고정보다 우선) — 반경 판정은
    ///    ChaseState가 하고 ReturnState.Enter가 ResetAggro()를 호출한다. 타겟 다운 시 즉시 재계산(NotifyTargetDowned —
    ///    다운 시스템은 step-09, 지금은 훅만).
    ///
    /// Attacker는 GameObject(현 단계: 무기/봇 루트) — 플레이어 신원 매핑 정밀화는 step-10(홀스터 소지자)/step-12(PlayerRef).
    /// Spec: 지시서 4단계 (§4-3)
    /// </summary>
    [RequireComponent(typeof(MonsterHealth))]
    public class AggroController : MonoBehaviour
    {
        [SerializeField] private MonsterData m_data;
        [Header("디버그 (읽기 전용)")]
        [Tooltip("현재 타겟 이름")] [SerializeField] private string m_debugTarget = "(none)";
        [Tooltip("약점 고정 잔여(초)")] [SerializeField] private float m_debugLockRemaining;

        private MonsterHealth m_health;
        private Transform m_target;
        private Transform m_lockTarget;   // null = 고정 없음
        private float m_lockRemaining;

        public Transform CurrentTarget => m_target;
        public bool IsLocked => m_lockTarget != null && m_lockRemaining > 0f;
        public float LockRemainingSeconds => IsLocked ? m_lockRemaining : 0f;

        /// <summary>Editor 팩토리·런타임 조립용 데이터 주입.</summary>
        public void Configure(MonsterData data)
        {
            m_data = data;
        }

        private void Awake()
        {
            m_health = GetComponent<MonsterHealth>();
            if (m_data == null) m_data = m_health.Data;

            m_health.Damaged -= OnDamaged;
            m_health.Damaged += OnDamaged;

            // 타겟 다운 시 즉시 재계산 (규칙 6 후반) — step-09 다운 시스템의 전역 이벤트 구독
            Player.PlayerHealth.AnyPlayerDowned -= OnAnyPlayerDowned;
            Player.PlayerHealth.AnyPlayerDowned += OnAnyPlayerDowned;
        }

        private void OnDestroy()
        {
            if (m_health != null) m_health.Damaged -= OnDamaged;
            Player.PlayerHealth.AnyPlayerDowned -= OnAnyPlayerDowned;
        }

        private void OnAnyPlayerDowned(Player.PlayerHealth player)
        {
            NotifyTargetDowned(player.gameObject);
        }

        private void Update()
        {
            // 고정 자연 만료 — 고정만 풀리고 타겟은 유지 (규칙 1: 재계산은 유효타 트리거)
            if (m_lockTarget != null)
            {
                m_lockRemaining -= Time.deltaTime;
                if (m_lockRemaining <= 0f)
                {
                    m_lockTarget = null;
                    m_lockRemaining = 0f;
                }
            }

            m_debugTarget = m_target != null ? m_target.name : "(none)";
            m_debugLockRemaining = LockRemainingSeconds;
        }

        private void OnDamaged(HitInfo hit)
        {
            NotifyValidHit(hit.Attacker, hit.IsWeakpoint);
        }

        /// <summary>유효타 수신 — 재계산 트리거(규칙 1) + 약점 고정/감쇄/소진(규칙 3~5).</summary>
        public void NotifyValidHit(GameObject attacker, bool isWeakpoint)
        {
            Transform attackerT = attacker != null ? attacker.transform : null;

            if (isWeakpoint && attackerT != null)
            {
                if (IsLocked && m_lockTarget != attackerT)
                {
                    m_lockRemaining -= Data().weakpointDecaySeconds;      // 규칙 4
                    if (m_lockRemaining <= 0f) Lock(attackerT);            // 규칙 5 — 마지막 공격자에게 신규 고정
                }
                else
                {
                    Lock(attackerT);                                       // 규칙 3 (신규/동일 공격자 갱신)
                }
            }

            if (IsLocked)
            {
                m_target = m_lockTarget;   // 고정 중 — 근접 재계산 무시
                return;
            }

            RecomputeByProximity();        // 규칙 1 + 2
        }

        /// <summary>Chase 진입 시 초기 타겟 획득 — 규칙 1의 트리거(유효타) 밖의 최초 1회.</summary>
        public void AcquireIfNone()
        {
            if (m_target == null)
                m_target = CombatPlayers.FindNearest(transform.position);
        }

        /// <summary>규칙 6 — 추격 반경 이탈 시 무조건 즉시 리셋 (약점 고정보다 우선). ReturnState.Enter가 호출.</summary>
        public void ResetAggro()
        {
            m_target = null;
            m_lockTarget = null;
            m_lockRemaining = 0f;
        }

        /// <summary>규칙 6 후반 — 타겟 다운 시 즉시 재계산. 다운 시스템(step-09)이 호출, 지금은 훅만.</summary>
        public void NotifyTargetDowned(GameObject player)
        {
            if (player == null) return;
            Transform t = player.transform;

            if (m_lockTarget == t)
            {
                m_lockTarget = null;
                m_lockRemaining = 0f;
            }
            if (m_target == t)
            {
                m_target = CombatPlayers.FindNearest(transform.position); // 즉시 재계산
            }
        }

        private void Lock(Transform target)
        {
            m_lockTarget = target;
            m_lockRemaining = Data().weakpointLockSeconds;
            m_target = target;
        }

        private void RecomputeByProximity()
        {
            Transform nearest = CombatPlayers.FindNearest(transform.position);
            if (nearest == null) return;

            if (m_target == null)
            {
                m_target = nearest;
                return;
            }

            // 규칙 2 — 히스테리시스: 현재 타겟보다 X미터 이상 가까워야 전환 (프레임 단위 타겟 튐 방지)
            float currentDist = PlanarDistance(m_target.position);
            float candidateDist = PlanarDistance(nearest.position);
            if (candidateDist <= currentDist - Data().aggroHysteresisMeters)
                m_target = nearest;
        }

        private float PlanarDistance(Vector3 position)
        {
            Vector3 to = position - transform.position;
            to.y = 0f;
            return to.magnitude;
        }

        private MonsterData Data()
        {
            return m_data != null ? m_data : m_health.Data;
        }

#if UNITY_EDITOR
        // 디버그 기즈모 — 감지(노랑)/추격(빨강) 반경, 타겟 라인(고정=마젠타, 일반=시안)
        private void OnDrawGizmosSelected()
        {
            var data = m_data != null ? m_data : GetComponent<MonsterHealth>()?.Data;
            if (data != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(transform.position, data.detectRadius);
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(transform.position, data.chaseRadius);
            }
            if (m_target != null)
            {
                Gizmos.color = IsLocked ? Color.magenta : Color.cyan;
                Gizmos.DrawLine(transform.position + Vector3.up, m_target.position + Vector3.up);
            }
        }
#endif
    }
}
