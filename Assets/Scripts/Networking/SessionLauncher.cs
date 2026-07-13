using System;
using System.Threading;
using System.Threading.Tasks;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CampLantern.Networking
{
    /// <summary>
    /// Fusion 2 세션 진입/종료 관리. 공간별 roomName 규칙은 room-architecture.md 참조:
    /// 낚시터 "fishing_{shardId}", 사냥터 "hunt_zone_{zoneId}", 영지 "estate_{ownerId}".
    /// 로비는 공유 Room이 아니므로 이 컴포넌트를 사용하지 않는다(순수 씬 전환, room-architecture.md).
    /// 실제 매칭/샤딩·영지 소유권 인증은 백엔드 미정이라 P0에서는 고정 이름 Room 합류만 지원한다
    /// (tech-stack-decisions.md "영구 저장 백엔드" 항목).
    /// NetworkRunner는 씬 배치 대신 이 컴포넌트가 같은 GameObject에 AddComponent로 부착한다 (RULE-02).
    /// </summary>
    public class SessionLauncher : MonoBehaviour
    {
        /// <summary>현재 실행 중인 러너. 세션 없으면 null.</summary>
        public NetworkRunner Runner { get; private set; }

        // StartGame 진행 중 재진입 가드 — Runner는 StartGame 완료 후에야 대입되므로
        // Runner null 체크만으로는 진행 중 재호출을 막지 못한다 (러너 이중 부착 방지).
        private bool m_starting;

        /// <summary>세션 시작 성공 시 발화. 구독자는 OnDestroy/OnDisable에서 반드시 해제할 것 (rules/scripts.md).</summary>
        public event Action<NetworkRunner> SessionStarted;

        /// <summary>
        /// Shared Mode로 지정된 이름의 Room에 접속한다. 같은 sessionName으로 접속한 클라이언트는 같은 Room에 합류한다.
        /// 호출자는 공간별 이름 규칙(위 클래스 주석)에 맞는 문자열을 조립해서 넘긴다.
        /// </summary>
        public async Task StartSession(string sessionName, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(sessionName))
                throw new ArgumentException("sessionName이 비어 있음", nameof(sessionName));
            if (Runner != null || m_starting)
            {
                Debug.LogWarning("[SessionLauncher] 이미 세션이 실행/시작 중 — 중복 시작 무시");
                return;
            }

            m_starting = true;
            try
            {
                var runner = gameObject.AddComponent<NetworkRunner>();
                runner.ProvideInput = true; // step-08 협동 사냥 입력용 — Shared Mode에서는 로컬 플레이어 입력 제공 필수

                // StartGame이 SceneManager 없이도 기본값을 만들어주지만, FusionBootstrap과 동일하게 명시 부착
                var sceneManager = runner.GetComponent<INetworkSceneManager>()
                                   ?? (INetworkSceneManager)runner.gameObject.AddComponent<NetworkSceneManagerDefault>();

                var args = new StartGameArgs
                {
                    GameMode                 = GameMode.Shared, // 데디케이티드/호스트 모드는 P0 불필요 (step-07 제약)
                    SessionName              = sessionName,
                    SceneManager             = sceneManager,
                    StartGameCancellationToken = ct,
                };

                // 멀티 피어 모드(에디터 더미 테스트)일 때만 시작 씬 지정 — 싱글 피어 경로는 기존 그대로
                NetworkSceneInfo? arenaScene = TryGetMultiPeerArenaScene();
                if (arenaScene.HasValue) args.Scene = arenaScene.Value;

                var result = await runner.StartGame(args);

                if (!result.Ok)
                {
                    Destroy(runner);
                    throw new InvalidOperationException(
                        $"[SessionLauncher] 세션 시작 실패: {result.ShutdownReason} — {result.ErrorMessage}");
                }

                ct.ThrowIfCancellationRequested();

                Runner = runner;
            }
            finally
            {
                m_starting = false;
            }

            SessionStarted?.Invoke(Runner);
        }

        /// <summary>
        /// 사냥터 존 Room 접속 — roomName은 hunt_zone_{zoneId}. step-07부터 쓰인 기존 호출부와 호환하는 얇은 래퍼.
        /// </summary>
        public Task StartHuntZone(string zoneId, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(zoneId))
                throw new ArgumentException("zoneId가 비어 있음", nameof(zoneId));
            return StartSession($"hunt_zone_{zoneId}", ct);
        }

        /// <summary>세션을 종료하고 러너를 제거한다. 세션이 없으면 아무것도 하지 않는다.</summary>
        public async Task Shutdown(CancellationToken ct = default)
        {
            if (Runner == null) return;
            ct.ThrowIfCancellationRequested();

            var runner = Runner;
            Runner = null;

            // destroyGameObject: false 필수 — true(기본값)면 SessionLauncher가 붙은 GameObject째 파괴된다
            await runner.Shutdown(destroyGameObject: false);
            if (runner != null) Destroy(runner);
        }

        private void OnDestroy()
        {
            // 파괴 시 연결 누수 방지 — await 불가 지점이므로 동기 킥오프만
            if (Runner != null && Runner.IsRunning)
                Runner.Shutdown(destroyGameObject: false);
        }

        /// <summary>
        /// 멀티 피어 모드(에디터 더미 테스트)에서는 StartGameArgs.Scene 지정이 필수 —
        /// 없으면 씬 준비 상태가 완료되지 않아 스폰 큐가 영영 처리되지 않는다 (경고가 아니라 실질 고장).
        /// 빈 아레나 씬(P0NetArena)을 피어별로 로드시킨다. 싱글 피어에서는 null 반환 (기존 동작 유지).
        /// 더미 피어(P0Harness)도 같은 헬퍼를 사용한다.
        /// </summary>
        internal static NetworkSceneInfo? TryGetMultiPeerArenaScene()
        {
            if (NetworkProjectConfig.Global.PeerMode != NetworkProjectConfig.PeerModes.Multiple)
                return null;

            int buildIndex = SceneUtility.GetBuildIndexByScenePath("Assets/Scenes/P0NetArena.unity");
            if (buildIndex < 0)
            {
                Debug.LogWarning("[SessionLauncher] 멀티 피어 모드인데 P0NetArena 씬이 Build Settings에 없음 — " +
                                 "Tools > Make Assets > P0 Net Arena Scene 실행 필요. 스폰이 처리되지 않을 수 있음");
                return null;
            }

            var sceneInfo = new NetworkSceneInfo();
            sceneInfo.AddSceneRef(SceneRef.FromIndex(buildIndex), LoadSceneMode.Additive);
            return sceneInfo;
        }
    }
}
