using UnityEngine;
using UnityEngine.InputSystem;

namespace CampLantern.Fishing
{
    /// <summary>
    /// VR 입력·햅틱 어댑터 (§8) — 감지→코어 호출만 하는 얇은 층. 판정(유효 상태)은 전부 코어가 한다.
    ///   릴링 = 트리거 홀드(크랭크 아님) / 챔질·낚아올림 = 낚싯대 들어올리기(상향 각속도, 상태로 구분)
    ///   비늘털이 대응 = 좌/우 스윙(각속도 + 방향 전환) / 캐스팅 = 스윙으로 장전 후 릴리즈
    /// 햅틱: 줄 색과 동기화(§8) — 흰=약한 릴링 진동, 빨강=강한 경고, 초록=성공 펄스. 화면 응시 불필요(대화 병행).
    ///
    /// 데스크톱 폴백(개발용, Quest 빌드 전 제거 대상): 프로젝트가 New Input System 전용(activeInputHandler=1)이라
    /// legacy Input 대신 InputSystem 키보드/마우스 사용 — C=캐스팅, Space=챔질/낚아올림, 마우스 좌클릭 홀드=릴링, A/D=스윙.
    /// </summary>
    public class FishingRodInput : MonoBehaviour
    {
        [SerializeField] private FishingRod m_rod;
        [SerializeField] private FishingSpot m_spot; // 캐스팅 타겟 질의 — 미할당이면 Awake에서 검색
        [SerializeField] private FishingTuning m_tuning = new FishingTuning();
        [SerializeField] private OVRInput.Controller m_controller = OVRInput.Controller.RTouch;

        [Tooltip("릴링 트리거 임계값 (0~1)")]
        [SerializeField] private float m_triggerThreshold = 0.5f;        // TODO(TUNING): 실기 테스트 후 확정

        [Tooltip("챔질/낚아올림 — 들어올리기 최소 피치 각속도(deg/s)")]
        [SerializeField] private float m_liftMinAngularSpeed = 240f;     // TODO(TUNING): VR 실기 테스트로 확정

        [Tooltip("캐스팅 장전 — 스윙 최소 각속도(deg/s)")]
        [SerializeField] private float m_castMinAngularSpeed = 240f;     // TODO(TUNING): VR 실기 테스트로 확정

        [Tooltip("캐스팅 장전 유지 시간(초) — 이 안에 트리거를 떼면 캐스팅")]
        [SerializeField] private float m_castArmSeconds = 0.5f;          // TODO(TUNING)

        [Tooltip("제스처(챔질/스윙) 재발화 방지 쿨다운(초)")]
        [SerializeField] private float m_gestureCooldown = 0.3f;         // TODO(TUNING)

        // 스윙(좌/우) 감지 상태 — §7-5: 각속도 임계 이상 + 방향 전환 N회
        private float m_lastYawSign;
        private int m_dirChanges;
        private float m_swingResetTimer;
        private float m_cooldownUntil;
        private float m_castArmedUntil;
        private bool m_prevTriggerHeld;

        private void Awake()
        {
            // 배선 누락 대비 — 코드로 확정 (rules/scripts.md)
            if (m_spot == null) m_spot = FindFirstObjectByType<FishingSpot>();
        }

        private void Update()
        {
            if (m_rod == null) return;

            // ── 각속도 (rad/s → deg/s). 컨트롤러 부재 시 0 벡터 — NRE 없음 ──
            Vector3 angular = OVRInput.GetLocalControllerAngularVelocity(m_controller) * Mathf.Rad2Deg;
            Keyboard kb = Keyboard.current; // 없으면 null (SSH/헤드리스 등)

            UpdateReeling(kb);
            UpdateSwing(angular, kb);
            UpdateLiftGesture(angular, kb);
            UpdateCast(angular, kb);
            UpdateHaptics();
        }

        // ── 릴링: 트리거 홀드 (§8) ──────────────────────────────────

        private void UpdateReeling(Keyboard kb)
        {
            bool vrHold   = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, m_controller) > m_triggerThreshold;
            bool deskHold = Mouse.current != null && Mouse.current.leftButton.isPressed;
            m_rod.SetReeling(vrHold || deskHold);
        }

        // ── 좌/우 스윙: 비늘털이 QTE (§7-5) ─────────────────────────

        private void UpdateSwing(Vector3 angularDeg, Keyboard kb)
        {
            float yaw = angularDeg.y;

            if (Mathf.Abs(yaw) >= m_tuning.swingMinAngularSpeed)
            {
                float sign = Mathf.Sign(yaw);
                if (m_lastYawSign != 0f && !Mathf.Approximately(sign, m_lastYawSign))
                    m_dirChanges++;
                m_lastYawSign    = sign;
                m_swingResetTimer = 0.6f; // 전환 카운트 유지 창

                if (m_dirChanges >= m_tuning.swingMinDirChanges && Time.time >= m_cooldownUntil)
                {
                    FireSwing();
                    return;
                }
            }

            m_swingResetTimer -= Time.deltaTime;
            if (m_swingResetTimer <= 0f)
            {
                m_lastYawSign = 0f;
                m_dirChanges  = 0;
            }

            // 데스크톱 폴백: A/D 키 = 스윙
            if (kb != null && (kb.aKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame))
                FireSwing();
        }

        private void FireSwing()
        {
            m_cooldownUntil = Time.time + m_gestureCooldown;
            m_lastYawSign   = 0f;
            m_dirChanges    = 0;
            if (m_rod.CurrentFish != null) m_rod.CurrentFish.OnSwing(); // 유효성(체공 중)은 Fish가 판정
        }

        // ── 들어올리기: 챔질(Bite) / 낚아올림(Hooked) — 같은 제스처, 상태로 구분 (§8) ──

        private void UpdateLiftGesture(Vector3 angularDeg, Keyboard kb)
        {
            bool vrLift   = Mathf.Abs(angularDeg.x) >= m_liftMinAngularSpeed;
            bool deskLift = kb != null && kb.spaceKey.wasPressedThisFrame;
            if (!vrLift && !deskLift) return;
            if (Time.time < m_cooldownUntil) return;
            m_cooldownUntil = Time.time + m_gestureCooldown;

            Fish fish = m_rod.CurrentFish;
            if (fish != null && fish.State == FishState.Hooked)
                fish.Hook();       // 낚아올림 (§2-5)
            else
                m_rod.Chamjil();   // 챔질 — Bite 밖이면 코어가 무시/취소 처리
        }

        // ── 캐스팅: 스윙으로 장전 → 릴리즈 (§8) ─────────────────────

        private void UpdateCast(Vector3 angularDeg, Keyboard kb)
        {
            // 스윙 속도 임계 초과 → 잠깐 동안 "장전"
            if (Mathf.Abs(angularDeg.y) >= m_castMinAngularSpeed || Mathf.Abs(angularDeg.x) >= m_castMinAngularSpeed)
                m_castArmedUntil = Time.time + m_castArmSeconds;

            bool triggerHeld = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, m_controller) > m_triggerThreshold;
            bool vrRelease   = m_prevTriggerHeld && !triggerHeld && Time.time <= m_castArmedUntil;
            m_prevTriggerHeld = triggerHeld;

            bool deskCast = kb != null && kb.cKey.wasPressedThisFrame;

            if (vrRelease || deskCast)
                CastNearest();
        }

        private void CastNearest()
        {
            if (m_spot == null) return;
            if (m_spot.TryGetNearestFish(m_rod.transform.position, m_rod.Rod.length, out Fish target))
                m_rod.Cast(target);
        }

        // ── 햅틱: 줄 색 동기화 (§8) — 매 프레임 갱신(OVR 진동은 ~2초 자동 정지) ──

        private void UpdateHaptics()
        {
            Fish fish = m_rod.CurrentFish;
            LineColor line = fish != null ? fish.Line : LineColor.None;

            switch (line)
            {
                case LineColor.White: // 약한 릴링 진동 — 릴링 중에만
                    OVRInput.SetControllerVibration(0.3f, m_rod.Reeling ? 0.15f : 0f, m_controller);
                    break;
                case LineColor.Red:   // 강한 경고 진동 — "트리거를 떼라"
                    OVRInput.SetControllerVibration(0.8f, 0.6f, m_controller);
                    break;
                case LineColor.Green: // 성공 펄스
                    float pulse = Mathf.PingPong(Time.time * 3f, 1f) > 0.5f ? 0.5f : 0f;
                    OVRInput.SetControllerVibration(0.5f, pulse, m_controller);
                    break;
                default:
                    OVRInput.SetControllerVibration(0f, 0f, m_controller);
                    break;
            }
        }

        private void OnDisable()
        {
            // 진동 잔류 방지
            OVRInput.SetControllerVibration(0f, 0f, m_controller);
        }
    }
}
