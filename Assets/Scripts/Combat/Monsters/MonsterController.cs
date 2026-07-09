using System;
using CampLantern.Combat.Data;
using UnityEngine;

namespace CampLantern.Combat.Monsters
{
    /// <summary>
    /// 몬스터 메인 FSM 소유자 — 상태 패턴 (Idle/Chase/Return, Attack/Exhausted는 step-06 추가).
    /// 오버레이 레이어(Stunned/Death)는 메인 상태와 독립 — PausesMainState면 메인 Tick 정지.
    ///
    /// [이동 방식 결정 — NavMeshAgent 미사용, 평면 직선 스티어링]
    /// 근거: 사냥터 존은 작고 평탄(현 사냥터 씬 = 평면)해 장애물 회피가 불필요하고, NavMesh를 쓰면
    /// 베이크가 씬 전제조건이 되어 임의 씬 무인 검증(봇 주입)과 씬 팩토리가 무거워진다.
    /// 장애물 지형 도입 시 이 클래스의 MoveTowards 한 곳만 NavMeshAgent 경유로 이관하면 된다.
    /// RULE-03 정합: Rigidbody 미사용 — Transform 이동은 물리 API가 아니다.
    ///
    /// 타겟팅은 임시(가장 가까운 플레이어, CombatPlayers) — step-05 AggroController 조회로 교체 예정.
    /// Spec: 지시서 3단계 (§4-4)
    /// </summary>
    [RequireComponent(typeof(MonsterHealth))]
    public class MonsterController : MonoBehaviour
    {
        [SerializeField] private MonsterData m_data;
        [Tooltip("현재 메인 상태 (인스펙터 확인용 — 지시서 명시)")]
        [SerializeField] private string m_stateName = "(none)";
        [Tooltip("Idle 배회 도착 후 대기 시간 범위(초). TODO(TUNING)")]
        [SerializeField] private float m_idleWaitMin = 1f;
        [SerializeField] private float m_idleWaitMax = 3f;
        [Tooltip("이동 도착 판정 거리(m). TODO(TUNING)")]
        [SerializeField] private float m_arriveRadius = 0.3f;
        [Tooltip("회전 속도(deg/s). TODO(TUNING)")]
        [SerializeField] private float m_turnSpeed = 360f;

        private MonsterHealth m_health;
        private IMonsterState m_current;
        private IOverlayState m_overlay;

        public MonsterData Data => m_data;
        public MonsterHealth Health => m_health;
        public Vector3 SpawnPosition { get; private set; }
        public Transform CurrentTarget { get; private set; }
        public string CurrentStateName => m_stateName;
        public IOverlayState CurrentOverlay => m_overlay;
        public float IdleWaitMin => m_idleWaitMin;
        public float IdleWaitMax => m_idleWaitMax;

        /// <summary>메인 상태 전이 시 (이전, 다음). step-07 애니 드라이버 구독 지점. 구독 해제 필수.</summary>
        public event Action<IMonsterState, IMonsterState> StateChanged;

        /// <summary>Editor 팩토리·런타임 조립용 — MonsterHealth.Configure와 함께 호출할 것.</summary>
        public void Configure(MonsterData data)
        {
            m_data = data;
        }

        private void Awake()
        {
            m_health = GetComponent<MonsterHealth>();
            SpawnPosition = transform.position;

            m_health.Died -= OnDied;
            m_health.Died += OnDied;
        }

        private void Start()
        {
            // 데이터 주입(Configure)이 Awake 이후일 수 있어 상태 진입은 Start에서 (step-02 조립 순서 교훈)
            if (m_current == null && m_data != null)
                TransitionTo(new IdleState());
        }

        private void OnDestroy()
        {
            if (m_health != null) m_health.Died -= OnDied;
        }

        private void Update()
        {
            if (m_data == null) return;

            float dt = Time.deltaTime;

            if (m_overlay != null)
            {
                m_overlay.Tick(this, dt);
                if (m_overlay != null && m_overlay.PausesMainState) return; // 메인 상태 정지
            }

            // 런타임 조립에서 Start 이전에 Update가 오는 경우 대비 — 첫 틱에 Idle 진입
            if (m_current == null) TransitionTo(new IdleState());

            m_current.Tick(this, dt);
        }

        public void TransitionTo(IMonsterState next)
        {
            IMonsterState prev = m_current;
            prev?.Exit(this); // 중단 포함 항상 Exit — 판정 잔류 방지 계약 (step-06)

            m_current = next;
            m_stateName = next != null ? next.Name : "(none)";
            next?.Enter(this);

            StateChanged?.Invoke(prev, next);
        }

        /// <summary>오버레이 적용 — Death가 최우선(적용 중이면 다른 오버레이 무시). 기존 오버레이는 교체.</summary>
        public void ApplyOverlay(IOverlayState overlay)
        {
            if (m_overlay is DeathOverlay) return;

            m_overlay?.Remove(this);
            m_overlay = overlay;
            m_overlay?.Apply(this);
        }

        public void RemoveOverlay()
        {
            m_overlay?.Remove(this);
            m_overlay = null;
        }

        /// <summary>모든 메인 상태 즉시 종료 (DeathOverlay 전용 — Exit 보장).</summary>
        public void HaltMainState()
        {
            IMonsterState prev = m_current;
            prev?.Exit(this);
            m_current = null;
            m_stateName = "Dead";
            StateChanged?.Invoke(prev, null);
        }

        public void SetCurrentTarget(Transform target)
        {
            CurrentTarget = target;
        }

        /// <summary>임시 타겟 탐색 — step-05에서 AggroController 조회로 교체.</summary>
        public Transform FindNearestPlayer()
        {
            return CombatPlayers.FindNearest(transform.position);
        }

        /// <summary>평면(XZ) 직선 스티어링 — 이동·회전 유일 지점 (NavMesh 이관 시 여기만 교체).</summary>
        public void MoveTowards(Vector3 destination, float speed)
        {
            Vector3 to = destination - transform.position;
            to.y = 0f;
            float dist = to.magnitude;
            if (dist < 1e-4f) return;

            Vector3 dir = to / dist;
            float step = Mathf.Min(speed * Time.deltaTime, dist);
            transform.position += dir * step;

            Quaternion face = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, face, m_turnSpeed * Time.deltaTime);
        }

        public bool HasArrived(Vector3 destination)
        {
            Vector3 to = destination - transform.position;
            to.y = 0f;
            return to.magnitude <= m_arriveRadius;
        }

        public float PlanarDistanceTo(Vector3 position)
        {
            Vector3 to = position - transform.position;
            to.y = 0f;
            return to.magnitude;
        }

        private void OnDied()
        {
            ApplyOverlay(new DeathOverlay());
        }
    }
}
