using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CampLantern.UI
{
    /// <summary>
    /// 포탈 패널 소유 컨트롤러 — VRUIPanel 위 버튼들에 목적지(라벨+씬 이름)를 Push하고,
    /// 클릭 시 해당 공간 씬으로 이동한다. 로비 허브의 실사용 VR 이동 UI(LobbyHarness IMGUI의 VR 대체).
    ///
    /// UI 초기화 원칙(rules/scripts.md): 버튼은 표시만 담당하고, 라벨·클릭 구독은 이 소유자가 주입한다.
    /// 로비는 싱글 공간(room-architecture.md)이라 네트워크 처리 없음 — SceneManager.LoadScene만 수행.
    /// 배선은 LobbyMapFactory(Tools > Make Assets > Build Lobby Map (VR))가 한다.
    /// </summary>
    public class PortalPanelController : MonoBehaviour
    {
        [Serializable]
        public struct Destination
        {
            public string label;      // 버튼 표시 텍스트
            public string sceneName;  // SceneManager.LoadScene 대상
        }

        [SerializeField] private VRUIPanel m_panel;
        [SerializeField] private VRUIButton[] m_buttons;
        [SerializeField] private Destination[] m_destinations; // m_buttons와 인덱스 1:1
        [SerializeField] private string m_title = "이동";

        // 람다 구독을 해제하려면 동일 인스턴스가 필요 — 버튼별 핸들러 보관
        private Action[] m_handlers;

        private int PairCount => Mathf.Min(m_buttons != null ? m_buttons.Length : 0,
                                           m_destinations != null ? m_destinations.Length : 0);

        private void Start()
        {
            // 소유자가 준비된 시점에 Push (rules/scripts.md UI 초기화 순서 원칙)
            if (m_panel != null) m_panel.SetTitle(m_title);
            for (int i = 0; i < PairCount; i++)
                m_buttons[i].SetLabel(m_destinations[i].label);
        }

        private void OnEnable()
        {
            if (m_handlers == null)
            {
                m_handlers = new Action[PairCount];
                for (int i = 0; i < m_handlers.Length; i++)
                {
                    int index = i; // 클로저 캡처 고정
                    m_handlers[i] = () => LoadDestination(index);
                }
            }

            for (int i = 0; i < m_handlers.Length; i++)
            {
                // 중복 구독 방지 후 등록 (rules/scripts.md)
                m_buttons[i].Clicked -= m_handlers[i];
                m_buttons[i].Clicked += m_handlers[i];
            }
        }

        private void OnDisable()
        {
            if (m_handlers == null) return;
            for (int i = 0; i < m_handlers.Length; i++)
                if (m_buttons[i] != null) m_buttons[i].Clicked -= m_handlers[i];
        }

        private bool m_loading; // 페이드 중 중복 클릭 방지

        private void LoadDestination(int index)
        {
            string sceneName = m_destinations[index].sceneName;
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogWarning($"[PortalPanel] 목적지 씬 이름이 비어 있음 (index {index})");
                return;
            }
            if (m_loading) return;

            // 씬 전환 페이드 아웃 — 검은 플래시 방지. 페이드 인은 PersistentPlayer가 로드 후 트리거.
            var fade = OVRScreenFade.instance;
            if (fade != null)
            {
                m_loading = true;
                StartCoroutine(FadeAndLoad(fade, sceneName));
            }
            else
            {
                SceneManager.LoadScene(sceneName); // 페이드 미배선(데스크톱 등) — 즉시 이동
            }
        }

        private IEnumerator FadeAndLoad(OVRScreenFade fade, string sceneName)
        {
            fade.FadeOut();
            yield return new WaitForSeconds(fade.fadeTime);
            SceneManager.LoadScene(sceneName);
        }
    }
}
