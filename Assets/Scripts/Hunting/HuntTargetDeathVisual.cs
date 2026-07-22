using UnityEngine;

namespace CampLantern.Hunting
{
    /// <summary>
    /// 구형 HuntTarget 처치 시각 피드백 — 처치되면 Visual 루트를 옆으로 눕히고,
    /// 재사냥(HuntActive 재점등)이 시작되면 다시 일으켜 세운다.
    /// 그레이박스·실아트(Visual 스왑) 공용으로 루트 회전만 쓰며, 물리 API 미사용(RULE-03 무관).
    /// 프리팹 수정 없이 하네스가 런타임 AddComponent 한다 (HUD류와 동일한 코드 생성 패턴).
    /// 이벤트 기반(Defeated)이라 비권한 클라이언트의 늦은 스냅샷 초기값(HP 0)에 오발하지 않는다.
    /// </summary>
    [RequireComponent(typeof(HuntTarget))]
    public class HuntTargetDeathVisual : MonoBehaviour
    {
        private const float k_tweenSeconds = 0.6f; // 쓰러짐/기상 연출 시간
        private const float k_fallAngle    = 78f;  // 완전히 90°면 지면과 겹쳐 보여 살짝 덜 눕힘

        private HuntTarget m_target;
        private Transform m_visual;
        private Quaternion m_uprightRot;
        private bool m_fallen;
        private float m_progress; // 0 = 서 있음, 1 = 눕힘

        private void Awake()
        {
            m_target = GetComponent<HuntTarget>();

            // 팩토리 산출 프리팹은 비주얼 루트 이름이 "Visual" — 없으면 첫 자식으로 폴백
            m_visual = transform.Find("Visual");
            if (m_visual == null && transform.childCount > 0) m_visual = transform.GetChild(0);
            if (m_visual != null) m_uprightRot = m_visual.localRotation;
        }

        private void OnEnable()
        {
            m_target.Defeated -= OnDefeated;
            m_target.Defeated += OnDefeated;
        }

        private void OnDisable()
        {
            if (m_target != null) m_target.Defeated -= OnDefeated;
        }

        private void OnDefeated(HuntTarget _) => m_fallen = true;

        private void Update()
        {
            if (m_visual == null) return;

            // 재사냥 시작 — 권한자·비권한자 모두 HuntActive 복제로 감지해 기상
            if (m_fallen && m_target.HuntActive) m_fallen = false;

            float direction = m_fallen ? 1f : -1f;
            float next = Mathf.Clamp01(m_progress + direction * Time.deltaTime / k_tweenSeconds);
            if (Mathf.Approximately(next, m_progress)) return;

            m_progress = next;
            float eased = Mathf.SmoothStep(0f, 1f, m_progress);
            m_visual.localRotation = m_uprightRot * Quaternion.Euler(0f, 0f, k_fallAngle * eased);
        }
    }
}
