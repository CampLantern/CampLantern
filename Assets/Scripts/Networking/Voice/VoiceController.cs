using Fusion;
using Photon.Voice.Fusion;
using Photon.Voice.Unity;
using UnityEngine;

namespace CampLantern.Networking.Voice
{
    /// <summary>
    /// Fusion 세션에 Photon Voice 연결을 붙이고 로컬 마이크 송신을 제어한다 (step-09).
    /// SessionLauncher가 세션을 시작하면 같은 GameObject에 FusionVoiceClient/Recorder를 런타임 부착하고
    /// 음성 아바타(VoiceNetworkObject 프리팹)를 스폰한다. 파티 무전 채널은 P2 — 채널 개념을 넣지 않는다.
    /// </summary>
    [RequireComponent(typeof(SessionLauncher))]
    public class VoiceController : MonoBehaviour
    {
        // VoicePlayer 프리팹 (NetworkObject + VoiceNetworkObject + Speaker + AudioSource).
        // Editor 메뉴 Tools > Make Assets > Voice Player Prefab 으로 생성 후 씬에서 수동 할당.
        [SerializeField] private NetworkObject m_voicePlayerPrefab;

        // 마이크 토글은 씬(공간) 이동 시 이 컴포넌트가 씬과 함께 파괴돼도 유지돼야 한다 —
        // 유실되면 마이크를 껐던 유저가 이동 후 소리 없이 다시 송신하게 된다 (GDD 6-4 안전장치 위반).
        // 프로세스 수명 동안 마지막 토글 상태를 보존한다.
        private static bool s_micPreference = true;

        private SessionLauncher m_launcher;
        private PlayerMute m_playerMute;   // 같은 GO에 있으면 세션 시작 시 바인딩 (없어도 음성 자체는 동작)
        private FusionVoiceClient m_voiceClient;
        private Recorder m_recorder;
        private bool m_micEnabled = s_micPreference; // Recorder 생성 전 SetMicEnabled 호출 대비 희망 상태 보관

        /// <summary>로컬 마이크 송신 여부.</summary>
        public bool MicEnabled => m_recorder != null ? m_recorder.TransmitEnabled : m_micEnabled;

        /// <summary>자기 마이크 on/off. 세션 시작 전에 호출하면 시작 시점에 반영된다.</summary>
        public void SetMicEnabled(bool enabled)
        {
            m_micEnabled    = enabled;
            s_micPreference = enabled; // 다음 씬의 VoiceController가 이 상태로 시작
            if (m_recorder != null) m_recorder.TransmitEnabled = enabled;
        }

        private void Awake()
        {
            m_launcher   = GetComponent<SessionLauncher>();
            m_playerMute = GetComponent<PlayerMute>();

            m_launcher.SessionStarted -= OnSessionStarted;
            m_launcher.SessionStarted += OnSessionStarted;
        }

        private void OnDestroy()
        {
            if (m_launcher != null) m_launcher.SessionStarted -= OnSessionStarted;
            TeardownVoice();
        }

        private void Update()
        {
            // SessionLauncher에 세션 종료 이벤트가 없어(step-07 시그니처 유지) Runner 소멸을 폴링으로 감지한다
            if (m_voiceClient != null && m_launcher.Runner == null)
                TeardownVoice();
        }

        private void OnSessionStarted(NetworkRunner runner)
        {
            if (m_voiceClient != null)
            {
                Debug.LogWarning("[VoiceController] 이미 Voice가 붙어 있음 — 중복 세션 시작 무시");
                return;
            }

            // Recorder 초기값은 코드로 확정 (rules/scripts.md)
            m_recorder = gameObject.AddComponent<Recorder>();
            m_recorder.RecordingEnabled = true;
            m_recorder.TransmitEnabled  = m_micEnabled;
            // 데스크톱/Quest 마이크는 대부분 48kHz 고정 — 기본값 24kHz면 매 세션 불일치 경고 2종 + 리샘플 비용 발생
            m_recorder.SamplingRate     = POpusCodec.Enums.SamplingRate.Sampling48000;

            // FusionVoiceClient는 NetworkRunner와 같은 GO 필수(RequireComponent) — SessionLauncher가
            // 러너를 자기 GO에 붙이므로 여기도 같은 GO에 부착한다. 다음 Start()의 FollowLeader()가
            // 이미 시작된 세션을 따라 Voice 룸("{세션명}_voice")에 접속한다.
            m_voiceClient = gameObject.AddComponent<FusionVoiceClient>();
            m_voiceClient.UseFusionAppSettings = true;   // PhotonAppSettings의 App Id Voice 필드 사용
            m_voiceClient.UseFusionAuthValues  = false;  // Fusion 2.1 수술로 비활성 (tech-stack-decisions.md)
            m_voiceClient.PrimaryRecorder      = m_recorder; // VoiceNetworkObject가 이 Recorder를 가져다 쓴다

            // 세션 시작 후 부착이라 러너의 자동 콜백 수집에서 빠짐 — 직접 등록해야 접속 해제 등을 따라간다
            runner.AddCallbacks(m_voiceClient);

            if (m_voicePlayerPrefab != null)
                runner.Spawn(m_voicePlayerPrefab); // Shared Mode — 스폰한 로컬 플레이어가 State Authority
            else
                Debug.LogError("[VoiceController] m_voicePlayerPrefab 미할당 — 음성 아바타 없이는 송수신/음소거가 동작하지 않음");

            if (m_playerMute != null) m_playerMute.Bind(runner, m_voiceClient);
        }

        private void TeardownVoice()
        {
            if (m_playerMute != null) m_playerMute.Unbind();
            if (m_voiceClient != null) Destroy(m_voiceClient);
            if (m_recorder != null) Destroy(m_recorder);
            m_voiceClient = null;
            m_recorder    = null;
            // 스폰한 음성 아바타는 러너 종료와 함께 자동 despawn — 별도 처리 불필요
        }
    }
}
