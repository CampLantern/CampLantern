using System;
using UnityEngine;

namespace CampLantern.Fishing
{
    /// <summary>포획 보상 묶음 — 산출은 FishingFormulas 경유(§7-6 임시 고정 1, TODO(FORMULA)).</summary>
    public struct FishReward
    {
        public int xp;
        public int coin;
    }

    /// <summary>
    /// 낚싯대 — 입력 진입점 + 미끼/내구도/텐션(RodData) 컨트롤러 (design/fishing-detailed).
    /// 구 버전의 타이밍 FSM(캐스팅→대기→입질)은 물고기 FSM(<see cref="Fish"/>)으로 이관됐다 —
    /// 파이팅 진행은 전부 Fish가 담당하고, 낚싯대는 얇게 유지한다.
    ///
    /// VR 입력(스윙/트리거)은 별도 어댑터(FishingRodInput, step-07)가 이 public API를 호출한다.
    /// 획득 알림은 이벤트로만 — Wallet/Inventory 적용은 하네스 책임 (design 아키텍처 결정).
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
            if (m_rod.maxDurability <= 0) // maxDurability 필드 추가 전 직렬화된 씬 보정
                m_rod.maxDurability = m_rod.durability > 0 ? m_rod.durability : 10;
            if (m_tuning == null)
                m_tuning = new FishingTuning();
        }

        // §7-2 예시(텐션 10 낚싯대로 힘 10 물고기 → 허용 실수 4초)에 맞춘 기본 낚싯대.
        private static RodData CreateBasicRod() => new RodData
        {
            rodId = "rod_basic", power = 1f, durability = 10, maxDurability = 10, length = 15f, tension = 10f, // TODO(DATA)
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
            target.Caught -= OnCurrentFishCaught; // 중복 구독 방지 (rules/scripts.md)
            target.Caught += OnCurrentFishCaught;
            target.SetTuning(m_tuning);
            target.BeginApproach(this);
        }

        /// <summary>포획 성공 — 물고기 개체 + 보상(§7-6 임시 1 고정, TODO(FORMULA) 경유)을 알린다.
        /// Wallet/Inventory 적용은 구독자(하네스) 책임.</summary>
        public event Action<FishInstance, FishReward> FishCaught;

        // Fish.Caught 수신 — 보상 산출(Formulas 경유) → 이벤트 발화 → 대상 해제
        private void OnCurrentFishCaught()
        {
            Fish fish = CurrentFish;
            if (fish == null || fish.Instance == null) return;

            FishInstance instance = fish.Instance;
            var reward = new FishReward
            {
                xp   = FishingFormulas.RewardXp(instance),
                coin = FishingFormulas.RewardCoin(instance),
            };

            FishCaught?.Invoke(instance, reward);
            ClearCurrent(fish);
        }

        /// <summary>챔질 — 현재 물고기에 위임. 유효 판정(Bite 윈도우)은 Fish가 한다.</summary>
        public void Chamjil() => CurrentFish?.OnChamjil();

        /// <summary>릴링(트리거 홀드, §8) 상태 — Fish가 매 프레임 읽는다. 크랭크 회전 아님.</summary>
        public bool Reeling { get; private set; }

        /// <summary>트리거 홀드 on/off — 입력 어댑터(step-07)/하네스가 호출.</summary>
        public void SetReeling(bool held) => Reeling = held;

        /// <summary>입질(Bite) 진입 시점의 미끼 차감 (§4). 부족하면 false.</summary>
        public bool TryConsumeBait()
        {
            if (m_baitCount <= 0) return false;
            m_baitCount--;
            return true;
        }

        /// <summary>챔질 성공(Fight 진입) 시점의 내구도 차감 (§4).</summary>
        public void ConsumeDurability() => m_rod.durability = Mathf.Max(0, m_rod.durability - 1);

        /// <summary>미끼 보충 — 결제(Wallet, §9 BaitPrice 경유)는 하네스 책임. 낚싯대는 수량만 관리.</summary>
        public void AddBait(int count)
        {
            if (count <= 0) return;
            m_baitCount += count;
        }

        /// <summary>내구도 전량 수리 (§5-2 수리대) — 결제(Wallet, §9 RepairPrice 경유)는 하네스 책임.</summary>
        public void Repair() => m_rod.durability = m_rod.maxDurability;

        /// <summary>물고기가 시도 종료(실패 복귀/포획) 시 호출 — 현재 대상 해제 + 구독/홀드 상태 정리.</summary>
        public void ClearCurrent(Fish fish)
        {
            if (CurrentFish != fish) return;
            if (fish != null) fish.Caught -= OnCurrentFishCaught;
            CurrentFish = null;
            Reeling = false; // 다음 시도에 홀드가 이월되지 않게
        }

        private void OnDestroy()
        {
            if (CurrentFish != null) CurrentFish.Caught -= OnCurrentFishCaught;
        }

    }
}
