using System;
using UnityEngine;

namespace CampLantern.Fishing
{
    /// <summary>
    /// 물고기 FSM 본체 (§3-1). 개체 스탯(FishInstance)을 들고 자신의 체력·텐션을 소유한다.
    /// 파이팅 진행의 주체는 이 클래스 — 낚싯대(FishingRod)는 입력·미끼·내구도 컨트롤러다.
    ///
    /// step-02: 뼈대만 — 상태/줄색/이벤트/전이 훅. 상태별 동작은 후속 단계에서 채운다:
    ///   step-03 Idle/Approach/Bite, step-04 FightNormal, step-05 FightEscape/FightShake, step-06 Hooked/Caught.
    /// </summary>
    public class Fish : MonoBehaviour
    {
        [Tooltip("낚시 튜닝 값 묶음 — 스포너(FishingSpot)가 공유 인스턴스를 주입할 수도 있다")]
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

        private void Awake()
        {
            // 초기 상태는 코드로 확정 (rules/scripts.md — Inspector 의존 금지)
            State = FishState.Idle;
            Line  = LineColor.None;
        }

        /// <summary>스포너(FishingSpot)가 개체를 주입한다. 체력은 개체 최대치로 시작.</summary>
        public void Initialize(FishInstance instance)
        {
            Instance = instance;
            Health   = instance.MaxHealth;
            State    = FishState.Idle;
            Line     = LineColor.None;
        }

        /// <summary>튜닝 공유 주입(스포너용) — null이면 무시.</summary>
        public void SetTuning(FishingTuning tuning)
        {
            if (tuning != null) m_tuning = tuning;
        }

        // ── 전이 훅 — 같은 값이면 무시하고, 바뀔 때만 이벤트 발화 ──

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

        // step-03에서 구현: BeginApproach / OnChamjil / Bite 윈도우 타이머 / ResetToIdle(§3-2)
        // step-04에서 구현: FightNormal 릴링 체력 감소(§7-1), 텐션 초기화(§7-2)
        // step-05에서 구현: 도망 간격 타이머(§7-3), FightEscape/FightShake(§7-4/7-5), 줄 끊김
        // step-06에서 구현: Hooked(초록)/Hook()/Caught
    }
}
