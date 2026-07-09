using System;
using UnityEngine;

namespace CampLantern.Fishing
{
    /// <summary>
    /// 물고기 FSM 본체 (§3-1). 개체 스탯(FishInstance)을 들고 자신의 체력·텐션을 소유한다.
    /// 파이팅 진행의 주체는 이 클래스 — 낚싯대(FishingRod)는 입력·미끼·내구도 컨트롤러다.
    ///
    /// 구현 현황: step-03 Idle/Approach/Bite(챔질). FightNormal 본 로직은 step-04,
    /// FightEscape/FightShake는 step-05, Hooked/Caught는 step-06에서 채운다.
    /// 물리 API 미사용(이동은 Transform) — RULE-03 해당 없음. 타이머는 Update 누산(90Hz 경량).
    /// </summary>
    public class Fish : MonoBehaviour
    {
        // 접근 유영 속도(m/s) — 연출값. 재접근 시간이 자연 쿨다운 역할(§3-2)이므로 너무 빠르게 하지 않는다.
        private const float k_approachSpeed = 1.5f;
        private const float k_arriveDistance = 0.05f;

        [Tooltip("낚시 튜닝 값 묶음 — 낚싯대/스포너가 SetTuning으로 공유 인스턴스를 주입할 수 있다")]
        [SerializeField] private FishingTuning m_tuning = new FishingTuning();

        /// <summary>현재 FSM 상태 (§3-1).</summary>
        public FishState State { get; private set; }

        /// <summary>현재 줄 색 (§2) — 파이팅 판단 신호.</summary>
        public LineColor Line { get; private set; }

        /// <summary>캐스팅 시 주입되는 개체 스탯 (공유 품질 롤, §5-3).</summary>
        public FishInstance Instance { get; private set; }

        /// <summary>남은 체력. 초기값 = Instance.MaxHealth. 실패 리셋 시 복원 (§3-2).</summary>
        public float Health { get; private set; }

        /// <summary>남은 텐션(실수 예산). Fight 진입 시 rod.tension으로 초기화 — 이월 없음 (§7-2).</summary>
        public float Tension { get; private set; }

        public FishingTuning Tuning => m_tuning;

        /// <summary>상태 전이 시 발화 (UI/사운드/연출 훅).</summary>
        public event Action<FishState> StateChanged;

        /// <summary>줄 색 변경 시 발화 (줄 렌더·햅틱 훅, §8).</summary>
        public event Action<LineColor> LineColorChanged;

        /// <summary>체력 변경 시 발화 (파이팅 UI 훅).</summary>
        public event Action HealthChanged;

        /// <summary>체력 0 도달 — Hooked 진입 (§3-1).</summary>
        public event Action Hooked;

        /// <summary>낚아올림 성공 — 개체 제거·보상 지급 직전 (§3-1 Caught).</summary>
        public event Action Caught;

        private FishingRod m_rod;          // 현재 이 개체를 낚는 낚싯대 (Approach~Fight 동안만)
        private Vector3 m_homePosition;    // 원래 위치 — 실패 복귀 지점 (§3-2)
        private Vector3 m_baitPoint;       // 미끼 착수 지점 — Approach 목표
        private float m_phaseTimer;        // 현재 상태의 남은 시간 (Bite 윈도우 등)

        private void Awake()
        {
            // 초기 상태는 코드로 확정 (rules/scripts.md — Inspector 의존 금지)
            State = FishState.Idle;
            Line  = LineColor.None;
        }

        /// <summary>스포너(FishingSpot)가 개체를 주입한다. 호출 시점의 위치가 원위치가 된다.</summary>
        public void Initialize(FishInstance instance)
        {
            Instance       = instance;
            Health         = instance.MaxHealth;
            m_homePosition = transform.position;
            State          = FishState.Idle;
            Line           = LineColor.None;
        }

        /// <summary>튜닝 공유 주입(낚싯대/스포너용) — null이면 무시.</summary>
        public void SetTuning(FishingTuning tuning)
        {
            if (tuning != null) m_tuning = tuning;
        }

        private void Update()
        {
            switch (State)
            {
                case FishState.Approach:
                    UpdateApproach();
                    break;

                case FishState.Bite:
                    // 고정 타이밍 윈도우(§2) — 초과 시 챔질 실패
                    m_phaseTimer -= Time.deltaTime;
                    if (m_phaseTimer <= 0f)
                        ResetToIdle(); // §3-2: 원위치 Idle + 체력 리셋 (미끼는 이미 차감 — §4 챔질 실패 비용)
                    break;

                // FightNormal: step-04 / FightEscape·FightShake: step-05 / Hooked: step-06
            }
        }

        // ── Idle → Approach → Bite (step-03) ─────────────────────────

        /// <summary>
        /// 캐스팅 성공 시 낚싯대가 호출 (§3-1 Idle 탈출: 미끼 인지). Idle에서만 유효.
        /// 미끼 착수 지점은 현재 위치에서 낚싯대 쪽으로 1m — 접근 유영이 보이는 최소 연출.
        /// </summary>
        public void BeginApproach(FishingRod rod)
        {
            if (State != FishState.Idle || rod == null) return;

            m_rod          = rod;
            m_homePosition = transform.position;

            Vector3 toRod = rod.transform.position - transform.position;
            toRod.y = 0f;
            m_baitPoint = transform.position + (toRod.sqrMagnitude > 0.001f ? toRod.normalized : Vector3.forward);

            SetState(FishState.Approach);
        }

        private void UpdateApproach()
        {
            transform.position = Vector3.MoveTowards(transform.position, m_baitPoint, k_approachSpeed * Time.deltaTime);
            if (Vector3.Distance(transform.position, m_baitPoint) <= k_arriveDistance)
                EnterBite();
        }

        private void EnterBite()
        {
            // §4: 미끼는 입질(Bite) 발생 시점에 무조건 -1. 부족하면 입질 진행 불가 → 조용히 복귀.
            if (m_rod == null || !m_rod.TryConsumeBait())
            {
                Debug.Log("[Fish] 미끼 없음 — 입질 진행 불가, 원위치 복귀", this);
                ResetToIdle();
                return;
            }

            m_phaseTimer = m_tuning.biteWindowSeconds; // 전 어종 고정 (§2) // 값은 TODO(TUNING) — FishingTuning 참조
            SetState(FishState.Bite);
        }

        /// <summary>
        /// 챔질 입력 (낚싯대가 위임). Bite 윈도우 안이면 성공 → Fight 진입(내구도 -1, §4).
        /// Approach 중이면 조기 회수 = 캐스팅 취소(비용 없음 — 미끼는 Bite 진입 시 차감되므로).
        /// </summary>
        public void OnChamjil()
        {
            switch (State)
            {
                case FishState.Bite:
                    m_rod.ConsumeDurability(); // §4: 내구도는 챔질 성공(Fight 진입) 시점 -1
                    StartFight();
                    break;

                case FishState.Approach:
                    ResetToIdle();
                    break;
            }
        }

        // ── Fight 진입 (본 로직은 step-04~05) ─────────────────────────

        private void StartFight()
        {
            // step-04: 텐션풀 = rod.tension 초기화(§7-2), 릴링 체력 감소(§7-1)
            SetState(FishState.FightNormal);
            SetLine(LineColor.White);
        }

        // ── 실패 처리 통일 규칙 (§3-2) ────────────────────────────────

        /// <summary>챔질 실패·줄 끊김 공통: 원위치 Idle 복귀 + 체력 리셋. 실루엣 유지(재도전 가능).</summary>
        private void ResetToIdle()
        {
            if (Instance != null) Health = Instance.MaxHealth;
            transform.position = m_homePosition;
            SetLine(LineColor.None);

            FishingRod rod = m_rod;
            m_rod = null;
            SetState(FishState.Idle);
            if (rod != null) rod.ClearCurrent(this);
        }

        // ── 전이 훅 ──────────────────────────────────────────────────

        private void SetState(FishState next)
        {
            if (State == next) return;
            State = next;
            StateChanged?.Invoke(next);
        }

        private void SetLine(LineColor color)
        {
            if (Line == color) return;
            Line = color;
            LineColorChanged?.Invoke(color);
        }
    }
}
