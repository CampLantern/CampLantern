using System;
using CampLantern.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CampLantern.UI
{
    /// <summary>
    /// 왼손 손목 HUD — 코인 잔액(+씬별 추가 한 줄, 예: 낚시 미끼·내구도)을 손목시계처럼 표시한다.
    /// IMGUI의 "코인: {n}" 라벨의 실사용 VR 대체물. 표시 전용이라 UI 전체를 코드로 생성한다.
    ///
    /// 코인은 Wallet.CoinsChanged 이벤트 구동, 추가 줄은 저빈도 폴링(0.5s).
    /// 왼손 컨트롤러 앵커(영속 리그)에 부착되므로 씬 전환 시 하네스가 파괴 책임을 진다
    /// (리그는 DontDestroyOnLoad — 방치하면 씬마다 HUD가 쌓인다). 소유자: 각 공간 하네스.
    /// </summary>
    public class WristHud : MonoBehaviour
    {
        private const float k_refreshInterval = 0.5f;

        private Wallet m_wallet;
        private Func<string> m_extraProvider;
        private TextMeshProUGUI m_coinLabel;
        private TextMeshProUGUI m_extraLabel;
        private float m_nextRefreshAt;
        private bool m_attached;

        /// <summary>손목 HUD 생성. extraProvider는 둘째 줄 텍스트(없으면 줄 숨김). 소유자가 파괴를 책임진다.</summary>
        public static WristHud Spawn(Wallet wallet, Func<string> extraProvider = null)
        {
            var go = new GameObject("WristHud");
            var hud = go.AddComponent<WristHud>();
            hud.m_wallet = wallet;
            hud.m_extraProvider = extraProvider;
            hud.BuildUI();

            if (wallet != null)
            {
                wallet.CoinsChanged -= hud.OnCoinsChanged;
                wallet.CoinsChanged += hud.OnCoinsChanged;
            }
            hud.RefreshCoins();
            hud.RefreshExtra();
            return hud;
        }

        private void BuildUI()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)transform;
            rt.sizeDelta = new Vector2(260f, 110f);
            transform.localScale = Vector3.one * 0.0008f; // 260px ≈ 0.21m — 손목 크기

            var bg = NewChild("Bg", new Vector2(260f, 110f), Vector2.zero).AddComponent<Image>();
            bg.color = new Color(0.08f, 0.08f, 0.1f, 0.75f);
            bg.raycastTarget = false;

            m_coinLabel = NewChild("Coins", new Vector2(240f, 46f), new Vector2(0f, 24f)).AddComponent<TextMeshProUGUI>();
            SetupLabel(m_coinLabel, 30f, new Color(1f, 0.85f, 0.4f, 1f)); // 골드 톤

            m_extraLabel = NewChild("Extra", new Vector2(240f, 40f), new Vector2(0f, -24f)).AddComponent<TextMeshProUGUI>();
            SetupLabel(m_extraLabel, 22f, new Color(0.85f, 0.9f, 0.95f, 1f));
            m_extraLabel.gameObject.SetActive(m_extraProvider != null);
        }

        private static void SetupLabel(TextMeshProUGUI label, float size, Color color)
        {
            label.fontSize      = size;
            label.alignment     = TextAlignmentOptions.Center;
            label.color         = color;
            label.raycastTarget = false;
            label.richText      = false;
        }

        private GameObject NewChild(string name, Vector2 size, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            return go;
        }

        private void Update()
        {
            // 영속 리그는 스폰 타이밍이 늦을 수 있어 붙을 때까지 재시도
            if (!m_attached) TryAttachToWrist();

            if (Time.time < m_nextRefreshAt) return;
            m_nextRefreshAt = Time.time + k_refreshInterval;
            RefreshExtra();
        }

        private void TryAttachToWrist()
        {
            var rig = FindFirstObjectByType<OVRCameraRig>();
            if (rig == null || rig.leftHandAnchor == null) return;

            transform.SetParent(rig.leftHandAnchor, false);
            transform.localPosition = new Vector3(0f, 0.04f, -0.09f);       // 손목 위 — TODO(TUNING): 실기 확인
            transform.localRotation = Quaternion.Euler(55f, 0f, 0f);        // 시계 보듯 위를 향함
            m_attached = true;
        }

        private void OnCoinsChanged(int coins) => RefreshCoins();

        private void RefreshCoins()
        {
            if (m_coinLabel != null)
                m_coinLabel.text = m_wallet != null ? $"코인 {m_wallet.Coins}" : "코인 -";
        }

        private void RefreshExtra()
        {
            if (m_extraProvider == null || m_extraLabel == null) return;
            m_extraLabel.text = m_extraProvider();
        }

        private void OnDestroy()
        {
            if (m_wallet != null) m_wallet.CoinsChanged -= OnCoinsChanged;
        }
    }
}
