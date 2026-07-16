using UnityEngine;

namespace CampLantern.Cooking
{
    /// <summary>
    /// 국자 — 냄비 젓기 도구 마커. PotInteractionZone이 Tip(국자 머리) 이동량으로 젓기 진행도를 잰다.
    /// 도구라서 낙사 시 파괴하지 않고 앵커(스폰 위치)로 복귀시킨다.
    /// 프리팹(그랩 배선 포함)은 Tools > Make Assets > Physical Cooking.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class StirTool : MonoBehaviour
    {
        [Tooltip("젓기 판정 기준점 — 국자 머리(스쿱). 미할당이면 루트 사용")]
        [SerializeField] private Transform m_tip;

        private const float k_killY = -1f; // 낙사 복귀 기준 높이

        private Rigidbody m_rb;
        private Vector3 m_anchorPos;
        private Quaternion m_anchorRot;

        /// <summary>젓기 판정 기준점.</summary>
        public Transform Tip => m_tip != null ? m_tip : transform;

        private void Awake()
        {
            m_rb = GetComponent<Rigidbody>();
            m_anchorPos = transform.position;
            m_anchorRot = transform.rotation;
        }

        /// <summary>낙사 복귀 지점 지정 — 스폰 직후 조립기가 호출한다.</summary>
        public void SetAnchor(Vector3 position, Quaternion rotation)
        {
            m_anchorPos = position;
            m_anchorRot = rotation;
        }

        private void FixedUpdate()
        {
            // 물리 API(velocity/position)는 FixedUpdate에서만 (RULE-03)
            if (transform.position.y >= k_killY) return;

            if (!m_rb.isKinematic) // 그랩 중엔 SDK가 kinematic으로 바꿀 수 있음 — velocity 세팅 경고 회피
            {
                m_rb.linearVelocity  = Vector3.zero;
                m_rb.angularVelocity = Vector3.zero;
            }
            m_rb.position = m_anchorPos;
            m_rb.rotation = m_anchorRot;
        }
    }
}
