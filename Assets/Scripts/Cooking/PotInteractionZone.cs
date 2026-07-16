using System;
using System.Collections.Generic;
using CampLantern.Core;
using UnityEngine;

namespace CampLantern.Cooking
{
    /// <summary>
    /// 냄비 림(입구) 트리거 — UI 버튼("투입"/"조리")을 대체하는 물리 동사 두 개를 담당한다.
    ///   ① 재료 투입: IngredientPickup이 들어오면 CookingPot.TryAddIngredient —
    ///      성공 시 프록시 파괴, 실패(재고 없음) 시 위로 튕겨내고 Rejected 발화.
    ///   ② 조리: StirTool(국자) 끝이 트리거 안에서 누적 이동 거리를 채우면 Cook().
    /// 햅틱: 젓는 동안 양손 약한 진동, 투입/조리 성공 시 짧은 펄스.
    /// 트리거 콜백은 물리 스텝 내부 실행이므로 velocity 조작은 RULE-03 취지에 부합한다.
    /// 생성·배선은 PhysicalCookingBuilder(런타임 코드 생성 — 씬/프리팹 수정 없음).
    /// </summary>
    public class PotInteractionZone : MonoBehaviour
    {
        private const float k_stirGoalMeters = 2.2f;  // 국자 끝 누적 이동 거리 — 냄비 3~4바퀴 감각. TODO(TUNING): 실기 확정
        private const float k_stirMinSpeed   = 0.15f; // 이 미만은 정지 드리프트로 무시 (m/s)
        private const float k_stirMaxSpeed   = 5f;    // 이 초과는 순간이동성 노이즈로 무시 (m/s)
        private const float k_rejectUpSpeed  = 2.5f;  // 거부 시 튕겨내는 상향 속도

        /// <summary>재고가 없어 투입이 거부된 재료 — 토스트 등 피드백 훅. 구독자는 OnDestroy/OnDisable에서 해제할 것.</summary>
        public event Action<ItemDef> Rejected;

        private CookingPot m_pot;
        private readonly Dictionary<StirTool, Vector3> m_lastTip = new Dictionary<StirTool, Vector3>();
        private float m_stirProgress;
        private float m_lastStirTime = -10f; // 최근 유효 젓기 시각 — 햅틱 유지 판단
        private float m_pulseUntil;          // 성공 펄스 종료 시각

        /// <summary>조립기가 생성 직후 호출한다 (Push 초기화 — rules/scripts.md).</summary>
        public void Initialize(CookingPot pot)
        {
            m_pot = pot;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (m_pot == null) return;
            Rigidbody rb = other.attachedRigidbody;
            if (rb == null) return;

            IngredientPickup pickup = rb.GetComponent<IngredientPickup>();
            if (pickup == null || pickup.Item == null) return;

            if (m_pot.TryAddIngredient(pickup.Item))
            {
                m_pulseUntil = Time.time + 0.12f; // 투입 성공 펄스
                Destroy(pickup.gameObject);
            }
            else
            {
                // 재고 없음(프록시가 낡음) — 삼키지 않고 튕겨낸다. 인벤토리가 권위라 복제는 불가능.
                if (!rb.isKinematic) // 손에 들린 채 들어오면 SDK가 kinematic일 수 있음 — velocity 세팅 경고 회피
                {
                    rb.linearVelocity = new Vector3(
                        UnityEngine.Random.Range(-0.6f, 0.6f),
                        k_rejectUpSpeed,
                        UnityEngine.Random.Range(-0.6f, 0.6f));
                }
                Rejected?.Invoke(pickup.Item);
            }
        }

        private void OnTriggerStay(Collider other)
        {
            if (m_pot == null) return;
            Rigidbody rb = other.attachedRigidbody;
            if (rb == null) return;

            StirTool tool = rb.GetComponent<StirTool>();
            if (tool == null) return;

            // 재료 없는 냄비는 저어도 진행 없음 — 진행도도 초기화
            if (m_pot.Ingredients.Count == 0)
            {
                m_stirProgress = 0f;
                m_lastTip[tool] = tool.Tip.position;
                return;
            }

            Vector3 tip = tool.Tip.position;
            if (m_lastTip.TryGetValue(tool, out Vector3 last))
            {
                float distance = (tip - last).magnitude;
                float speed = distance / Time.fixedDeltaTime;
                if (speed >= k_stirMinSpeed && speed <= k_stirMaxSpeed)
                {
                    m_stirProgress += distance;
                    m_lastStirTime = Time.time;
                }
            }
            m_lastTip[tool] = tip;

            if (m_stirProgress >= k_stirGoalMeters)
            {
                m_stirProgress = 0f;
                m_pulseUntil = Time.time + 0.25f; // 조리 완료 펄스
                m_pot.Cook();
            }
        }

        private void OnTriggerExit(Collider other)
        {
            Rigidbody rb = other.attachedRigidbody;
            if (rb == null) return;

            StirTool tool = rb.GetComponent<StirTool>();
            if (tool != null) m_lastTip.Remove(tool);
        }

        private void Update()
        {
            // 어느 손이 국자를 쥐었는지 SDK 참조 없이 알 수 없으므로 양손에 동일 진동 (P0 단순화)
            float amplitude;
            float frequency;
            if (Time.time < m_pulseUntil)
            {
                amplitude = 0.7f; frequency = 0.6f;      // 성공 펄스 — 강하게 짧게
            }
            else if (Time.time - m_lastStirTime < 0.12f)
            {
                amplitude = 0.2f; frequency = 0.3f;      // 젓는 중 — 은은하게
            }
            else
            {
                amplitude = 0f; frequency = 0f;
            }

            OVRInput.SetControllerVibration(frequency, amplitude, OVRInput.Controller.LTouch);
            OVRInput.SetControllerVibration(frequency, amplitude, OVRInput.Controller.RTouch);
        }

        private void OnDisable()
        {
            // 진동 잔류 방지 (FishingRodInput과 동일 패턴)
            OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.LTouch);
            OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.RTouch);
        }
    }
}
