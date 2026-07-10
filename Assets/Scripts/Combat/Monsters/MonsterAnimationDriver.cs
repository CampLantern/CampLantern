using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CampLantern.Combat.Monsters
{
    /// <summary>
    /// FSM/피격/사망 이벤트 → Animator 파라미터 구동 (design/combat-detailed step-07). **순수 비주얼 계층** —
    /// 판정은 전부 데이터(hit window)로 완결되어 있고(§4-6), 이 컴포넌트를 제거해도 전투는 동일하게 동작한다.
    /// 애니메이션 이벤트(AnimationEvent)는 사용하지 않는다 (지시서 명시).
    ///
    /// [구동 방식 — 배타적 bool 스위칭] BearAnimator.controller의 파라미터는 전부 Bool(m_Type 4, 실측)이라
    /// SetTrigger가 아니라 "대상만 true, 관리 대상 나머지 false"로 전환한다. Animator가 없거나(플레이스홀더
    /// 프리미티브) 파라미터가 없으면 조용히 no-op — 파라미터 목록은 Awake에서 1회 캐싱.
    ///
    /// 피격 반응은 잠깐 재생 후 현재 상태 파라미터로 복귀(ReassertAfter). 사망 후에는 재생/복귀 없음.
    /// Spec: 지시서 3단계(애니 연계)·5단계(클립 동기화 — Sync 메뉴는 CombatMonsterPrefabFactory)
    /// </summary>
    public class MonsterAnimationDriver : MonoBehaviour
    {
        [SerializeField] private Animator m_animator;

        [Header("파라미터 매핑 (BearAnimator 기본값 — 다른 몬스터는 인스펙터/팩토리에서 교체)")]
        [SerializeField] private string m_idleParam = "Idle";
        [SerializeField] private string m_walkParam = "WalkForward";
        [SerializeField] private string m_chaseParam = "Run Forward";
        [SerializeField] private string m_returnParam = "WalkForward";
        [SerializeField] private string m_exhaustedParam = "Combat Idle";
        [SerializeField] private string m_stunnedParam = "Stunned Loop";
        [SerializeField] private string m_deathParam = "Death";
        [SerializeField] private string m_hitParam = "Get Hit Front";
        [Tooltip("스킬 인덱스별 공격 파라미터 — 스킬 수보다 적으면 순환")]
        [SerializeField] private string[] m_attackParams = { "Attack1" };

        [Header("animDuration 동기화 — 스킬 인덱스별 클립 (Tools > Make Assets > Combat — Sync Skill Anim Durations가 읽음)")]
        [SerializeField] private AnimationClip[] m_attackClips;

        [Tooltip("피격 반응 후 현재 상태 파라미터 복귀까지(초). TODO(TUNING)")]
        [SerializeField] private float m_hitReassertSeconds = 0.6f;

        private MonsterController m_controller;
        private MonsterHealth m_health;
        private readonly HashSet<string> m_availableParams = new HashSet<string>();
        private readonly List<string> m_managedParams = new List<string>();
        private string m_currentParam;
        private string m_lastIdleLocomotion;
        private Coroutine m_reassert;

        public AnimationClip[] AttackClips => m_attackClips;

        /// <summary>Editor 팩토리용 배선 — Visual의 Animator·동기화 클립 주입.</summary>
        public void Configure(Animator animator, AnimationClip[] attackClips)
        {
            m_animator = animator;
            m_attackClips = attackClips;
        }

        private void Awake()
        {
            m_controller = GetComponent<MonsterController>();
            m_health = GetComponent<MonsterHealth>();
            if (m_animator == null) m_animator = GetComponentInChildren<Animator>();

            if (m_animator != null)
                foreach (var p in m_animator.parameters)
                    m_availableParams.Add(p.name);

            CollectManaged(m_idleParam); CollectManaged(m_walkParam); CollectManaged(m_chaseParam);
            CollectManaged(m_returnParam); CollectManaged(m_exhaustedParam); CollectManaged(m_stunnedParam);
            CollectManaged(m_deathParam); CollectManaged(m_hitParam);
            if (m_attackParams != null)
                foreach (var a in m_attackParams) CollectManaged(a);

            if (m_controller != null)
            {
                m_controller.StateChanged -= OnStateChanged;
                m_controller.StateChanged += OnStateChanged;
                m_controller.OverlayChanged -= OnOverlayChanged;
                m_controller.OverlayChanged += OnOverlayChanged;
            }
            if (m_health != null)
            {
                m_health.Damaged -= OnDamaged;
                m_health.Damaged += OnDamaged;
                m_health.Died -= OnDied;
                m_health.Died += OnDied;
            }
        }

        private void OnDestroy()
        {
            if (m_controller != null)
            {
                m_controller.StateChanged -= OnStateChanged;
                m_controller.OverlayChanged -= OnOverlayChanged;
            }
            if (m_health != null)
            {
                m_health.Damaged -= OnDamaged;
                m_health.Died -= OnDied;
            }
        }

        private void Update()
        {
            // Idle 보정 — 배회 이동 중이면 걷기, 대기 중이면 Idle (상태 이벤트만으로는 구분 불가한 비주얼 폴리시)
            if (m_animator == null || m_controller == null || IsDead()) return;
            if (m_controller.CurrentStateName != "Idle") return;

            // 배회 여부는 프레임 이동량으로 추정 (드라이버 자체 판단 — FSM에 역의존 없음)
            string want = (transform.position - m_lastPos).sqrMagnitude > 1e-6f ? m_walkParam : m_idleParam;
            if (want != m_lastIdleLocomotion)
            {
                m_lastIdleLocomotion = want;
                ApplyExclusive(want);
            }
            m_lastPos = transform.position;
        }

        private Vector3 m_lastPos;

        private void OnStateChanged(IMonsterState prev, IMonsterState next)
        {
            if (next == null || IsDead()) return; // null = HaltMainState(사망) — Died가 처리
            m_lastIdleLocomotion = null;

            switch (next)
            {
                case IdleState _:      ApplyExclusive(m_idleParam); break;
                case ChaseState _:     ApplyExclusive(m_chaseParam); break;
                case ReturnState _:    ApplyExclusive(m_returnParam); break;
                case ExhaustedState _: ApplyExclusive(m_exhaustedParam); break;
                case AttackState attack:
                    if (m_attackParams != null && m_attackParams.Length > 0)
                        ApplyExclusive(m_attackParams[attack.SkillIndex % m_attackParams.Length]);
                    break;
            }
        }

        private void OnOverlayChanged(IOverlayState overlay)
        {
            if (IsDead()) return;
            if (overlay is StunnedOverlay)
                ApplyExclusive(m_stunnedParam);
            // 오버레이 해제 복귀는 StunnedOverlay.Remove의 Chase 전이 → StateChanged가 처리
        }

        private void OnDamaged(HitInfo hit)
        {
            if (IsDead()) return;
            string current = m_currentParam;
            ApplyExclusive(m_hitParam);

            if (m_reassert != null) StopCoroutine(m_reassert);
            m_reassert = StartCoroutine(ReassertAfter(current));
        }

        private IEnumerator ReassertAfter(string param)
        {
            yield return new WaitForSeconds(m_hitReassertSeconds);
            if (!IsDead() && m_currentParam == m_hitParam && param != null)
                ApplyExclusive(param);
        }

        private void OnDied()
        {
            ApplyExclusive(m_deathParam);
        }

        // ── 비권한 클라 표현 브리지 (combat-detailed-network step-04) ──
        // 원격에는 FSM이 없으므로(권한자 전용) NetworkedHuntMonster가 [Networked] 상태명/HP 변화를
        // 이 진입점으로 전달한다. 기존 이벤트 구독 경로(권한자/로컬)는 불변.

        /// <summary>상태명 문자열로 구동 — 원격 몬스터 표현. Attack은 스킬 인덱스를 모르므로 첫 파라미터 사용.
        /// Stunned 오버레이는 메인 상태명에 없어 원격 표현 미지원 (TODO(SPEC): 오버레이 동기화 시 확장).</summary>
        public void ApplyStateByName(string stateName)
        {
            if (string.IsNullOrEmpty(stateName)) return;
            m_lastIdleLocomotion = null;

            if (stateName.StartsWith("Attack"))
            {
                if (m_attackParams != null && m_attackParams.Length > 0)
                    ApplyExclusive(m_attackParams[0]);
                return;
            }

            switch (stateName)
            {
                case "Idle": ApplyExclusive(m_idleParam); break;
                case "Chase": ApplyExclusive(m_chaseParam); break;
                case "Return": ApplyExclusive(m_returnParam); break;
                case "Exhausted": ApplyExclusive(m_exhaustedParam); break;
                case "Dead": ApplyExclusive(m_deathParam); break;
            }
        }

        /// <summary>피격 반응 — 원격 몬스터 표현 (NetCurrentHp 감소 감지 시). 로컬 경로(OnDamaged)와 동일 연출.</summary>
        public void ApplyHitReaction()
        {
            string current = m_currentParam;
            ApplyExclusive(m_hitParam);
            if (m_reassert != null) StopCoroutine(m_reassert);
            m_reassert = StartCoroutine(ReassertAfter(current));
        }

        /// <summary>대상 파라미터만 true, 관리 대상 나머지 false (배타적 bool 스위칭 — BearAnimator 파라미터가 Bool이라서).</summary>
        private void ApplyExclusive(string param)
        {
            if (m_animator == null || string.IsNullOrEmpty(param) || !m_availableParams.Contains(param)) return;

            for (int i = 0; i < m_managedParams.Count; i++)
                m_animator.SetBool(m_managedParams[i], m_managedParams[i] == param);
            m_currentParam = param;
            Debug.Log($"[MonsterAnim] param={param} ({name})");
        }

        private void CollectManaged(string param)
        {
            if (!string.IsNullOrEmpty(param) && m_availableParams.Contains(param) && !m_managedParams.Contains(param))
                m_managedParams.Add(param);
        }

        private bool IsDead()
        {
            return m_health != null && m_health.CurrentHp <= 0 && m_health.MaxHp > 0;
        }
    }
}
