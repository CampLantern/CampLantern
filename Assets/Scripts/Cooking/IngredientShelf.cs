using System.Collections.Generic;
using CampLantern.Core;
using CampLantern.Player;
using UnityEngine;

namespace CampLantern.Cooking
{
    /// <summary>
    /// 재료 선반 — 인벤토리의 아이템 종류별로 물리 프록시(IngredientPickup)를 슬롯에 1개씩 꺼내 놓는다.
    /// 프록시는 인벤토리를 차감하지 않으므로(냄비 투입 시점에만 차감 — IngredientPickup 주석 참조)
    /// 자유롭게 재스폰해도 안전하다. 슬롯이 비면(프록시가 파괴되거나 집혀 나가면) 다음 갱신에서 다시 채운다.
    /// 선반 비주얼·슬롯 앵커 생성은 PhysicalCookingBuilder가 담당하고, 이 컴포넌트는 스폰 로직만 가진다.
    /// </summary>
    public class IngredientShelf : MonoBehaviour
    {
        private const float k_slotFreeDistance = 0.35f; // 프록시가 이 거리 이상 벗어나면 슬롯을 빈 것으로 간주(재스폰 허용)
        private const float k_refreshInterval  = 1f;    // 낙사 파괴 등 이벤트 없는 변화 대비 폴링 주기

        private Inventory m_inventory;
        private IngredientPickup m_pickupPrefab;
        private Transform[] m_slots;
        private float m_nextRefreshTime;
        private bool m_overflowLogged;

        private struct Entry
        {
            public IngredientPickup Pickup;
            public int Slot;
        }

        private readonly Dictionary<ItemDef, Entry> m_live = new Dictionary<ItemDef, Entry>();
        private readonly List<ItemDef> m_scratch = new List<ItemDef>(); // 갱신 중 딕셔너리 수정 회피용

        /// <summary>조립기가 생성 직후 호출한다 (Push 초기화 — rules/scripts.md).</summary>
        public void Initialize(Inventory inventory, IngredientPickup pickupPrefab, Transform[] slots)
        {
            m_inventory = inventory;
            m_pickupPrefab = pickupPrefab;
            m_slots = slots;

            m_inventory.Changed -= Refresh;
            m_inventory.Changed += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            if (m_inventory != null) m_inventory.Changed -= Refresh;
        }

        private void Update()
        {
            // 프록시 낙사 파괴·집어 나감은 이벤트가 없으므로 저주기 폴링으로 슬롯을 다시 채운다
            if (Time.time < m_nextRefreshTime) return;
            m_nextRefreshTime = Time.time + k_refreshInterval;
            Refresh();
        }

        private void Refresh()
        {
            if (m_inventory == null || m_pickupPrefab == null || m_slots == null) return;

            PurgeStale();

            // 슬롯 점유 현황 — 살아 있는 엔트리가 붙잡은 슬롯만 점유로 본다
            var occupied = new bool[m_slots.Length];
            foreach (KeyValuePair<ItemDef, Entry> pair in m_live)
                occupied[pair.Value.Slot] = true;

            // 보유 중(수량>0)인데 슬롯에 프록시가 없는 아이템을 채운다
            foreach (KeyValuePair<ItemDef, int> item in m_inventory.Items)
            {
                if (item.Value <= 0 || m_live.ContainsKey(item.Key)) continue;

                int slot = FindFreeSlot(occupied);
                if (slot < 0)
                {
                    if (!m_overflowLogged)
                    {
                        m_overflowLogged = true;
                        Debug.LogWarning($"[IngredientShelf] 슬롯({m_slots.Length}) 부족 — 일부 아이템은 선반에 표시되지 않음", this);
                    }
                    break;
                }

                occupied[slot] = true;
                Spawn(item.Key, slot);
            }
        }

        // 파괴된 프록시 / 슬롯을 떠난 프록시 / 재고가 사라진 프록시를 엔트리에서 정리한다
        private void PurgeStale()
        {
            m_scratch.Clear();
            foreach (KeyValuePair<ItemDef, Entry> pair in m_live)
            {
                Entry entry = pair.Value;
                if (entry.Pickup == null)
                {
                    m_scratch.Add(pair.Key); // 냄비 투입·낙사로 파괴됨 → 재고 남았으면 다음 루프에서 재스폰
                    continue;
                }

                float sqrFromSlot = (entry.Pickup.transform.position - m_slots[entry.Slot].position).sqrMagnitude;
                bool leftSlot = sqrFromSlot > k_slotFreeDistance * k_slotFreeDistance;

                if (leftSlot)
                {
                    // 손에 들려 나갔음 — 엔트리만 놓아준다(오브젝트는 냄비/낙사가 정리). 슬롯은 빈 것으로 취급.
                    m_scratch.Add(pair.Key);
                }
                else if (m_inventory.CountOf(pair.Key) <= 0)
                {
                    // 판매 등으로 재고 소진 + 아직 슬롯 위에 그대로(아무도 안 든 상태) → 안전하게 회수
                    Destroy(entry.Pickup.gameObject);
                    m_scratch.Add(pair.Key);
                }
            }

            foreach (ItemDef key in m_scratch)
                m_live.Remove(key);
        }

        private int FindFreeSlot(bool[] occupied)
        {
            for (int i = 0; i < occupied.Length; i++)
                if (!occupied[i]) return i;
            return -1;
        }

        private void Spawn(ItemDef item, int slot)
        {
            // 슬롯 위 살짝 띄워 스폰 — 중력으로 트레이에 안착
            Vector3 pos = m_slots[slot].position + Vector3.up * 0.03f;
            Quaternion rot = Quaternion.Euler(0f, (slot * 53f) % 360f, 0f); // 슬롯별 고정 요(yaw) 변화 — 균일함 완화
            IngredientPickup pickup = Instantiate(m_pickupPrefab, pos, rot, transform);
            pickup.Initialize(item);

            // 커스텀 손 부착(낚싯대와 동일 방식). 작은 구체라 중심(0,0,0)을 손바닥에 쥔다.
            // 놓으면 떨어뜨림(Drop) — 냄비 투입/낙사 정리에 맡긴다(선반이 슬롯을 다시 채움).
            pickup.gameObject.AddComponent<HeldItemGrabber>().Configure(
                gripLocalPoint: Vector3.zero,
                heldEuler: Vector3.zero,
                palmOffset: new Vector3(0f, -0.02f, 0.03f),
                hand: HeldItemGrabber.HandSide.Auto,
                releaseMode: HeldItemGrabber.ReleaseMode.Drop);

            m_live[item] = new Entry { Pickup = pickup, Slot = slot };
        }
    }
}
