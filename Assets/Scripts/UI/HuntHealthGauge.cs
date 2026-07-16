using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CampLantern.UI
{
    /// <summary>
    /// 사냥감 머리 위 HP 게이지 — 이름·HP 바·협동 필요 인원 표시 (IMGUI HP 라벨의 실사용 VR 대체).
    /// 표시 전용이라 인터랙션 배선이 필요 없어 UI 전체를 코드로 생성한다(프리팹 없음).
    ///
    /// 하네스가 사냥감 훅 시점에 <see cref="Attach"/>로 붙인다. 값은 클로저 폴링(0.2s) —
    /// HuntTarget([Networked] 프로퍼티)과 NetworkedHuntMonster 두 타입을 공통 인터페이스 없이 수용하기 위함.
    /// 대상이 despawn되면 스스로 파괴되고, 처치되면 "처치!" 표시 후 잠시 뒤 사라진다.
    /// </summary>
    public class HuntHealthGauge : MonoBehaviour
    {
        private const float k_refreshInterval = 0.2f;
        private const float k_destroyDelayAfterKill = 3f;

        private Transform m_follow;
        private float m_heightOffset;
        private string m_displayName;
        private int m_needPlayers;
        private Func<int> m_getHp;
        private Func<int> m_getMax;
        private Func<bool> m_getActive;

        private TextMeshProUGUI m_label;
        private RectTransform m_fillRt;
        private Image m_fill;
        private float m_nextRefreshAt;
        private float m_destroyAt = -1f;

        /// <summary>
        /// 사냥감 위에 게이지를 생성한다. 값 조회는 클로저로 — 대상 파괴 시 게이지도 자멸하므로
        /// 클로저가 파괴된 오브젝트를 만질 일은 없다.
        /// </summary>
        public static HuntHealthGauge Attach(Component target, string displayName,
                                             Func<int> getHp, Func<int> getMax, Func<bool> getActive,
                                             int needPlayers)
        {
            var go = new GameObject($"HuntHealthGauge ({displayName})");
            var gauge = go.AddComponent<HuntHealthGauge>();
            gauge.m_follow      = target.transform;
            gauge.m_displayName = displayName;
            gauge.m_getHp       = getHp;
            gauge.m_getMax      = getMax;
            gauge.m_getActive   = getActive;
            gauge.m_needPlayers = needPlayers;

            // 머리 위 높이 — 렌더러 바운드 최고점 + 여유. 최소 0.5m(렌더러 없거나 바닥에 붙은 경우 대비).
            // 주의: 초기값을 크게 잡으면 멧돼지처럼 작은 동물의 게이지가 공중에 뜬다 (플레이 검증에서 발견).
            var renderers = target.GetComponentsInChildren<Renderer>();
            float top = 0.5f;
            foreach (Renderer r in renderers)
                top = Mathf.Max(top, r.bounds.max.y - target.transform.position.y);
            gauge.m_heightOffset = top + 0.35f;

            gauge.BuildUI();
            gauge.Refresh();
            return gauge;
        }

        private void BuildUI()
        {
            // 월드스페이스 캔버스 500x120px × 0.002 = 1.0 x 0.24 m
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)transform;
            rt.sizeDelta = new Vector2(500f, 120f);
            transform.localScale = Vector3.one * 0.002f;

            var bg = NewChild("Bg", transform, new Vector2(500f, 120f), Vector2.zero).AddComponent<Image>();
            bg.color = new Color(0.08f, 0.08f, 0.1f, 0.7f);
            UISkin.TryApplyRounded(bg); // 라운드 스킨 (없으면 사각 단색)
            bg.raycastTarget = false;

            m_label = NewChild("Label", transform, new Vector2(470f, 50f), new Vector2(0f, 26f))
                .AddComponent<TextMeshProUGUI>();
            m_label.fontSize      = 32f;
            m_label.alignment     = TextAlignmentOptions.Center;
            m_label.color         = Color.white;
            m_label.raycastTarget = false;
            m_label.richText      = false;

            var barBg = NewChild("BarBg", transform, new Vector2(460f, 30f), new Vector2(0f, -30f)).AddComponent<Image>();
            barBg.color = new Color(0.2f, 0.2f, 0.24f, 0.9f);
            barBg.raycastTarget = false;

            // 채움 바 — anchorMax.x를 비율로 조절
            var fillGo = new GameObject("Fill", typeof(RectTransform));
            fillGo.transform.SetParent(barBg.transform, false);
            m_fillRt = (RectTransform)fillGo.transform;
            m_fillRt.anchorMin = Vector2.zero;
            m_fillRt.anchorMax = new Vector2(1f, 1f);
            m_fillRt.offsetMin = Vector2.zero;
            m_fillRt.offsetMax = Vector2.zero;
            m_fill = fillGo.AddComponent<Image>();
            m_fill.color = new Color(0.35f, 0.85f, 0.4f, 1f);
            m_fill.raycastTarget = false;
        }

        private static GameObject NewChild(string name, Transform parent, Vector2 size, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            return go;
        }

        private void Update()
        {
            // 대상 despawn → 자멸
            if (m_follow == null)
            {
                Destroy(gameObject);
                return;
            }
            if (m_destroyAt > 0f && Time.time >= m_destroyAt)
            {
                Destroy(gameObject);
                return;
            }

            // 위치·빌보드 (수평 회전만 — VRUIPanel과 동일 규칙)
            transform.position = m_follow.position + Vector3.up * m_heightOffset;
            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector3 toCam = transform.position - cam.transform.position;
                toCam.y = 0f;
                if (toCam.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.LookRotation(toCam);
            }

            if (Time.time >= m_nextRefreshAt)
            {
                m_nextRefreshAt = Time.time + k_refreshInterval;
                Refresh();
            }
        }

        private void Refresh()
        {
            if (m_getHp == null || m_label == null) return;

            int hp  = Mathf.Max(0, m_getHp());
            int max = Mathf.Max(1, m_getMax());
            bool active = m_getActive != null && m_getActive();

            if (hp <= 0)
            {
                m_label.text = $"{m_displayName} · 처치!";
                if (m_destroyAt < 0f) m_destroyAt = Time.time + k_destroyDelayAfterKill;
            }
            else if (!active)
            {
                m_label.text = m_needPlayers > 1
                    ? $"{m_displayName} · {m_needPlayers}인 필요 (대기)"
                    : $"{m_displayName} · 대기";
            }
            else
            {
                m_label.text = $"{m_displayName} · {hp}/{max}";
            }

            float ratio = Mathf.Clamp01(hp / (float)max);
            m_fillRt.anchorMax = new Vector2(ratio, 1f);
            m_fillRt.offsetMax = Vector2.zero; // anchor 변경 후 오프셋 재고정
            m_fill.color = Color.Lerp(new Color(0.9f, 0.3f, 0.25f, 1f), new Color(0.35f, 0.85f, 0.4f, 1f), ratio);
        }
    }
}
