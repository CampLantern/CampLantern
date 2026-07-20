using UnityEngine;

namespace CampLantern.Fishing
{
    /// <summary>
    /// 낚싯대 원거리 그랩 — 오른손으로 낚싯대를 조준하면 손에서 그랩 라인이 자동으로 나가고
    /// ("잡을 수 있음" 어포던스), 그 상태에서 트리거를 꽉 쥐면 대가 손으로 날아와 붙는다
    /// (컨트롤러 앵커에 부모화). 내려놓기는 B 버튼.
    ///
    /// 트리거가 그랩 버튼인 이유(사용자 결정): 릴링/캐스팅과 같은 손가락으로 잡기까지 이어지는 게
    /// 직관적이고, Meta XR Simulator에서도 확실히 눌리는 입력이다(그립은 시뮬레이터 바인딩이 애매).
    /// 대신 든 동안 트리거는 릴링에 쓰이므로:
    ///   - 내려놓기는 B 버튼 (트리거와 분리)
    ///   - 잡을 때 쥔 트리거를 한 번 놓기 전까지는 낚시 제스처(FishingRodInput)를 켜지 않는다
    ///     — 잡는 즉시 릴링/캐스팅으로 새는 오발 방지
    ///
    /// FishingRodInput(제스처→코어 호출)과 역할 분리 — 이 컴포넌트는 "대를 누가 들고 있나"만 다룬다.
    /// 빈손 제스처 오발 방지: VR 컨트롤러가 연결된 동안엔 대를 든 상태에서만 FishingRodInput을 켠다.
    /// 데스크톱(컨트롤러 미연결)에서는 항상 켜 둬 키보드 폴백(C/Space/클릭/A/D)이 그대로 동작한다.
    ///
    /// 배선: FishingGroundHarness가 런타임에 낚싯대 GO에 AddComponent (FishingRodInput과 동일 패턴).
    /// </summary>
    public class RodGrabber : MonoBehaviour
    {
        [SerializeField] private FishingRod m_rod;
        [SerializeField] private OVRInput.Controller m_controller = OVRInput.Controller.RTouch;

        [Tooltip("그랩 확정 트리거 값")]
        [SerializeField] private float m_grabThreshold = 0.6f;

        [Tooltip("조준 허용 반경(m) — 손 레이와 손잡이 중심의 수직 거리")]
        [SerializeField] private float m_aimRadius = 0.4f;

        [Tooltip("그랩 최대 거리(m)")]
        [SerializeField] private float m_maxDistance = 8f;

        // 손에 쥐었을 때의 로컬 포즈 — "손잡이를 실제로 쥔" 자세.
        // 손잡이 지점(k_gripLocalPoint)이 손 앵커 안에 오도록 든 위치를 역산한다:
        //   heldRot * grip + heldPos = palmOffset  →  heldPos = palmOffset - heldRot * grip
        //
        // 그립 지점은 실제 씬 낚싯대 = RvrfTackleFactory가 스왑한 RVRF 모델(Rod_Float_01) 기준:
        //   FBX 피벗 = 손잡이 밑동, rod-local (0, 0.7, 0.1)에 배치 + 28° 전방 기울기(RvrfTackleFactory).
        //   샤프트 단위방향 rotX(28)*(0,1,0)=(0,0.883,0.469) 로 밑동 위 0.15m = 손잡이(릴 밑) → (0, 0.83, 0.17).
        //   (구 그레이박스 원통 기준 0.26이면 실제 손잡이보다 0.5m 아래를 잡아 릴이 머리 위로 떴었다.)
        // TODO(TUNING): 실기 미세조정 — 더 위/아래로 잡으려면 y, 손바닥 깊이는 k_palmOffset.
        private static readonly Vector3    k_gripLocalPoint = new Vector3(0f, 0.83f, 0.17f);
        private static readonly Quaternion k_heldLocalRot   = Quaternion.Euler(10f, 0f, 0f);
        private static readonly Vector3    k_palmOffset     = new Vector3(0f, -0.02f, 0.03f); // 손바닥 안쪽으로 살짝
        private static readonly Vector3    k_heldLocalPos   = k_palmOffset - (k_heldLocalRot * k_gripLocalPoint);
        private const float k_grabProximityMin = 0.2f; // 손 바로 뒤/옆 오검출 방지

        private Transform m_hand;        // 오른손 컨트롤러 앵커 (PersistentPlayer 리그)
        private FishingRodInput m_input; // 든 동안(그랩 트리거 해제 후)에만 활성화 (VR)
        private LineRenderer m_line;
        private Transform m_dockParent;  // 복귀용 — 거치 시점의 부모/로컬 포즈
        private Vector3 m_dockLocalPos;
        private Quaternion m_dockLocalRot;
        private bool m_holding;
        private bool m_grabTriggerReleased; // 잡을 때 쥔 트리거를 한 번 놓았는가 — 놓기 전엔 제스처 게이트

        private void Awake()
        {
            // 배선 누락 대비 — 코드로 확정 (rules/scripts.md)
            if (m_rod == null) m_rod = GetComponent<FishingRod>();
            if (m_rod == null) m_rod = FindFirstObjectByType<FishingRod>();

            // 손에서 나가는 그랩 라인 — Sprites/Default는 항상 빌드에 포함되고 버텍스 컬러를 지원
            var lineGo = new GameObject("GrabLine");
            lineGo.transform.SetParent(transform, false);
            m_line = lineGo.AddComponent<LineRenderer>();
            m_line.positionCount = 2;
            m_line.startWidth = 0.006f;
            m_line.endWidth = 0.002f;
            m_line.material = new Material(Shader.Find("Sprites/Default"));
            m_line.enabled = false;
        }

        private void Start()
        {
            // 영속 리그는 PersistentPlayer가 스폰하므로 Awake가 아닌 Start에서 해석
            var rig = FindFirstObjectByType<OVRCameraRig>();
            if (rig != null)
                m_hand = rig.rightControllerAnchor != null ? rig.rightControllerAnchor : rig.rightHandAnchor;

            // 하네스가 FishingRodInput을 먼저 AddComponent한 뒤 이 컴포넌트를 붙인다 — Start 시점엔 존재
            m_input = FindFirstObjectByType<FishingRodInput>();
        }

        private void Update()
        {
            bool vrActive = m_hand != null && OVRInput.IsControllerConnected(m_controller);

            // 제스처 게이트 — VR이면 "들고 + 그랩 트리거를 한 번 놓은 뒤"에만, 데스크톱이면 항상
            if (m_input != null)
                m_input.enabled = !vrActive || (m_holding && m_grabTriggerReleased);

            if (!vrActive || m_rod == null)
            {
                if (m_line.enabled) m_line.enabled = false;
                return;
            }

            float trigger = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, m_controller);

            if (m_holding)
            {
                if (!m_grabTriggerReleased && trigger < 0.15f)
                    m_grabTriggerReleased = true;

                if (OVRInput.GetDown(OVRInput.Button.Two, m_controller)) // B 버튼 = 내려놓기
                    Release();
                return;
            }

            // 레이-손잡이 근접 판정 — 물리 레이캐스트 대신 수학으로 (Ground 콜라이더에 가로막히지 않게).
            // 조준되면 버튼 입력 없이도 그랩 라인이 자동 표시된다 — "여기 잡을 수 있음" 어포던스.
            Vector3 origin = m_hand.position;
            Vector3 dir    = m_hand.forward;
            Vector3 handle = m_rod.transform.position + Vector3.up * 0.6f; // 그랩 볼륨(BoxCollider) 중심 높이
            Vector3 toRod  = handle - origin;
            float proj     = Vector3.Dot(toRod, dir);
            bool aiming    = proj > k_grabProximityMin && proj < m_maxDistance &&
                             (toRod - dir * proj).sqrMagnitude < m_aimRadius * m_aimRadius;

            m_line.enabled = aiming;
            if (aiming)
            {
                // 레이는 컨트롤러가 가리키는 방향 그대로 직진 — 끝점만 낚싯대 근처(레이 위 최근접점)에서 멈춘다.
                // 손잡이에 스냅시키면 컨트롤러를 움직여도 라인이 낚싯대에 붙어 있는 것처럼 보여 방향감이 깨진다.
                m_line.SetPosition(0, origin);
                m_line.SetPosition(1, origin + dir * Mathf.Min(proj, m_maxDistance));
                // 트리거를 쥘수록 초록이 진해진다 — 잡기 진행 피드백
                Color c = Color.Lerp(new Color(0.75f, 0.95f, 0.8f, 0.55f), new Color(0.3f, 1f, 0.45f, 1f),
                                     Mathf.InverseLerp(0f, m_grabThreshold, trigger));
                m_line.startColor = c;
                m_line.endColor   = new Color(c.r, c.g, c.b, 0.1f);

                if (trigger >= m_grabThreshold) Grab();
            }
        }

        private void Grab()
        {
            m_dockParent   = m_rod.transform.parent;
            m_dockLocalPos = m_rod.transform.localPosition;
            m_dockLocalRot = m_rod.transform.localRotation;

            m_rod.transform.SetParent(m_hand, false);
            m_rod.transform.localPosition = k_heldLocalPos;
            m_rod.transform.localRotation = k_heldLocalRot;

            m_holding = true;
            m_grabTriggerReleased = false; // 이 트리거를 놓기 전까진 제스처 잠금
            m_line.enabled = false;

            // 잡힘 피드백 한 펄스 — 이후 프레임은 FishingRodInput의 줄 색 햅틱이 덮는다
            OVRInput.SetControllerVibration(0.4f, 0.3f, m_controller);
        }

        private void Release()
        {
            m_rod.transform.SetParent(m_dockParent, false);
            m_rod.transform.localPosition = m_dockLocalPos;
            m_rod.transform.localRotation = m_dockLocalRot;
            m_holding = false;
            m_grabTriggerReleased = false;
        }

        private void OnDisable()
        {
            if (m_holding) Release();
            if (m_line != null) m_line.enabled = false;
        }
    }
}
