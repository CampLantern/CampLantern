# CLAUDE.md

이 파일은 Claude Code(claude.ai/code)가 이 레포지토리에서 작업할 때 참고하는 가이드이다.

## 프로젝트 개요

**Camp Lantern** — Unity 6000.3.10f1(Unity 6) 기반 **VR 코지 소셜 영지 게임** (Meta Quest 타겟). 유저가 캠핑 영지를 소유하고 낚시·요리·사냥으로 얻은 자원으로 영지를 꾸미며, 협동·방문·공유 진행도(Shared Ledger)로 소셜 플레이를 유도한다. 수익화는 프리미엄(1회 구매) + DLC.

GDD 원본: `.claude/domain/gdd/VR_코지_소셜_영지_게임_기획서.docx` (.docx라 직접 Read 불가). **시스템별 정리는 `.claude/domain/*.md`에 있으며 `.claude/INDEX.md` Level 2로 라우팅한다.**

**현재 진행: P0 코어 프로토타입 9단계 전부 코드 착수 완료.** 설계는 `.claude/design/p0-core-prototype/`(README + step-01~09), 구현은 `Assets/Scripts/` 하위(Core/Fishing/Cooking/Estate/Hunting/Networking/Bootstrap/Editor). 남은 것은 에디터 플레이 검증(15~30분 루프 순환, 2인 협동 사냥 동기화, 음소거 토글) — mvp-scope.md P0 완료 판정 기준.

## 불변 제약 (Inviolable Constraints)

프로젝트 루트의 [`RULES.md`](./RULES.md)에 시스템을 실제로 망가뜨리는 제약이 정리되어 있다 (에디터 멈춤, GUID 손상, 빌드 실패 등). 작업 시작 전 반드시 스캔할 것.

- **RULE-01**: Domain Reload 트리거 금지 (`[InitializeOnLoad]`, `autoReferenced: true`)
- **RULE-02**: Unity 에셋 파일 직접 편집 금지 (`.meta`/`.prefab`/`.unity`/`.asset`)
- **RULE-03**: 물리 API는 `FixedUpdate`에서만
- **RULE-04**: `ProjectSettings/`는 Claude가 직접 수정하지 않음

프로젝트 고유 시스템(저장 데이터, 플랫폼 분기 등)이 생기면 해당 시점에 새 RULE을 추가한다.

## 추가 규칙 및 컨벤션

프로젝트 지식은 `.claude/` 하위에 계층별로 정리되어 있다. 필요에 따라 참조할 것:

### `.claude/rules/` — 경로 기반 코딩 규칙 (자동 로드)
- `scripts.md` — 컴포넌트 캐싱, 비동기 CancellationToken, 이벤트 구독 해제, Awake 초기화 등 범용 스크립팅 컨벤션. 프로젝트 고유 컨벤션(싱글톤 체계, UI 프레임워크 등)이 정해지면 이 파일에 추가한다.

### `.claude/domain/` — 이 프로젝트 고유 설계 의도
시스템을 수정하기 **전에** 관련 도메인 문서를 읽어 "왜 이렇게 설계됐는가"를 파악할 것. GDD 원본(.docx)을 시스템별로 distill한 8개 문서가 존재: `tech-stack-decisions`, `room-architecture`, `estate-system`, `resource-loop`, `social-cooperation`, `mvp-scope`, `economy`.

**GDD 라우팅은 `.claude/INDEX.md` Level 2를 통해 한다.** 특정 파일명을 직접 열지 말고, INDEX.md의 keywords와 작업 주제를 매칭해 해당 문서만 선별 로드한다.
- `gdd/` — GDD 원본 `.docx` 보관 (직접 Read 불가, distilled 문서를 통해 참조).
- 새 도메인 문서 추가 시 INDEX.md Level 2에 keywords와 함께 등록 (`domain/README.md` 참조).

### `.claude/knowledge/` — 범용 Unity/C# 레퍼런스
언어·엔진 베스트 프랙티스가 헷갈릴 때 참조:
- `RULES.md` — 21개 범용 코딩 원칙 (R1-R21)
- `csharp-dotnet.md` — 값 타입, 박싱, 이벤트, async, LINQ
- `unity-scripting-gotchas.md` — 직렬화 함정, 코루틴 주의점, IL2CPP, Unity 2022→6000 API 리네임
- `unity-mobile-performance.md` — 모바일/XR 성능 규칙 (프로파일링, GC, 배칭, UI 등)
- `debugging/` — 디버깅 원칙 10개 (가정 의심, 버그 분류, 증상 vs 원인, 버그 일지, 정적 분석, 단언, 이분 탐색 등)
- `qa/` — QA 원칙 10개

### `.claude/INDEX.md`
위 모든 계층에 대한 키워드 기반 라우팅. **GDD를 포함한 모든 도메인 문서는 이 파일을 통해 라우팅된다.** 어떤 파일을 열어야 할지 모를 때 참고.

## 빌드 커맨드

아직 커스텀 빌드 파이프라인이 없다. Unity 에디터의 기본 `File > Build Settings`를 사용한다. 빌드 자동화 스크립트가 추가되면 여기에 기록한다.

### 에디터 도구 (에셋/씬 생성 — RULE-02 준수)

`.asset`·`.prefab`·`.unity`를 직접 편집하지 않고 Editor 팩토리 스크립트(`Assets/Scripts/Editor/`, `CampLantern.EditorTools`)로 Unity가 생성하게 한다. Unity 메뉴에서 실행:

- **Tools > Make Assets > Content Registry** — `Assets/Data`를 스캔해 `Assets/Resources/ContentRegistry.asset` 갱신 (`ContentRegistryFactory`). **콘텐츠 데이터(ItemDef/EstateObjectDef 등) 추가 후 반드시 재실행.**
- **P0 데이터/씬 생성** — `P0DataFactory`(어종·레시피·영지 오브젝트 SO), `P0PlaySceneFactory`(단일 씬 플레이 하네스 배선), `RoomScenesFactory`(로비/낚시터/사냥터/영지 Room 씬), `LobbyMapFactory`(**Build Lobby Map (VR)** — 로비 캠프장 환경·석양 라이팅·스폰·포탈 UI, idempotent, `CreateLobby`가 공통 호출), `VoicePlayerFactory`. 실행 진입점은 `P0PlayTestMenu` / `RoomScenesPlayTestMenu`.
- **프리팹 생성** — `VRPlayerRigFactory`(VR 리그), `StationFactory`(낚시/요리 스테이션), `VRUIFactory`(VR UI 세트: 패널·버튼·EventSystem, `Import TMP Essentials`·`Add Interaction To VR Rig` 메뉴 포함). 전부 `Tools > Make Assets` 아래.
- 새 콘텐츠·씬이 필요하면 이 팩토리를 확장하거나 `/make-assets` 스킬로 새 Editor 스크립트를 작성한다.

## 아키텍처

### 공간/네트워크 구조 (GDD 8장 확정)

- **4개 공간**: 로비(싱글) / 낚시터(드롭인 멀티) / 사냥터(존 분할) / 영지(`estate_{userID}`, 오프라인 방문 가능) — 상세는 `.claude/domain/room-architecture.md`
- **넷코드**: Photon Fusion 2 (서버 권한 모델 — 영지 주인 오프라인 요구사항 때문에 PUN 2 배제). 음성은 Photon Voice 2. 선택 근거는 `.claude/domain/tech-stack-decisions.md`
- **영구 저장 백엔드**: 미정 (자체 서버 방향, 추후 결정) — 오프라인 영지 방문·Shared Ledger 재접속 복원은 백엔드 확정 후 착수 (P0 범위 제외)

### 코드 아키텍처 (P0 구현)

네임스페이스는 `CampLantern.{영역}`, 폴더는 `Assets/Scripts/{영역}`으로 1:1 대응. **P0은 단일 `Assembly-CSharp` 유지** (asmdef 신설 안 함 — RULE-01 회피).

- **데이터: Def(SO) + 런타임 상태 분리** — `CampLantern.Core`(`Core/Data`)에 `FishDef`/`ItemDef`/`RecipeDef`/`EstateObjectDef`/`HuntTargetDef`/`Rarity` ScriptableObject 정의. 런타임 상태(`Wallet`/`Inventory`/`EstateShop`)는 순수 C# 클래스로 별도.
- **Id 기반 참조 복원**: `ContentRegistry`(`Assets/Resources/ContentRegistry.asset`, `Resources.Load` 접근)가 Id 문자열→Def 조회. 저장 파일엔 Id만 기록되므로 로드 시 이걸로 SO 참조 복원.
- **저장(`Core/Persistence`)**: `PlayerState`가 `Wallet`/`Inventory`/`Shop`을 소유하고 `SaveService`(로컬 JSON, `persistentDataPath`)와 `Load`/`Save`로 동기화. 씬(공간)마다 새로 만들어져도 디스크에서 복원하므로 공간 이동 간 코인·아이템·배치가 이어진다. `Save(EstateManager)`에 매니저가 없으면 배치 목록은 건드리지 않음(그 씬은 배치를 모름).
- **로컬 우선, 네트워크는 사냥만**: 낚시(`Fishing`: `FishingRod`/`FishingSpot`)·요리(`Cooking`: `CookingPot`/`RecipeMatcher`)·영지(`Estate`: `EstateManager`/`EstateShop`/`PlacedObject`)는 싱글 로직. Fusion `NetworkBehaviour`는 협동 사냥(`Hunting`: `HuntTarget`/`HuntLedger` — Shared Ledger 최소판)에만.
- **세션 진입**: `Networking.SessionLauncher`가 Fusion 2 **Shared Mode**로 고정 이름 Room 합류(`hunt_zone_{zoneId}` 등). `NetworkRunner`는 씬 배치 없이 런타임 AddComponent. 매칭/샤딩·영지 소유권 인증은 백엔드 미정이라 P0은 고정 이름만.
- **음성**: `Networking.Voice`(`VoiceController`/`PlayerMute`) — Photon Voice 2 설치 후 동작(step-09 선행 조건).
- **VR 리그·아바타 (2겹 구조)**: ① **로컬 영속 아바타** — `Player.PersistentPlayer`(`Assets/Resources/PersistentPlayer.prefab`)가 OVRCameraRig+OVRManager+`OvrAvatarManager`(AvatarSdkManager)+`SampleInputManager`(AvatarInputManager)를 통째로 싣고 `RuntimeInitializeOnLoadMethod`로 어느 씬에서든 1회 자동 스폰 후 `DontDestroyOnLoad`. 씬 전환 시 각 씬의 `PlayerSpawnPoint`로 이동. **씬에는 리그/아바타 매니저를 다시 넣지 않는다** — 넣으면 OVRManager/OvrAvatarManager 이중 인스턴스로 아바타가 깨진다. ② **네트워크 아바타** — `Networking.Avatar.AvatarController`가 `VoiceController`와 동일 패턴으로 `SessionStarted`에서 플레이어당 몸체(Meta `FusionAvatarSdk28Plus`)를 스폰해 영속 리그를 따라가게 한다. 세션(Photon Room)이 공간마다 다르므로 AvatarController와 음성(VoiceController+PlayerMute)은 **Network 오브젝트(SessionLauncher와 같은 GO)에 씬별 배치**한다(=네트워크 소셜 스택). 배선 헬퍼는 `EditorTools.AvatarSetupFactory.EnsureOnNetworkObject`(아바타)와 `RoomScenesFactory.EnsureVoiceOnNetworkObject`(음성)이며, `P0PlaySceneFactory`/`RoomScenesFactory`가 씬 생성 시 공통 호출 → 재생성해도 유실 없음. 기존 씬 일괄 패치는 **Tools > Make Assets > Wire Networked Social Stack Into Room Scenes**(사냥터·낚시터에 아바타+음성 idempotent 배선). 낚시 로직 자체는 로컬(공간만 멀티, `domain/social-cooperation.md`). 통합 근거·샘플 임포트 함정은 `domain/tech-stack-decisions.md`.
- **부트스트랩/하네스(`Bootstrap`)**: `P0Harness`(단일 씬 전체 배선 + IMGUI 디버그 UI), 공간별 `LobbyHarness`/`FishingGroundHarness`/`HuntZoneHarness`/`EstateHarness`. **IMGUI(OnGUI)는 개발용 임시** — 실사용 UI는 아래 VR UI 토대로, 개발 조작은 VR 입력 어댑터로 대체 예정(Quest 빌드 전 IMGUI 제거 대상).
- **VR UI(`UI`)**: 월드스페이스 UGUI 기반 토대 — `VRUIPanel`(월드스페이스 Canvas + Meta Interaction `PointableCanvas` 레이/포크 배선)·`VRUIButton`(Button+TMP 라벨). 프리팹은 `Assets/Prefabs/UI/`(`VRUIPanel`/`VRUIButton`/`VRUIEventSystem`), 생성기는 `VRUIFactory`(Tools > Make Assets > VR UI). 클릭 판정 체인: 리그의 Ray/PokeInteractor → Interactable → PointableCanvas → `VRUIEventSystem`의 PointableCanvasModule → GraphicRaycaster → Button. 리그 인터랙터는 `VRPlayerRig`에 `OVRComprehensiveInteractionRig` 중첩(Tools > Make Assets > Add Interaction To VR Rig — 레이/포크뿐 아니라 그랩·로코모션 포함 올인원). **컨트롤러 비주얼은 인터랙션 리그의 ControllerVisual 하나만 쓴다** — Core SDK `OVRControllerPrefab` 모델과 겹치면 이동/회전 시 갱신 타이밍 차이로 고스트 컨트롤러가 보인다(리그 프리팹에서 비활성화됨, 재발 시 Tools > Make Assets > Fix Duplicate Controller Visuals). 텍스트는 TMP(Essentials 임포트됨: `Assets/TextMesh Pro`). 실사용 첫 사례는 로비 포탈(`UI/PortalPanelController` — 버튼 클릭→공간 씬 이동, `LobbyMapFactory`가 배선). 나머지 게임 UI(상점·인벤토리 등)는 이 토대를 복제·확장해 짓는다.
- **싱글톤 미사용**: `PlayerState`·`ContentRegistry`는 하네스/매니저가 생성·주입. 전역 static 매니저 패턴은 아직 도입 안 함.

## 코드 컨벤션

P0 코드에서 확립된 실제 컨벤션 (`.claude/rules/scripts.md`의 범용 규칙과 함께 적용):

- **한글 주석** 허용 (실제로 전 코드가 한글 주석·요약 사용).
- **네임스페이스**: `CampLantern.{영역}` — 폴더명과 일치. 영역: `Core`(+`Core.Data`/`Core.Persistence`), `Cooking`, `Fishing`, `Estate`, `Hunting`, `Networking`(+`Networking.Voice`/`Networking.Avatar`), `UI`, `Player`, `Bootstrap`, `EditorTools`.
- **필드 접두사**: 직렬화/인스턴스 필드는 `m_`, `private const`는 `k_`. `[SerializeField] private` + 프로퍼티 노출 패턴.
- **이벤트**: C# `event Action<T>`로 시스템 간 배선(예: `FishCaught`→Inventory, `RewardGranted`→보상). 구독자는 `OnDestroy`/`OnDisable`에서 해제 (rules/scripts.md).
- **비동기**: Fusion 세션 등 async 메서드는 `CancellationToken`을 받는다 (`SessionLauncher.StartSession`). 별도 async 라이브러리(UniTask 등)는 아직 미도입.
- **싱글톤 미사용** — 상태 객체는 하네스/매니저가 생성·주입한다.
- 새 컨벤션이 정해지면 이 섹션과 `.claude/rules/scripts.md`에 기록한다.

## 주요 패키지

- **Meta XR SDK 슈트 — 전부 203.0.0으로 버전 통일** (`core`, `haptics`, `interaction.ovr`, `platform`). 새 Meta XR 패키지 추가 시 반드시 203.0.0에 맞출 것 — 슈트 버전이 섞이면 내부 API 호환이 깨진다.
- **Meta Avatars SDK 40.0.1** — EOF(End-of-Feature) 최종 버전. 버전 번호가 203.x 트레인과 다른 게 정상이며 업데이트하면 안 됨 (더 높은 버전이 존재하지 않음).
- **Photon Fusion 2 / Voice 2** — `.unitypackage`로 `Assets/Photon/`에 임포트됨 (UPM 아님). App ID는 `Fusion > Real Time Settings`의 `PhotonAppSettings` 에셋에 저장 (App Id Fusion / App Id Voice 두 필드).
- 나머지는 `Packages/manifest.json` 참고. Oculus 샘플 에셋은 `Assets/Oculus/`.

## 플랫폼 노트

- **타겟: Meta Quest** (GDD 확정 — 성능 기준, Quest 스토어 매출 벤치마크, 캠프 수용량 등 Quest 전제로 설계됨). PCVR 확장은 GDD 15-2에서 장기 검토 사항으로만 언급.
- 성능 목표: 90Hz 유지 (`.claude/rules/scripts.md`, `knowledge/unity-mobile-performance.md` 참고).
