# 기술 스택 결정 (GDD 미기재 — 세션 결정 사항)

## 한 줄 요약
GDD 8장은 "Photon 기반 Room" 이라고만 명시하고 구체 SDK·버전·백엔드는 정하지 않았다. 이 문서는 실제로 확정한 값과 그 이유를 기록한다.

## 핵심 타입 / 진입점
- 아직 코드 없음. `Packages/manifest.json`에 패키지 의존성만 존재.

## 결정 사항

| 항목 | 결정 | 이유 |
|---|---|---|
| 넷코드 | Photon **Fusion 2** (PUN 2 아님) | GDD 8-4 "마스터 클라이언트를 주인으로 고정할 수 없음, 서버 관리형 방식 필요" — Fusion의 서버 권한(host authoritative) 모델이 이 요구와 직접 맞음. PUN 2는 마스터클라이언트 기반이라 8-5 "배치 데이터는 서버 권한" 요구를 만족하려면 추가 설계가 필요했음 |
| 음성 | Photon **Voice 2** (Voice for Fusion 통합판) | 6-4 근접 음성 + 파티 무전 요구. Fusion과 별도 App ID로 관리 |
| 아바타 | Meta Avatars SDK **v40.0.1 (EOF 고정)** | Meta가 Avatars SDK를 End-of-Feature 처리해 v40.0.1이 마지막 릴리스. 신규 기능·API 추가는 없지만 백엔드 서비스는 계속 운영되고 기존 통합은 정상 동작 — 대체 SDK가 없어 그대로 채택. **버전이 다른 Meta XR 패키지(203.x 트레인)와 번호 체계가 다르다는 점**이 낚시 포인트 (Asset Store 검색에서 안 보일 수 있음, Meta Downloads 페이지에서 직접 받아야 함) |
| Meta XR SDK 버전 | 기존 haptics/interaction.ovr/platform과 동일한 **203.0.0**으로 신규 패키지(Core) 버전 통일 | Meta XR SDK는 슈트 단위로 버전이 같이 올라가는 구조 — 버전 섞으면 슈트 내부 API 호환 깨짐 |
| 영지 배치 데이터 영구 저장 백엔드 | **미정 (자체 서버, 추후 결정) — P0는 로컬 JSON으로 임시 대체** | Photon Room은 실시간 세션일 뿐 영구 DB가 아님. 8장 "재화·오브젝트 배치 데이터는 Room 생존 여부와 무관하게 서버 DB에 영구 저장" 요구를 만족할 별도 백엔드가 필요하지만 아직 팀 결정 전. 2026-07-08: 사용자 요청으로 `SaveService`(JSON, `Application.persistentDataPath`)를 우선 도입 — 인터페이스(`SaveService.Load/Save`)를 좁게 유지해 백엔드 확정 시 내부 구현만(HTTP 호출 등으로) 교체하고 호출부(`PlayerState`)는 그대로 두도록 설계함. **로컬 저장은 오프라인 방문(다른 유저가 읽는 것)을 해결하지 못함** — 그건 여전히 서버 필요 |

## 시스템 간 관계
- Fusion 2의 서버 권한 모델은 [[room-architecture]] 8-4/8-5 (영지 권한, 협동 이벤트 진행도)의 전제 조건이다. Fusion 없이는 "먼저 입장한 유저 또는 서버 관리형" 구조를 구현할 방법이 마땅치 않음.
- Avatars SDK가 EOF라는 것은, 향후 아바타 커스터마이징 기능 확장 시 Meta가 새 API를 추가해주지 않는다는 뜻 — 커스터마이징 요구가 늘면 자체 아바타 시스템 전환을 재검토해야 할 수 있음.
- 영구 저장 백엔드 미정 상태이므로, [[room-architecture]]의 "영지 오프라인 방문" 기능은 백엔드가 정해지기 전까지 실제 구현 착수 불가 (스냅샷을 어디서 읽어올지가 정해져야 함).

## 기획 의도 / 역사적 맥락
- GDD가 "Photon 기반"까지만 정하고 SDK 세부 선택을 비워둔 건 기획 문서가 기술 스택보다 시스템 요구사항 중심으로 쓰였기 때문. 8-4의 "마스터 클라이언트 고정 불가" 요구사항 한 줄이 사실상 PUN 2를 배제하고 Fusion을 선택하게 만든 결정적 근거였음.

## Photon Voice 2 임포트 레이아웃 (2026-07-06 확정 — 표준 임포트와 다름)

Voice 2.63(Asset Store)은 Realtime **4** 기반인데 Fusion 2.1은 Realtime **5** 소스(`Assets/Photon/PhotonRealtime`, asmdef `Photon.Realtime`)를 쓴다. Voice를 그대로 임포트하면 같은 폴더에 RT4가 덮어써져 **asmdef 중복 + RT5 파일 손상**으로 컴파일이 깨진다. 공식 문서(voice-for-fusion)는 Fusion 2.1 이전 기준이라 이 충돌을 다루지 않음. 이 프로젝트의 확정 레이아웃:

- **RT4 전체를 `Assets/Photon/PhotonVoice/PhotonRealtime/Code/`로 이전** (asmdef `PhotonRealtime`). RT5는 원위치 유지. 두 어셈블리는 이름이 달라 공존 가능.
- 구 lib `Photon3Unity3D.dll`(네임스페이스 `ExitGames.Client.Photon`)과 신 lib `PhotonClient.dll`(`Photon.Client`)은 네임스페이스가 달라 공존 가능 — **둘 다 필요, 둘 다 삭제 금지**.
- PUN 2/PhotonChat/Voice의 PUN 통합/비-Fusion 데모는 공식 가이드대로 임포트 제외(삭제)됨.
- `FusionVoiceClient.cs`·`PrefabSpawner.cs`에 Fusion 2.1 호환 수정 있음 (OnReliableDataReceived 시그니처, FusionAppSettings 필드 리플렉션 복사, UseFusionAuthValues 비활성). 추가 수정 (2026-07-07): `VoiceNetworkObject.cs` — VoiceConnection 없는 피어(에디터 더미 러너)에서 Spawned/Despawned 양쪽 다 NRE 방지 가드(Spawned에서만 막으면 Despawned에서 재발하므로 반드시 쌍으로 적용), `VoiceFollowClient.cs` — 접속 진행 중 "Voice client is busy" 로그를 Warning→Info 하향. **Voice 패키지를 업데이트/재임포트하면 이 수정과 폴더 이전이 전부 되돌아가므로 이 섹션 절차를 다시 적용해야 한다.**

### Fusion 위빙 함정 — Meta XR Building Blocks (2026-07-07 발견·해결)
- Meta XR Core SDK는 Fusion 존재를 감지하면 `Meta.XR.MultiplayerBlocks.Fusion` 어셈블리(NetworkBehaviour 포함)를 자동 컴파일한다. 이 어셈블리가 `NetworkProjectConfig.fusion`의 `AssembliesToWeave`에 없으면 **러너 초기화(NetworkTypesMeta)가 통째로 실패해 모든 세션 접속이 Error**가 된다 (`FusionAnchor has no attribute NetworkStructWeavedAttribute`). 목록에 추가돼 있음 — 지우지 말 것.
- `AssembliesToWeave` 변경은 일반 재컴파일로는 반영 안 된다 (Bee가 ILPostProcessor 결과 캐시 재사용). **`CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.CleanBuildCache)`** 로 캐시까지 버려야 위버가 다시 돈다. Meta XR/Fusion 패키지 업데이트 후 같은 에러가 재발하면 이 절차부터.
- Photon 대시보드 App ID는 **타입이 있다** — Voice 접속엔 반드시 "Voice" 타입 앱의 ID여야 한다. 다른 타입(Fusion 등) ID를 App Id Voice에 넣으면 룸 참가에서 `Unsupported Plugin (32752)` 서버 에러. (2026-07-07 실제 발생 — Voice 타입 앱 새로 생성해 해결.)

### Voice 런타임 통합 구조 (2026-07-07, step-09)
- `UseFusionAuthValues`가 비활성(위 수술)이라 **Voice 룸 액터번호↔Fusion PlayerRef 매핑이 불가능**하다.
  플레이어 식별이 필요한 음성 기능(음소거, P2 파티 무전)은 반드시 플레이어당 스폰되는
  `VoiceNetworkObject`(VoicePlayer.prefab)의 `Object.StateAuthority`를 경유할 것.
- NetworkRunner가 런타임 AddComponent라(SessionLauncher) FusionVoiceClient도 세션 시작 **후**
  같은 GO에 부착한다 — 자동 콜백 수집에서 빠지므로 `runner.AddCallbacks(voiceClient)` 필수.

### 에디터 더미 2인 테스트 — Fusion 멀티 피어 3종 세트 (2026-07-07)
빌드 없이 에디터 한 프로세스에서 2번째 러너(더미 플레이어)를 같은 룸에 붙이려면 셋 다 필요하다:
1. `NetworkProjectConfig.fusion`: `"PeerMode": 1` (Multiple)
2. `"EnqueueIncompleteSynchronousSpawns": true` — 멀티 피어는 동기 프리팹 스폰을 거부함
3. **StartGameArgs.Scene 지정 필수** — 없으면 에러 로그 후 러너는 살지만 **씬 준비 상태가 완료되지 않아 스폰 큐가 무한 대기** (사냥감/음성 아바타가 조용히 안 나타나는 증상). 빈 `P0NetArena.unity`(Build Settings 등록)를 `SessionLauncher.TryGetMultiPeerArenaScene()`이 멀티 피어일 때만 지정 — 싱글 피어(실빌드) 경로 불변.
- 더미 추가는 P0Harness "더미 플레이어 추가" 버튼 / 자동 검증은 `Tools > P0 Play Test > Run Coop Hunt Test`.
- 더미 러너에는 음성을 붙이지 않는다 (마이크 이중 캡처·에코 방지) — VoiceNetworkObject 가드가 이를 전제.
- 부수 함정: PeerMode 변경 등으로 config가 재임포트될 때 프리팹 테이블 참조가 깨질 수 있음 — 증상이 "스폰 무음 실패"면 `Tools > Fusion > Rebuild Prefab Table` 먼저. Play 중에는 재컴파일이 보류되므로 브릿지 자동화 시 Play 종료 후 Refresh.

## Meta Avatars 통합 (2026-07-08 — VR 리그 + 네트워크 아바타)

### 샘플 임포트가 선행 조건 (Package Manager GUI 작업)
네트워크 아바타는 Meta가 통째로 제공(`FusionAvatarSdk28Plus.prefab` = NetworkObject+NetworkTransform+`AvatarBehaviourFusion`+`AvatarEntity`, core 패키지에 이미 컴파일됨)하지만, 씬 런타임에 **`OvrAvatarManager` 프리팹 + 입력 매니저가 필요한데 이 둘은 패키지의 `Samples~`에만 있다.** Package Manager > Meta Avatars SDK > Samples > **"Sample Scenes" Import** 필수 (스크립트로 대체 불가). 임포트 시 `Assets/Samples/.../`에 `AvatarSdkManagerStyle2Meta.prefab`(Recommended), `SampleInputManager`(로컬 바디 트래킹), `SampleAvatarEntity`가 들어온다.

### 샘플 임포트 = Platform SDK 203 비호환 2건 패치 필요 (재임포트하면 되돌아감)
"Sample Scenes"는 구 Platform SDK 기준이라 이 프로젝트의 Platform SDK 203.0.0과 컴파일이 깨진다. 자체 asmdef(`Oculus.AvatarSDK2.Samples`)라 게임 코드를 직접 막진 않지만 에러가 있으면 Play가 막힌다. 패치:
- `AvatarEditorUtils.cs` `LaunchAvatarEditor()` — `Oculus.Platform.CAPI`가 internal화돼 `ovr_Avatar_LaunchAvatarEditor` 직접 호출 불가. 에디터/스탠드얼론 브랜치를 no-op(경고)으로 스텁 (아바타 에디터는 Quest 기기 전용 데모, 파이프라인 불필요).
- `SampleSceneLocomotion.cs` — `yRotate`가 `#if USING_XR_SDK` 블록 안에만 선언돼 `#if UNITY_EDITOR` 브랜치에서 미정의. 메서드 스코프로 올림.
- **Voice와 동일 caveat**: 샘플을 재임포트/업데이트하면 이 패치가 전부 되돌아간다 — 이 절차를 다시 적용.

### Meta의 AvatarSpawnerFusion을 쓰지 않는다 — 커스텀 AvatarController
`AvatarSpawnerFusion`은 `FusionBBEvents.OnSceneLoadDone`(Meta Building Blocks 표준 부트스트랩 이벤트)로 러너를 얻는데, 이 프로젝트는 커스텀 `SessionLauncher`가 런타임에 러너를 AddComponent하므로 그 이벤트가 발화하지 않아 아바타가 영영 스폰되지 않는다(위 Voice의 "런타임 AddComponent된 러너는 자동 콜백 수집에서 빠진다"와 같은 뿌리). 그래서 `CampLantern.Networking.Avatar.AvatarController`가 `VoiceController`와 동일하게 `SessionLauncher.SessionStarted`에 직접 얹어 `runner.Spawn(FusionAvatarSdk28Plus, ..., runner.LocalPlayer, onBeforeSpawned: LocalAvatarIndex 설정)` 한다. `[[room-architecture]]`의 SessionLauncher 흐름을 그대로 따른다.
- **P0 범위**: `OculusId=0`으로 스폰 → Meta 계정 entitlement 없이 프리셋(테스트) 아바타. 실제 유저 아바타는 `OvrAvatarEntitlement.SetAccessToken`(Platform SDK entitlement)가 필요 — 후속. 에디터에서 `Licensing 404 entitlement` 로그는 정상(프리셋 폴백).
- **런타임 선행조건**: `OVRCameraRig`(`AvatarBehaviourFusion`이 `OVRManager.instance`로 찾음) + `OvrAvatarManager`(Style2Meta) + `SampleInputManager`가 있어야 함. **이 셋은 씬에 배치하지 않고 `Player.PersistentPlayer`(`Resources/PersistentPlayer.prefab`)에 실어 `DontDestroyOnLoad`로 어느 씬에서든 자동 공급한다** — 씬에 중복 배치하면 OVRManager/OvrAvatarManager 이중 인스턴스로 깨진다(예전엔 P0Playground에 in-scene AvatarSystem으로 배선했으나 영속 플레이어로 이관). 씬에 남는 네트워크 배선은 `AvatarController`(아바타)와 음성(`VoiceController`+`PlayerMute`) — 둘 다 Network 오브젝트에 붙는 "네트워크 소셜 스택"이며, `P0PlaySceneFactory`/`RoomScenesFactory` 재생성 시 공통 배선한다(기존 씬은 `Tools > Make Assets > Wire Networked Social Stack Into Room Scenes`로 사냥터·낚시터에 idempotent 패치). 낚시터도 드롭인 멀티라 근접 음성을 넣지만, 낚시 행위 자체는 로컬 로직이다(공간만 멀티 — `[[social-cooperation]]`).
- 스폰 프리팹은 Fusion 프리팹 테이블에 있어야 함 — 임포트로 자동 베이킹되나 무음 실패 시 `Tools > Fusion > Rebuild Prefab Table`.

### Meta XR Simulator — Activate 메뉴로만 켠다 (manifest 편집 금지) (2026-07-08 확정)
헤드셋 없이 에디터에서 VR을 테스트하려면 시뮬레이터가 필수다(XR 런타임을 제공). 없으면 Play 진입 시 OpenXR가 HMD를 못 찾아 `XR_ERROR_FORM_FACTOR_UNAVAILABLE`로 실패한다.
- **`Meta > Meta XR Simulator > Activate` 메뉴로 켠다.** 이 메뉴(Core SDK의 `MetaXRSimulator` 폴더 제공)가 `XR_SELECTED_RUNTIME_JSON`을 **standalone 설치본**(예: `C:\Program Files\MetaXRSimulator\vXXX\meta_openxr_simulator.json`)으로 설정한다. Play 검증은 브릿지로 `EditorApplication.ExecuteMenuItem("Meta/Meta XR Simulator/Activate")` 호출.
- **manifest.json에 `com.meta.xr.simulator`를 직접 추가하지 마라 — 헛수고다.** 시뮬레이터가 standalone을 쓸 때, 자체 로직(`Meta.XR.Simulator.Editor.PackageManagerUtils.RemovePackageAsync`)이 "비활성인데 UPM 패키지가 있네" 하고 즉시 되돌려 제거한다. (2026-07-08 이걸로 한참 헤맴 — manifest 편집→자동 제거 반복.)
- UPM 패키지를 manifest에서 빼도 시뮬레이터는 standalone으로 동작한다. 단 **제거 직후 Bee 빌드 캐시에 `MetaXrSimulator.Editor.asmdef` 참조가 남아 "Could not read file … asmdef"로 컴파일이 깨질 수 있음** → `CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.CleanBuildCache)`로 캐시 청소하면 해소.
- 시뮬레이터 바이너리 미설치 시: `Edit > Preferences > Meta XR > Meta XR Simulator > Available Versions`에서 다운로드(자동 다운로드는 Meta CDN — 버전 안 받아지면 다른 버전 시도).

### 아바타 프리셋 로딩 — 빈 zip 이슈 (2026-07-09 해결)
증상: 로컬 아바타 스폰 시 `{index}_{platform}.glb NotFound`(에디터=`_rift`, 기기=`_quest`) + `ovrAvatar2 OvrAssert` 실패. 아바타는 손/스켈레톤만 렌더되고 프리셋 몸체 로드 실패.
- **진짜 원인**: rift/quest·인덱스 문제가 아니라, **SDK가 로드하는 `PresetAvatars_*.zip`이 전부 빈 zip(0KB)**이었다(어제 "인덱스 제한" 추정은 틀림). 프리셋 소스 .glb(33종, ~660MB)는 `SampleAssetsUnzipped/`에 풀려 있으나 **zip 패키징 단계(Preset Selector로 골라 Package)가 안 된 상태**. 런타임 로드 경로: `AvatarEntity.LoadLocalAvatar()` → `{LocalAvatarIndex}` + `GetPlatformGLBPostfix()`(에디터=rift) → `LoadAssets(..., AssetSource.Zip)` → 빈 zip에서 NotFound.
- **정식 방법**: `MetaAvatarsSDK/Assets/Sample Assets/Preset Selector`로 소량 선택 → 패키징. (전체 `Package Presets`는 Standard Quest+Rift ~300MB라 과함. `RepackageWithPresetSelections(bool[] quality, bool[] avatars)`는 bool[] 배열 인자라 브릿지/리플렉션 자동화가 까다로움.)
- **이번 처리**: 앞 8종(0~7) × Quest+Rift 소스 .glb를 직접 `PresetAvatars_Rift.zip`/`_Quest.zip`에 flat 엔트리(`4_rift.glb` 등, 로드 경로와 일치)로 넣어 소량 패키징(~73MB). 런타임이 `Added zip source .../PresetAvatars_Rift.zip`로 직접 읽어 정상 로드 확인(로그: `ClipUpgradeHelper hands_riftController`, NotFound 소멸).
- **주의(대용량 미커밋)**: 채운 zip(35~38MB×2)과 `SampleAssetsUnzipped/`(660MB)는 저장소에 커밋하지 않는다. zip은 빈 상태로 이미 추적 중이라 `git update-index --assume-unchanged`로 로컬 변경을 숨김 — **팀원은 클론 후 빈 zip을 받으므로 각자 Preset Selector(또는 동일 수동 방식)로 채워야** 에디터 아바타가 뜬다. `AvatarController.m_presetAvatarCount`(현재 6)는 패키징한 개수(≤8) 이하로 유지.

## 숨은 규칙 / 암묵지
- Meta Avatars SDK를 Asset Store에서 검색해도 안 뜨는 게 정상이다 (EOF라 검색 노출이 약함). `developers.meta.com/horizon/downloads/package/meta-avatars-sdk/`에서 직접 받아야 한다.
- Fusion App ID와 Voice App ID는 **같은 `PhotonAppSettings` 에셋의 다른 필드**(App Id Fusion / App Id Voice)에 들어간다 — 별도 설정 파일이 아니다.

## 수정 시 주의
- 백엔드가 확정되면 이 문서의 "영구 저장 백엔드" 행을 갱신하고, `domain/room-architecture.md`의 오프라인 방문 섹션에도 반영해야 한다.
- Meta가 Avatars SDK를 공식 후속 SDK로 대체 발표하면(현재 시점 미확인) 이 문서와 마이그레이션 필요 여부를 같이 검토.
