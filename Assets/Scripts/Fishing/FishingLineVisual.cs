using UnityEngine;

namespace CampLantern.Fishing
{
    /// <summary>
    /// 낚싯줄 + 찌 연출 (§2). 줄 색(흰/빨/초)은 파이팅의 유일한 판단 신호(§1)라 여기서 실체화된다.
    ///   - Approach: 찌를 미끼 착수 지점에 표시(잔잔한 부유), 줄 = 로드 팁 → 찌
    ///   - Bite: 찌 상하 흔들림(§2-2 입질 연출)
    ///   - Fight~Hooked: 찌 숨김, 줄 = 로드 팁 → 물고기(끌려다니는 연출), 색 = Fish.Line
    /// 표시 전용 — rod.CurrentFish를 폴링만 하고 게임 상태를 바꾸지 않는다. 90Hz 경량.
    /// 배선은 RvrfTackleFactory(Tools > Make Assets > RVRF Tackle — Swap Rod Visual)가 한다.
    /// </summary>
    public class FishingLineVisual : MonoBehaviour
    {
        [SerializeField] private FishingRod m_rod;
        [SerializeField] private Transform m_tipAnchor;   // 로드 팁 (rod FBX 상단, 팩토리 배선)
        [SerializeField] private GameObject m_bobber;     // 찌 (Bobber0 FBX — 피벗이 줄 매듭점)
        [SerializeField] private GameObject m_bait;       // 미끼 왁스웜 (찌 자식) — Approach에만 표시, 입질에 사라짐(§4 소모 가시화)
        [SerializeField] private LineRenderer m_line;

        [Tooltip("입질 흔들림 진폭(m) — §2-2 찌 상하 흔들림")]
        [SerializeField] private float m_biteBobAmplitude = 0.06f;

        [Tooltip("입질 흔들림 주파수(Hz)")]
        [SerializeField] private float m_biteBobFrequency = 7f;

        [Tooltip("평상(접근 중) 부유 진폭(m)")]
        [SerializeField] private float m_idleBobAmplitude = 0.015f;

        private void Awake()
        {
            // 배선 누락 대비 + 초기 비표시는 소유 매니저(이 컴포넌트)가 코드로 확정 (rules/scripts.md)
            if (m_rod == null) m_rod = GetComponent<FishingRod>();
            if (m_bobber != null) m_bobber.SetActive(false);
            if (m_bait != null) m_bait.SetActive(false);
            if (m_line != null) m_line.enabled = false;
        }

        private void Update()
        {
            Fish fish = m_rod != null ? m_rod.CurrentFish : null;
            if (fish == null)
            {
                Hide();
                return;
            }

            switch (fish.State)
            {
                case FishState.Approach:
                    ShowBobber(fish.BaitPoint, m_idleBobAmplitude, 1.5f);
                    SetBaitVisible(true); // 미끼가 걸린 낚싯대 (§2-1)
                    DrawLine(BobberKnot(), LineColorOf(fish.Line));
                    break;

                case FishState.Bite:
                    ShowBobber(fish.BaitPoint, m_biteBobAmplitude, m_biteBobFrequency); // §2-2
                    SetBaitVisible(false); // 물고기가 물었다 — 미끼 -1(§4)의 가시화
                    DrawLine(BobberKnot(), LineColorOf(fish.Line));
                    break;

                case FishState.FightNormal:
                case FishState.FightEscape:
                case FishState.FightShake:
                case FishState.Hooked:
                    if (m_bobber != null && m_bobber.activeSelf) m_bobber.SetActive(false);
                    DrawLine(fish.transform.position, LineColorOf(fish.Line));
                    break;

                default:
                    Hide();
                    break;
            }
        }

        private void ShowBobber(Vector3 baitPoint, float amplitude, float frequency)
        {
            if (m_bobber == null) return;
            if (!m_bobber.activeSelf) m_bobber.SetActive(true);

            // 피벗(줄 매듭점)을 수면 살짝 위에 두고 몸통은 물에 잠기게 + 상하 흔들림
            float bob = Mathf.Sin(Time.time * frequency * Mathf.PI * 2f) * amplitude;
            m_bobber.transform.position = baitPoint + Vector3.up * (0.04f + bob);
        }

        private void SetBaitVisible(bool visible)
        {
            if (m_bait != null && m_bait.activeSelf != visible) m_bait.SetActive(visible);
        }

        private Vector3 BobberKnot() =>
            m_bobber != null ? m_bobber.transform.position : Vector3.zero;

        private void DrawLine(Vector3 end, Color color)
        {
            if (m_line == null || m_tipAnchor == null) return;
            if (!m_line.enabled) m_line.enabled = true;

            m_line.SetPosition(0, m_tipAnchor.position);
            m_line.SetPosition(1, end);
            m_line.startColor = color;
            m_line.endColor   = color;
        }

        private void Hide()
        {
            if (m_bobber != null && m_bobber.activeSelf) m_bobber.SetActive(false);
            if (m_line != null && m_line.enabled) m_line.enabled = false;
        }

        // §2 줄 색 — None(입질 전)은 옅은 흰색으로 존재감만
        private static Color LineColorOf(LineColor line) => line switch
        {
            LineColor.White => Color.white,
            LineColor.Red   => new Color(1f, 0.25f, 0.2f),
            LineColor.Green => new Color(0.3f, 1f, 0.4f),
            _               => new Color(1f, 1f, 1f, 0.6f),
        };
    }
}
