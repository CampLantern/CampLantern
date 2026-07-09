using System;
using CampLantern.Core;
using UnityEngine;

namespace CampLantern.Fishing
{
    /// <summary>
    /// 낚싯대 — 입력 진입점 + 미끼/내구도/텐션(RodData) 컨트롤러 (design/fishing-detailed).
    /// 구 버전의 타이밍 FSM(캐스팅→대기→입질)은 물고기 FSM(<see cref="Fish"/>)으로 이관됐다 —
    /// 파이팅 진행은 전부 Fish가 담당하고, 낚싯대는 얇게 유지한다.
    ///
    /// VR 입력(스윙/트리거)은 별도 어댑터(FishingRodInput, step-07)가 이 public API를 호출한다.
    /// </summary>
    public class FishingRod : MonoBehaviour
    {
        [Tooltip("장착 낚싯대 스탯 (§5-2)")]
        [SerializeField] private RodData m_rod = CreateBasicRod(); // TODO(DATA): 장비 시스템 미정 — 임시 하드코딩

        [Tooltip("보유 미끼(갯지렁이 1종 고정, §4)")]
        [SerializeField] private int m_baitCount = 10;             // TODO(DATA): UserData 미정 — 임시 하드코딩

        [Tooltip("낚시 튜닝 값 — 캐스팅한 물고기에 공유 주입")]
        [SerializeField] private FishingTuning m_tuning = new FishingTuning();

        /// <summary>장착 낚싯대 스탯.</summary>
        public RodData Rod => m_rod;

        /// <summary>보유 미끼 수.</summary>
        public int BaitCount => m_baitCount;

        /// <summary>내구도 0 — 수리대 수리 전까지 사용 불가 (§5-2).</summary>
        public bool Broken => m_rod.durability <= 0;

        /// <summary>현재 낚는 중인 물고기 (Approach~Fight). 없으면 null.</summary>
        public Fish CurrentFish { get; private set; }

        public FishingTuning Tuning => m_tuning;

        private void Awake()
        {
            // 직렬화 누락/구버전 씬 대비 — 코드로 확정 (rules/scripts.md)
            if (m_rod == null || string.IsNullOrEmpty(m_rod.rodId))
                m_rod = CreateBasicRod();
            if (m_tuning == null)
                m_tuning = new FishingTuning();
        }

        // §7-2 예시(텐션 10 낚싯대로 힘 10 물고기 → 허용 실수 4초)에 맞춘 기본 낚싯대.
        private static RodData CreateBasicRod() => new RodData
        {
            rodId = "rod_basic", power = 1f, durability = 10, length = 15f, tension = 10f, // TODO(DATA)
        };

        // ── 코어 진입점 (입력 어댑터/하네스가 호출) ──────────────────

        /// <summary>
        /// 캐스팅: 조준한 물고기를 Approach로 전환 (§2-1). 사거리(rod.length) 게이트 (§5-2).
        /// 미끼가 없거나, 내구도 0이거나, 이미 다른 개체를 낚는 중이면 무시.
        /// </summary>
        public void Cast(Fish target)
        {
            if (target == null) return;
            if (Broken)
            {
                Debug.Log("[FishingRod] 내구도 0 — 수리 필요 (§5-2)", this);
                return;
            }
            if (m_baitCount <= 0)
            {
                Debug.Log("[FishingRod] 미끼 없음 — 캐스팅 불가", this);
                return;
            }
            if (CurrentFish != null && CurrentFish.State != FishState.Idle)
                return; // 한 번에 한 마리

            float distance = Vector3.Distance(transform.position, target.transform.position);
            if (distance > m_rod.length)
            {
                Debug.Log($"[FishingRod] 사거리 밖 ({distance:F1}m > {m_rod.length}m)", this);
                return;
            }

            CurrentFish = target;
            target.SetTuning(m_tuning);
            target.BeginApproach(this);
        }

        /// <summary>챔질 — 현재 물고기에 위임. 유효 판정(Bite 윈도우)은 Fish가 한다.</summary>
        public void Chamjil() => CurrentFish?.OnChamjil();

        /// <summary>입질(Bite) 진입 시점의 미끼 차감 (§4). 부족하면 false.</summary>
        public bool TryConsumeBait()
        {
            if (m_baitCount <= 0) return false;
            m_baitCount--;
            return true;
        }

        /// <summary>챔질 성공(Fight 진입) 시점의 내구도 차감 (§4).</summary>
        public void ConsumeDurability() => m_rod.durability = Mathf.Max(0, m_rod.durability - 1);

        /// <summary>물고기가 시도 종료(실패 복귀/포획) 시 호출 — 현재 대상 해제.</summary>
        public void ClearCurrent(Fish fish)
        {
            if (CurrentFish == fish) CurrentFish = null;
        }

        // ══ LEGACY — 구 하네스(FishingGroundHarness/P0Harness) 컴파일 호환용, step-09에서 제거 ══

        /// <summary>[LEGACY] 구 하네스의 상태 표시용 — 현재 물고기 상태를 노출.</summary>
        public FishState State => CurrentFish != null ? CurrentFish.State : FishState.Idle;

        /// <summary>[LEGACY] 구 획득 이벤트(FishDef) — step-06에서 상세 이벤트로 대체, step-09에서 제거.</summary>
        public event Action<FishDef> FishCaught;

        /// <summary>LEGACY 획득 이벤트 발화 헬퍼 (step-06에서 사용).</summary>
        protected void RaiseLegacyFishCaught(FishDef def) => FishCaught?.Invoke(def);

        private Fish m_debugFish; // LEGACY Cast(spot)용 디버그 개체 — 재사용

        /// <summary>
        /// [LEGACY] 구 시그니처 캐스팅 — 스폿 어종 테이블에서 추첨해 디버그 물고기를 스폰 후 신 Cast로 위임.
        /// 정식 타겟팅(TryGetNearestFish)은 step-08 스포너 개편에서.
        /// </summary>
        [Obsolete("구 하네스 호환용 임시 — step-09에서 제거")]
        public void Cast(FishingSpot spot)
        {
            if (spot == null) return;

            FishDef def = spot.PickRandomFish();
            FishSpeciesData species = FindSpecies(def != null ? def.Id : null);

            if (m_debugFish == null)
            {
                var go = new GameObject("Fish_Debug");
                go.transform.position = spot.transform.position;
                var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                visual.name = "Visual";
                Destroy(visual.GetComponent<Collider>());
                visual.transform.SetParent(go.transform, false);
                visual.transform.localScale = Vector3.one * 0.3f;
                m_debugFish = go.AddComponent<Fish>();
            }

            if (m_debugFish.State != FishState.Idle) return;
            m_debugFish.transform.position = spot.transform.position;
            m_debugFish.Initialize(FishInstance.Roll(species, UnityEngine.Random.value));
            Cast(m_debugFish);
        }

        /// <summary>
        /// [LEGACY] 구 시그니처 릴링 — 물고기 상태별 컨텍스트 액션으로 매핑.
        /// (Bite/Approach → 챔질. Fight/Hooked 매핑은 step-04/06에서 확장)
        /// </summary>
        [Obsolete("구 하네스 호환용 임시 — step-09에서 제거")]
        public void Reel()
        {
            if (CurrentFish == null) return;
            switch (CurrentFish.State)
            {
                case FishState.Bite:
                case FishState.Approach:
                    Chamjil();
                    break;
            }
        }

        private static FishSpeciesData FindSpecies(string fishId)
        {
            FishSpeciesData[] all = FishSpeciesTable.Species;
            if (!string.IsNullOrEmpty(fishId))
                foreach (FishSpeciesData s in all)
                    if (s.fishId == fishId) return s;
            return all[0];
        }
    }
}
