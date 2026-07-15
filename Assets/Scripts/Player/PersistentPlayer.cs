using Oculus.Avatar2;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CampLantern.Player
{
    /// <summary>
    /// 공간(씬)을 넘나들어도 유지되는 영속 VR 플레이어. 로컬 XR 리그(OVRCameraRig+OVRManager+컨트롤러)와
    /// 아바타 입력 스택(SampleInputManager+OvrAvatarManager)을 담는 프리팹의 루트에 붙는다.
    ///
    /// 왜 영속인가 (room-architecture.md 공간 전환):
    ///   - 방을 바꿀 때마다 XR 리그를 재생성하면 VR에서 검은 플래시/헐컹이 난다 → 하나만 만들어 DontDestroyOnLoad.
    ///   - OVRManager는 단일 인스턴스여야 하므로(중복 시 오류) 영속 플레이어에 실어 씬마다 새로 만들지 않는다.
    ///
    /// 부트스트랩: <see cref="RuntimeInitializeOnLoadMethodAttribute"/>(런타임 어트리뷰트 — RULE-01의
    /// Domain Reload 트리거([InitializeOnLoad])와 무관)로 Resources에서 프리팹을 1회 인스턴스화한다.
    /// 어느 씬에서 플레이를 시작하든 자동 생성되므로 별도 부트스트랩 씬이 필요 없다.
    ///
    /// 씬 전환: SceneManager.sceneLoaded를 구독해 새 씬의 <see cref="PlayerSpawnPoint"/>로 리그를 이동한다.
    /// 스폰포인트가 없는 씬은 직전 위치를 유지한다.
    ///
    /// 네트워킹(SessionLauncher/VoiceController/AvatarController)은 이 프리팹에 넣지 않는다 — 공간마다
    /// 별도 Photon Room을 쓰므로 공간 씬별로 배치한다. 네트워크 아바타는 세션마다 스폰되어 이 영속 리그를
    /// 따라간다(AvatarBehaviourFusion이 OVRManager.instance로 리그를 찾음). 파티·음성 채널 전환 유지
    /// (room-architecture.md)는 백엔드/P2 영역이라 이 구현 범위 밖 — 후속 작업.
    /// </summary>
    public class PersistentPlayer : MonoBehaviour
    {
        /// <summary>현재 영속 플레이어. 없으면 null.</summary>
        public static PersistentPlayer Instance { get; private set; }

        // Assets/Resources/PersistentPlayer.prefab — Resources.Load 경로(확장자 없음)
        private const string k_resourcePath = "PersistentPlayer";

        /// <summary>
        /// 씬 로드 후 영속 플레이어가 아직 없으면 Resources에서 1회 생성한다.
        /// AfterSceneLoad라 최초 씬이 이미 로드된 상태 → 아래 OnEnable의 초기 이동에서 스폰포인트를 찾을 수 있다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;

            var prefab = Resources.Load<GameObject>(k_resourcePath);
            if (prefab == null)
            {
                Debug.LogError($"[PersistentPlayer] Resources/{k_resourcePath}.prefab 없음 — " +
                               "PersistentPlayerFactory(Tools > Make Assets > Persistent Player) 실행 필요");
                return;
            }
            var player = Instantiate(prefab);

            // 아바타 SDK 로그 억제 — OvrAvatarManager 기본 로그 레벨이 Verbose라 Info 홍수 +
            // ClipUpgradeHelper 경고 스팸(SDK 40.x 내장 클립의 알려진 노이즈)이 콘솔을 뒤덮는다.
            // Failure(→ELogLevel.Error)로 낮춰 에러만 통과시킨다. 아바타 문제 디버깅 시 이 값을 임시로 낮출 것.
            var avatarManager = player.GetComponentInChildren<OvrAvatarManager>();
            if (avatarManager != null)
                avatarManager.SetLogLevel(CAPI.ovrAvatar2LogLevel.Failure);
        }

        private void Awake()
        {
            // 씬 로드로 중복 생성될 수 있는 상황 방지 (프리팹이 실수로 씬에도 배치된 경우 등)
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            // 중복 구독 방지 (rules/scripts.md)
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;

            // 최초 배치 — 생성 시점의 활성 씬 스폰포인트로 이동 (이후 씬은 sceneLoaded가 처리)
            MoveToSpawnPoint();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Single 로드(공간 전환)에서만 재배치. Additive는 환경 로드일 수 있어 건드리지 않는다.
            if (mode == LoadSceneMode.Single) MoveToSpawnPoint();
        }

        /// <summary>로드된 씬의 스폰포인트로 리그 루트를 이동. 없으면 현재 위치 유지.</summary>
        private void MoveToSpawnPoint()
        {
            // 스폰포인트는 방금 로드된 씬에 있고 영속 플레이어(DontDestroyOnLoad 씬)와 분리돼 있으므로
            // FindFirstObjectByType로 안전하게 조회된다.
            var spawn = FindFirstObjectByType<PlayerSpawnPoint>();
            if (spawn != null)
                transform.SetPositionAndRotation(spawn.transform.position, spawn.transform.rotation);
        }
    }
}
