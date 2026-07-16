using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CampLantern.UI
{
    /// <summary>
    /// 획득/결과 토스트 — 시야 하단에 짧은 알림을 띄우고 자동으로 사라진다
    /// ("조리 결과: 생선구이", "판매 +35c", "사냥 보상 지급" 등 IMGUI 로그 라벨의 VR 대체).
    /// 표시 전용이라 UI 전체를 코드로 생성한다. 중앙 눈 앵커(영속 리그)에 부착되므로
    /// 씬 전환 시 소유자(하네스)가 파괴를 책임진다 (WristHud와 동일 규칙).
    /// </summary>
    public class ToastHud : MonoBehaviour
    {
        private const float k_holdSeconds = 2.5f;
        private const float k_fadeSeconds = 0.5f;

        private TextMeshProUGUI m_label;
        private CanvasGroup m_group;
        private float m_hideAt = -1f;
        private bool m_attached;

        /// <summary>토스트 HUD 생성. 소유자가 파괴를 책임진다.</summary>
        public static ToastHud Spawn()
        {
            var go = new GameObject("ToastHud");
            var hud = go.AddComponent<ToastHud>();
            hud.BuildUI();
            return hud;
        }

        /// <summary>짧은 알림 표시 — 연속 호출 시 최신 메시지로 교체되고 시간이 연장된다.</summary>
        public void Show(string message)
        {
            if (m_label == null) return;
            m_label.text = message;
            m_group.alpha = 1f;
            m_hideAt = Time.time + k_holdSeconds;
        }

        private void BuildUI()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)transform;
            rt.sizeDelta = new Vector2(640f, 90f);
            transform.localScale = Vector3.one * 0.0009f; // 640px ≈ 0.58m (1.2m 거리에서 시야 하단 한 줄)

            m_group = gameObject.AddComponent<CanvasGroup>();
            m_group.alpha = 0f;
            m_group.interactable = false;
            m_group.blocksRaycasts = false;

            var bg = new GameObject("Bg", typeof(RectTransform));
            bg.transform.SetParent(transform, false);
            var brt = (RectTransform)bg.transform;
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.one;
            brt.offsetMin = Vector2.zero;
            brt.offsetMax = Vector2.zero;
            var bgImage = bg.AddComponent<Image>();
            bgImage.color = new Color(0.08f, 0.08f, 0.1f, 0.7f);
            UISkin.TryApplyPill(bgImage); // 알림은 보라 필 (없으면 사각 단색)
            bgImage.raycastTarget = false;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(transform, false);
            var lrt = (RectTransform)labelGo.transform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(16f, 8f);
            lrt.offsetMax = new Vector2(-16f, -8f);
            m_label = labelGo.AddComponent<TextMeshProUGUI>();
            m_label.fontSize      = 30f;
            m_label.alignment     = TextAlignmentOptions.Center;
            m_label.color         = Color.white;
            m_label.raycastTarget = false;
            m_label.richText      = false;
        }

        private void Update()
        {
            // 영속 리그는 스폰 타이밍이 늦을 수 있어 붙을 때까지 재시도 (WristHud와 동일)
            if (!m_attached) TryAttachToView();

            if (m_hideAt < 0f || m_group == null) return;
            float sinceHide = Time.time - m_hideAt;
            if (sinceHide < 0f) return;
            m_group.alpha = Mathf.Clamp01(1f - sinceHide / k_fadeSeconds);
            if (m_group.alpha <= 0f) m_hideAt = -1f;
        }

        private void TryAttachToView()
        {
            var rig = FindFirstObjectByType<OVRCameraRig>();
            if (rig == null || rig.centerEyeAnchor == null) return;

            transform.SetParent(rig.centerEyeAnchor, false);
            transform.localPosition = new Vector3(0f, -0.22f, 1.2f); // 시야 하단 — TODO(TUNING): 실기 확인
            transform.localRotation = Quaternion.identity;
            m_attached = true;
        }
    }
}
