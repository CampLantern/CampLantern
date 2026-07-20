using System.Collections.Generic;
using UnityEngine;

namespace CampLantern.Player
{
    /// <summary>
    /// 범용 커스텀 손 부착 그랩 — 낚싯대 `RodGrabber`와 동일한 UX를 여러 아이템/양손용으로 일반화한 것.
    /// 조준(손 레이가 그립 지점 근처) → 트리거로 집으면 아이템이 손 앵커에 지정 그립 포즈로 스냅 부착,
    /// B/Y 버튼으로 내려놓기. Meta 물리 그랩(그립 축)이 시뮬레이터에서 안 잡히는 문제를 우회하고
    /// 손잡이 그립 포즈를 코드로 확정하기 위함(사용자 결정: 낚싯대와 동일 방식으로 통일).
    ///
    /// 낚싯대와 다른 점 3가지:
    ///   1. 양손 지원 — Auto면 조준·트리거한 손으로 잡힌다. 손당 1개만(정적 점유 레지스트리).
    ///   2. Meta 물리 그랩 중립화 — 아이템이 `Grabbable`/`GrabInteractable`을 달고 있으면(요리·무기
    ///      팩토리 산출물) Awake에서 비활성화한다. 두 그랩이 싸우지 않게. 프리팹 편집 없음(런타임).
    ///   3. Rigidbody 있으면 든 동안 kinematic — 놓으면 원복(선반/앵커 물리 복귀는 아이템 자기 로직).
    ///
    /// 그립 포즈(gripLocalPoint/heldEuler/palmOffset)는 아이템 지오메트리별로 `Configure`로 주입한다.
    /// 부착 위치 역산은 낚싯대와 동일: heldPos = palmOffset - heldRot * gripLocalPoint (손잡이가 손에 오도록).
    /// 배선은 소유자(하네스/조립기)가 런타임 AddComponent + Configure (RodGrabber와 동일 패턴).
    /// </summary>
    public class HeldItemGrabber : MonoBehaviour
    {
        public enum HandSide { Auto, Left, Right }

        /// <summary>내려놓기 동작 — 거치대로 복귀(국자·낚싯대) vs 물리로 떨어뜨림(재료: 냄비 투입).</summary>
        public enum ReleaseMode { ReturnToDock, Drop }

        // 손당 1개 점유 — 한 손이 이미 무언가 들고 있으면 다른 아이템이 그 손으로 잡히지 않게 막는다.
        private static readonly Dictionary<OVRInput.Controller, HeldItemGrabber> s_heldByHand
            = new Dictionary<OVRInput.Controller, HeldItemGrabber>();

        [SerializeField] private HandSide m_handSide = HandSide.Auto;
        [SerializeField] private ReleaseMode m_releaseMode = ReleaseMode.ReturnToDock;
        [SerializeField] private Vector3 m_gripLocalPoint = Vector3.zero;
        [SerializeField] private Vector3 m_heldEuler = new Vector3(10f, 0f, 0f);
        [SerializeField] private Vector3 m_palmOffset = new Vector3(0f, -0.02f, 0.03f);

        [SerializeField] private float m_grabThreshold = 0.6f;  // 그랩 확정 트리거 값
        [SerializeField] private float m_aimRadius     = 0.18f; // 조준 허용 반경(m) — 그립 지점까지 수직 거리
        [SerializeField] private float m_maxDistance   = 8f;    // 그랩 최대 거리(m)
        private const float k_grabProximityMin = 0.05f;         // 손 바로 뒤/옆 오검출 방지

        private Transform m_leftHand;
        private Transform m_rightHand;
        private LineRenderer m_line;

        private Rigidbody m_rb;
        private bool m_rbWasKinematic;
        private readonly List<Behaviour> m_metaGrabs = new List<Behaviour>(); // Grabbable/GrabInteractable

        private bool m_holding;
        private OVRInput.Controller m_heldBy;
        private Transform m_dockParent;   // 복귀용 — 부착 전 부모/월드 포즈
        private Vector3 m_dockLocalPos;
        private Quaternion m_dockLocalRot;
        private bool m_grabTriggerReleased; // 잡을 때 쥔 트리거를 한 번 놓았는가

        /// <summary>그립 포즈·손·내려놓기 동작을 주입한다. 소유자가 스폰/부착 직후 호출.</summary>
        public void Configure(Vector3 gripLocalPoint, Vector3 heldEuler, Vector3 palmOffset,
                              HandSide hand = HandSide.Auto, ReleaseMode releaseMode = ReleaseMode.ReturnToDock)
        {
            m_gripLocalPoint = gripLocalPoint;
            m_heldEuler = heldEuler;
            m_palmOffset = palmOffset;
            m_handSide = hand;
            m_releaseMode = releaseMode;
        }

        private void Awake()
        {
            m_rb = GetComponent<Rigidbody>();

            // Meta 물리 그랩 중립화 — 커스텀 부착과 충돌 방지. 타입명으로 찾아 비활성(어셈블리 직접 참조 회피).
            foreach (var b in GetComponents<Behaviour>())
            {
                string n = b.GetType().Name;
                if (n == "Grabbable" || n == "GrabInteractable" || n == "HandGrabInteractable")
                {
                    b.enabled = false;
                    m_metaGrabs.Add(b);
                }
            }

            // 조준 어포던스 라인 — Sprites/Default는 항상 빌드 포함 + 버텍스 컬러 지원
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
            {
                m_leftHand  = rig.leftControllerAnchor  != null ? rig.leftControllerAnchor  : rig.leftHandAnchor;
                m_rightHand = rig.rightControllerAnchor != null ? rig.rightControllerAnchor : rig.rightHandAnchor;
            }
        }

        private void Update()
        {
            if (m_holding)
            {
                UpdateHolding();
                return;
            }

            m_line.enabled = false;
            // 잡을 수 있는 손 후보를 순회 — Auto면 양손, 아니면 지정 손만
            if (ShouldTry(HandSide.Right)) TryAim(m_rightHand, OVRInput.Controller.RTouch);
            if (!m_holding && ShouldTry(HandSide.Left)) TryAim(m_leftHand, OVRInput.Controller.LTouch);
        }

        private bool ShouldTry(HandSide side)
        {
            if (m_handSide != HandSide.Auto && m_handSide != side) return false;
            var controller = side == HandSide.Right ? OVRInput.Controller.RTouch : OVRInput.Controller.LTouch;
            Transform hand = side == HandSide.Right ? m_rightHand : m_leftHand;
            if (hand == null || !OVRInput.IsControllerConnected(controller)) return false;
            // 이 손이 이미 다른 아이템을 들고 있으면 시도 안 함 (손당 1개)
            return !(s_heldByHand.TryGetValue(controller, out var occupant) && occupant != null && occupant != this);
        }

        private void TryAim(Transform hand, OVRInput.Controller controller)
        {
            Vector3 origin = hand.position;
            Vector3 dir    = hand.forward;
            Vector3 grip   = transform.TransformPoint(m_gripLocalPoint); // 이 아이템의 손잡이 지점(월드)
            Vector3 toGrip = grip - origin;
            float proj     = Vector3.Dot(toGrip, dir);
            bool aiming    = proj > k_grabProximityMin && proj < m_maxDistance &&
                             (toGrip - dir * proj).sqrMagnitude < m_aimRadius * m_aimRadius;
            if (!aiming) return;

            float trigger = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, controller);

            m_line.enabled = true;
            m_line.SetPosition(0, origin);
            m_line.SetPosition(1, origin + dir * Mathf.Min(proj, m_maxDistance));
            Color c = Color.Lerp(new Color(0.75f, 0.95f, 0.8f, 0.55f), new Color(0.3f, 1f, 0.45f, 1f),
                                 Mathf.InverseLerp(0f, m_grabThreshold, trigger));
            m_line.startColor = c;
            m_line.endColor   = new Color(c.r, c.g, c.b, 0.1f);

            if (trigger >= m_grabThreshold) Grab(hand, controller);
        }

        private void Grab(Transform hand, OVRInput.Controller controller)
        {
            m_dockParent   = transform.parent;
            m_dockLocalPos = transform.localPosition;
            m_dockLocalRot = transform.localRotation;

            if (m_rb != null)
            {
                m_rbWasKinematic = m_rb.isKinematic;
                m_rb.isKinematic = true; // 든 동안 물리 정지 (1회 구성 — RULE-03 조작 아님)
            }

            var heldRot = Quaternion.Euler(m_heldEuler);
            transform.SetParent(hand, false);
            transform.localRotation = heldRot;
            transform.localPosition = m_palmOffset - (heldRot * m_gripLocalPoint); // 손잡이가 손에 오도록 역산

            m_holding = true;
            m_heldBy = controller;
            m_grabTriggerReleased = false;
            s_heldByHand[controller] = this;
            m_line.enabled = false;

            OVRInput.SetControllerVibration(0.4f, 0.3f, controller); // 잡힘 피드백 한 펄스
        }

        private void UpdateHolding()
        {
            float trigger = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, m_heldBy);
            if (!m_grabTriggerReleased && trigger < 0.15f) m_grabTriggerReleased = true;

            if (OVRInput.GetDown(OVRInput.Button.Two, m_heldBy)) // B(우)/Y(좌) = 내려놓기
                Release();
        }

        private void Release()
        {
            if (m_releaseMode == ReleaseMode.ReturnToDock)
            {
                transform.SetParent(m_dockParent, false);
                transform.localPosition = m_dockLocalPos;
                transform.localRotation = m_dockLocalRot;
                if (m_rb != null) m_rb.isKinematic = m_rbWasKinematic;
            }
            else // Drop — 손을 떠나 현재 위치에서 물리로 떨어진다 (재료: 냄비 투입/낙사 정리에 맡김)
            {
                transform.SetParent(null, true); // 월드 위치 유지
                if (m_rb != null) m_rb.isKinematic = false;
            }

            if (s_heldByHand.TryGetValue(m_heldBy, out var occupant) && occupant == this)
                s_heldByHand.Remove(m_heldBy);

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
