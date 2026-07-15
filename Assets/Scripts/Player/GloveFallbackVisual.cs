using UnityEngine;

namespace CampLantern.Player
{
    /// <summary>
    /// 컨트롤러 앵커에 붙는 플레이스홀더 장갑 비주얼의 표시 제어 — 진짜 핸드 트래킹 데이터
    /// (컨트롤러 기반 손 포즈 포함)가 돌면 Meta HandVisual이 렌더되므로 자신을 숨긴다.
    /// Meta XR Simulator·PC Link처럼 손 데이터가 안 나오는 환경에서의 폴백 (실측: 시뮬레이터는
    /// controllerDrivenHandPoses를 지원하지 않아 GetHandTrackingEnabled가 False).
    /// 비주얼 생성은 VRPlayerRigFactory.EnableControllerDrivenHands가 한다.
    /// </summary>
    public class GloveFallbackVisual : MonoBehaviour
    {
        private Renderer[] m_renderers;
        private bool m_lastHandsActive;

        private void Awake()
        {
            m_renderers = GetComponentsInChildren<Renderer>(true);
        }

        private void Update()
        {
            bool handsActive = OVRPlugin.GetHandTrackingEnabled();
            if (handsActive == m_lastHandsActive && m_renderers.Length > 0 && m_renderers[0].enabled == !handsActive)
                return; // 상태 변화 없음

            m_lastHandsActive = handsActive;
            foreach (var r in m_renderers)
                r.enabled = !handsActive;
        }
    }
}
