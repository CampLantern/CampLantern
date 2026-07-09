using System.Collections.Generic;
using CampLantern.Core;
using UnityEngine;

namespace CampLantern.Fishing
{
    /// <summary>
    /// 낚시 구역 = 물고기 스포너/관리자 (design/fishing-detailed step-08).
    /// 스폿 안에 실루엣 물고기(Fish)를 여러 마리 스폰·유영시키고(§3-1 Idle), 캐스팅 타겟 질의를 제공하며,
    /// 잡힌 자리는 지연 후 재스폰한다. 실루엣 스케일은 개체 크기를 반영(§5-1) — 캐스팅 전 난이도 예고.
    ///
    /// m_fishTable(FishDef[])은 기존 에디터 팩토리 배선 호환용으로 유지 — FishDef.Id를
    /// FishSpeciesTable의 fishId에 매핑해 어종을 고른다 (연결 고리, step-08).
    /// 실루엣은 런타임 프리미티브 생성 — 프리팹 불필요(RULE-02 무관). 유영은 Transform만(RULE-03 무관).
    /// </summary>
    public class FishingSpot : MonoBehaviour
    {
        [Tooltip("이 구역에서 잡히는 어종 목록 — 에디터 팩토리가 배선. 비면 전 어종 등장")]
        [SerializeField] private FishDef[] m_fishTable;

        [Tooltip("동시 실루엣 수")]
        [SerializeField] private int m_fishCount = 3;            // TODO(TUNING)

        [Tooltip("스폰/유영 반경(m)")]
        [SerializeField] private float m_radius = 4f;            // TODO(TUNING)

        [Tooltip("포획 후 재스폰 지연(초)")]
        [SerializeField] private float m_respawnSeconds = 3f;    // TODO(TUNING)

        [Tooltip("Idle 유영 반경(m) — 앵커 주변 흔들림")]
        [SerializeField] private float m_wanderRadius = 0.5f;    // TODO(TUNING)

        [Tooltip("낚시 튜닝 값 — 스폰하는 물고기 전체에 공유 주입")]
        [SerializeField] private FishingTuning m_tuning = new FishingTuning();

        // 스폰 개체 + 유영 앵커/위상
        private class SpotFish
        {
            public Fish fish;
            public Vector3 anchor;
            public float phase;
        }

        private readonly List<SpotFish> m_fishes = new List<SpotFish>();
        private readonly List<float> m_respawnAt = new List<float>();

        public FishingTuning Tuning => m_tuning;

        private void Start()
        {
            // 다른 시스템의 Awake 이후 스폰 (초기화 순서 안전)
            for (int i = 0; i < m_fishCount; i++)
                SpawnOne();
        }

        private void Update()
        {
            // 재스폰 큐
            for (int i = m_respawnAt.Count - 1; i >= 0; i--)
            {
                if (Time.time < m_respawnAt[i]) continue;
                m_respawnAt.RemoveAt(i);
                SpawnOne();
            }

            // Idle 유영 — 원래 위치(앵커) 기준 가벼운 원운동 (§3-1). 파이팅 중 개체는 Fish가 움직인다.
            for (int i = 0; i < m_fishes.Count; i++)
            {
                SpotFish sf = m_fishes[i];
                if (sf.fish == null || sf.fish.State != FishState.Idle) continue;
                float t = Time.time * 0.6f + sf.phase;
                sf.fish.transform.position = sf.anchor
                    + new Vector3(Mathf.Sin(t), 0f, Mathf.Cos(t * 0.8f)) * m_wanderRadius;
            }
        }

        /// <summary>
        /// 캐스팅 타겟 질의 — point에서 가장 가까운 낚시 가능(Idle/Approach) 개체.
        /// maxRange(낚싯대 사거리, §5-2) 밖이면 false.
        /// </summary>
        public bool TryGetNearestFish(Vector3 point, float maxRange, out Fish fish)
        {
            fish = null;
            float best = maxRange;

            for (int i = 0; i < m_fishes.Count; i++)
            {
                Fish candidate = m_fishes[i].fish;
                if (candidate == null) continue;
                if (candidate.State != FishState.Idle && candidate.State != FishState.Approach) continue;

                float distance = Vector3.Distance(point, candidate.transform.position);
                if (distance <= best)
                {
                    best = distance;
                    fish = candidate;
                }
            }
            return fish != null;
        }

        // ── 스폰 ─────────────────────────────────────────────────────

        private void SpawnOne()
        {
            FishSpeciesData species = PickSpecies();
            var instance = FishInstance.Roll(species, Random.value);

            Vector2 offset = Random.insideUnitCircle * m_radius;
            Vector3 anchor = transform.position + new Vector3(offset.x, 0f, offset.y);

            var go = new GameObject($"Fish_{species.fishId}");
            go.transform.position = anchor;

            // 실루엣 비주얼 — 납작한 스피어, 스케일은 개체 크기 반영(§5-1). 런타임 생성이라 프리팹 불필요.
            var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.name = "Silhouette";
            Destroy(visual.GetComponent<Collider>());
            visual.transform.SetParent(go.transform, false);
            float s = Mathf.Max(0.15f, instance.Size * 0.02f);
            visual.transform.localScale = new Vector3(s * 1.6f, s * 0.4f, s);
            visual.GetComponent<Renderer>().material.color = new Color(0.12f, 0.16f, 0.22f, 1f); // 짙은 실루엣

            var fish = go.AddComponent<Fish>();
            fish.SetTuning(m_tuning);
            fish.Initialize(instance);

            var sf = new SpotFish { fish = fish, anchor = anchor, phase = Random.Range(0f, 6.28f) };
            m_fishes.Add(sf);

            // 포획 시 재스폰 예약 — 개체가 파괴되면 이벤트도 함께 사라지므로 구독 해제 불필요
            fish.Caught += () =>
            {
                m_fishes.Remove(sf);
                m_respawnAt.Add(Time.time + m_respawnSeconds);
            };
        }

        // FishDef 테이블(m_fishTable)에서 추첨 → fishId 매핑. 비었으면 전 어종 균등.
        private FishSpeciesData PickSpecies()
        {
            FishSpeciesData[] all = FishSpeciesTable.Species;

            if (m_fishTable != null && m_fishTable.Length > 0)
            {
                FishDef def = m_fishTable[Random.Range(0, m_fishTable.Length)];
                if (def != null)
                {
                    foreach (FishSpeciesData species in all)
                        if (species.fishId == def.Id)
                            return species;
                    Debug.LogWarning($"[FishingSpot] FishSpeciesTable에 없는 어종 Id: {def.Id} — 무작위 어종으로 대체", this);
                }
            }
            return all[Random.Range(0, all.Length)];
        }
    }
}
