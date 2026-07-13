# Room 아키텍처 (공간 구조 설계)

## 한 줄 요약
Photon Room 개념으로 로비/낚시터/사냥터/영지 4개 공간을 나누고, "저장 데이터"와 "실시간 Room"을 분리한다 — 영지 배치는 서버 DB에 상시 저장되고 Room은 플레이어가 있을 때만 생성된다.

## 핵심 타입 / 진입점
- `Networking/SessionLauncher.cs` — `StartSession(sessionName, ct)`가 공간 무관 공통 진입점. 공간별 이름 규칙: 낚시터 `fishing_{shardId}`, 사냥터 `hunt_zone_{zoneId}`(=`StartHuntZone` 래퍼), 영지 `estate_{ownerId}`. 로비는 공유 Room이 아니므로 SessionLauncher를 쓰지 않는다.
- `Bootstrap/LobbyHarness.cs` / `FishingGroundHarness.cs` / `HuntZoneHarness.cs` / `EstateHarness.cs` — 공간별 씬 진입점(개발용 IMGUI). 각 씬: `Lobby.unity`, `FishingGround.unity`, `HuntZone_A.unity`, `EstateTemplate.unity` (Editor 팩토리: `RoomScenesFactory.cs`, `Tools > Make Assets > Room Scenes`).
- **P0 스켈레톤 한계 (2026-07-07)**: 매칭/샤딩(낚시터 "정원 도달 시 새 인스턴스"), 사냥터 존 자동 배정(위치 기반 라우팅), 영지 소유자 인증(현재 `SystemInfo.deviceUniqueIdentifier` 임시값)은 전부 백엔드 미정이라 미구현 — 고정 이름 Room 접속만 된다.
- **로컬 JSON 저장 도입 (2026-07-08)**: `Core/Persistence/PlayerState.cs`+`SaveService.cs`로 코인/인벤토리/영지 배치가 로컬 JSON(`Application.persistentDataPath/player_save.json`)에 저장돼 로비↔낚시터↔사냥터↔영지 이동에도 이어진다. `Core/ContentRegistry.cs`(Resources 소재, `ContentRegistryFactory`로 생성)가 저장된 Id 문자열을 실제 Def 에셋으로 복원. **이건 "내 기기 안에서 상태 유지"만 해결** — 다른 유저가 오프라인 주인 영지를 읽는 진짜 오프라인 방문은 여전히 서버 필요 (tech-stack-decisions.md 참조).
- **즉시 저장 정책 (2026-07-13)**: 인벤토리/지갑/배치가 변하는 모든 시점(포획·사냥 보상·구매·배치·회수·판매·조리·미끼/수리)에 하네스가 `PlayerState.Save()`를 즉시 호출한다. 근거: Quest(Android)는 OS 강제종료 시 `OnApplicationQuit`이 보장되지 않아 "씬 이탈 시에만 저장"은 유실 경로였다(QA 발견 — 사냥 Rare 재료 유실 시나리오). **새 획득/소비 경로를 추가하면 같은 시점에 Save를 붙일 것.** 파일이 수 KB라 성능 부담 없음.

## 공간별 정의

| 공간 | 타입 | 핵심 제약 |
|---|---|---|
| 로비 | 싱글·개인 공간 | 공유 Room 아님, 네트워크 코드 불필요. 순수 메뉴/포탈 허브 |
| 낚시터 | 싱글/멀티 선택, 상시 열림 | 파티 구성 없이 드롭인. 랜덤 매칭, 정원 도달 시 새 인스턴스로 샤딩 |
| 사냥터 | 존 분할 구조 | 세계관은 하나의 필드처럼 보이되 실제 플레이 단위는 작은 "존"으로 쪼갬. 존마다 독립 Room |
| 영지 | 개인 소유, `roomName = estate_{userID}` | 주인 오프라인이어도 방문 가능해야 함 |

## 시스템 간 관계
- **영지 ↔ 서버 DB**: 배치 데이터는 Room 생존 여부와 무관하게 영구 저장. Room은 조회/편집을 위한 실시간 세션일 뿐 진실의 원천(source of truth)이 아니다. → DB가 원본, Room은 캐시에 가까운 관계.
- **사냥터 존 ↔ 필드보스 그룹핑**: 같은 존에 있는 유저들이 대형 사냥감 등장 시 자동 그룹핑됨 — 존 크기 설계가 [[social-cooperation]]의 "능동적 유도(2인 이상 협동 포획)"가 실제로 발동하는지를 결정한다. 존이 너무 크면 밀도가 낮아져 매칭이 안 일어남.
- **영지 권한 ↔ Fusion 서버 권한 모델**: 마스터 클라이언트를 주인으로 고정할 수 없으므로(주인이 오프라인일 수 있음) "먼저 입장한 유저 또는 서버 관리형" 방식이 필요 — 이건 PUN 2로는 어렵고 Fusion 2를 선택한 직접적 이유([[tech-stack-decisions]] 참조).

## 기획 의도 / 역사적 맥락
- 낚시터와 사냥터를 물리적으로 통합하지 않은 이유: 두 공간의 룸 라이프사이클이 다르기 때문 (낚시터는 상시 열림/드롭인, 사냥터는 존 단위 세션). 맵은 분리하되 운영 방식(싱글/멀티 선택 가능)은 동일하게 맞춤.
- "세계관은 넓게, 실제 인스턴스는 작게"가 사냥터 존 분할의 핵심 원칙 — 존이 작을수록 랜덤 매칭 시 실제로 마주칠 확률이 높아진다는 판단.

## 숨은 규칙 / 암묵지
- 영지는 주인 접속 여부와 무관하게 항상 열려 있어야 한다 — "방문객 있는데 Room이 없어서 못 들어감" 상황이 나오면 설계 위반. 오프라인 방문 시 최신 저장 스냅샷으로 인스턴스를 새로 생성해야 한다.
- 파티 ID와 음성 채널은 로비→낚시터→사냥터→영지 전환 시 유지되어야 한다. 공간 전환마다 파티가 깨지면 안 됨.

## 파티·음성 전환 유지 (설계 — 백엔드 확정 후 착수, P2)

위 "파티 ID·음성 채널 유지" 요구의 구현 설계. **현재 착수 보류** — 파티/매칭 백엔드가 미정([[tech-stack-decisions]] "영구 저장 백엔드")이라, 그 위에 파티 코드를 지금 짜면 투기적 코드가 된다.

### 현재 상태와 오해 방지 (2026-07-08)
- **로컬 플레이어(VR 리그)는 이미 공간 전환에도 유지된다** — `Player/PersistentPlayer`(DontDestroyOnLoad + RuntimeInitializeOnLoad)가 씬을 넘나들며 살아남고, 각 씬 `PlayerSpawnPoint`로 이동한다. 로컬 저장(코인·인벤·배치)도 로컬 JSON으로 이어진다.
- **음성/세션은 공간 씬별(Network GO: SessionLauncher+VoiceController+AvatarController)** — 공간 전환(`SceneManager.LoadScene`) 시 파괴·재생성된다. 각 공간이 별도 Photon Room이므로 음성이 방마다 재접속되는 건 구조상 필연이다.
- **이 "공간별 음성"은 P0에서 버그가 아니라 올바른 동작이다** — 드롭인 소셜은 *현재 공간에 있는 사람들과 대화*하는 게 맞다. "특정 파티가 공간을 넘나들며 함께"는 P2 파티 기능이다.

### 백엔드 확정 후 필요한 것
1. **영속 파티 매니저** — `PersistentPlayer`처럼 DontDestroyOnLoad로 파티 ID·멤버 목록을 들고 씬 전환에도 유지.
2. **파티 ID → 룸 라우팅** — 파티가 함께 이동할 때 같은 방에 모이도록: 낚시터 샤딩(`fishing_{shardId}`)·사냥터 존 배정이 파티를 같은 shard/zone으로 묶어야 한다(현재는 고정 이름만, 8-4 스켈레톤 한계). 이게 매칭 백엔드의 역할.
3. **음성 채널 승계** — 공간 전환 시 새 방 voice에 접속하되, 파티가 같은 방에 모이므로 자연히 같이 들린다. 파티 전용 "무전" 채널(공간 무관, GDD 6-4)은 별도 — Voice 채널 개념 도입 필요(현재 채널 미사용, [[tech-stack-decisions]] step-09).
4. **네트워킹 배치 재고** — 현재 공간 씬별인 SessionLauncher/VoiceController를, 파티 연속성 구현 시 영속 파티 매니저가 공간별 세션 진입을 조율하는 구조로 옮길지 검토(러너는 방마다 새로 뜨므로 완전 영속은 불가, 파티 상태만 영속).

### 착수 트리거
`tech-stack-decisions.md`의 "영구 저장 백엔드" 행이 확정되고 매칭/파티 ID 발급 방식이 정해지면 착수. 그 전까지 P0은 공간별 음성으로 충분하다.

## 수정 시 주의
- 영지 배치 데이터 저장 방식(서버 DB)이 [[tech-stack-decisions]]에서 아직 미정 — 이 부분 구현은 백엔드 결정 전까지 보류.
- 사냥터 존 개수/크기를 바꾸면 협동 포획 발동 확률([[social-cooperation]])이 같이 바뀐다는 점을 감안해서 밸런스 재검토 필요.
