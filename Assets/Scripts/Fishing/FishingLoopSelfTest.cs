using UnityEngine;

namespace CampLantern.Fishing
{
    /// <summary>
    /// 개발용 자동 검증 봇 — 정교화 낚시 풀 루프를 무인 주행한다 (사람 입력 불필요).
    /// Phase 1 (Catch): 올바른 플레이(흰색 릴링·빨간색 절제·비늘털이 스윙) → 포획까지.
    /// Phase 2 (Break): 나쁜 플레이(빨간색에서만 릴링) → 줄 끊김(§3-2 리셋)까지.
    /// 전이·결과를 "[FishTest]" 로그로 남긴다 — ClaudeBridge 플레이 검증/회귀 확인용.
    /// 플레이 중 아무 GameObject에 AddComponent 하면 동작 (rod/spot 자동 탐색, 입력 어댑터는 비활성화).
    /// </summary>
    public class FishingLoopSelfTest : MonoBehaviour
    {
        private enum Phase { Catch, Break, Done }

        private FishingRod m_rod;
        private FishingSpot m_spot;
        private Phase m_phase = Phase.Catch;
        private Fish m_watched;      // StateChanged 전이 로그 대상
        private FishState m_prev;
        private int m_escapes, m_shakes;
        private float m_timeout;

        private void Start()
        {
            m_rod  = FindFirstObjectByType<FishingRod>();
            m_spot = FindFirstObjectByType<FishingSpot>();
            m_timeout = Time.time + 150f;

            // 입력 어댑터가 SetReeling을 덮지 않도록 비활성 (봇이 단독 제어)
            var input = FindFirstObjectByType<FishingRodInput>();
            if (input != null) input.enabled = false;

            if (m_rod == null || m_spot == null)
            {
                Debug.LogError("[FishTest] rod/spot 없음 — 중단");
                m_phase = Phase.Done;
                return;
            }

            m_rod.FishCaught -= OnCaught;
            m_rod.FishCaught += OnCaught;
            Debug.Log("[FishTest] 시작 — Phase 1: 포획(절제 플레이)");
        }

        private void OnDestroy()
        {
            if (m_rod != null) m_rod.FishCaught -= OnCaught;
            Unwatch();
        }

        private void OnCaught(FishInstance fish, FishReward reward)
        {
            Debug.Log($"[FishTest] ✅ 포획 성공: {fish.Species.fishId} {fish.Length:F1}cm/{fish.Weight:F2}kg " +
                      $"(t={fish.T:F2}) — XP+{reward.xp} 코인+{reward.coin} · 도망 {m_escapes}회 · 비늘털이 {m_shakes}회");
            Unwatch();
            m_escapes = 0; m_shakes = 0;
            m_phase = Phase.Break;
            Debug.Log("[FishTest] Phase 2: 줄 끊김(빨간색 릴링) 검증 시작");
        }

        private void Update()
        {
            if (m_phase == Phase.Done) return;
            if (Time.time > m_timeout)
            {
                Debug.LogError($"[FishTest] ❌ 타임아웃 (phase={m_phase})");
                m_phase = Phase.Done;
                return;
            }

            Fish fish = m_rod.CurrentFish;
            if (fish == null)
            {
                m_rod.SetReeling(false);
                if (m_spot.TryGetNearestFish(m_rod.transform.position, m_rod.Rod.length, out Fish target))
                {
                    m_rod.Cast(target);
                    if (m_rod.CurrentFish != null)
                    {
                        Watch(target);
                        Debug.Log($"[FishTest] 캐스팅: {target.Instance.Species.fishId} " +
                                  $"HP {target.Instance.MaxHealth:F1} 힘 {target.Instance.Species.power} (미끼 {m_rod.BaitCount})");
                    }
                }
                return;
            }

            switch (fish.State)
            {
                case FishState.Bite:
                    m_rod.Chamjil();
                    break;

                case FishState.FightNormal:
                    // Phase1: 흰색 = 감는다 / Phase2: 흰색엔 안 감아 체력 보존(끊김 보장)
                    m_rod.SetReeling(m_phase == Phase.Catch);
                    break;

                case FishState.FightEscape:
                    // Phase1: 빨간색 = 절제 / Phase2: 일부러 감아 텐션 소모
                    m_rod.SetReeling(m_phase == Phase.Break);
                    break;

                case FishState.FightShake:
                    m_rod.SetReeling(m_phase == Phase.Break);
                    if (m_phase == Phase.Catch) fish.OnSwing(); // 체공 스윙 — 디버프 획득
                    break;

                case FishState.Hooked:
                    m_rod.SetReeling(false);
                    fish.Hook();
                    break;
            }
        }

        // ── 전이 추적 (§3-1 대조용 로그) ─────────────────────────────

        private void Watch(Fish fish)
        {
            Unwatch();
            m_watched = fish;
            m_prev = fish.State;
            fish.StateChanged += OnStateChanged;
        }

        private void Unwatch()
        {
            if (m_watched != null) m_watched.StateChanged -= OnStateChanged;
            m_watched = null;
        }

        private void OnStateChanged(FishState next)
        {
            Debug.Log($"[FishTest] 전이: {m_prev} → {next} (줄 {m_watched.Line} · HP {m_watched.Health:F1} · 텐션 {m_watched.Tension:F1})");

            if (next == FishState.FightEscape) m_escapes++;
            if (next == FishState.FightShake)  m_shakes++;

            // Phase2 목표: 파이팅 중 Idle 복귀 = 줄 끊김(§3-2)
            if (m_phase == Phase.Break && next == FishState.Idle &&
                (m_prev == FishState.FightEscape || m_prev == FishState.FightShake))
            {
                Debug.Log($"[FishTest] ✅ 줄 끊김 검증 성공 — 원위치 Idle 복귀, HP 리셋 {m_watched.Health:F1} (§3-2)");
                Unwatch();
                m_phase = Phase.Done;
                Debug.Log("[FishTest] ══ 전체 통과 ══");
            }

            m_prev = next;
        }
    }
}
