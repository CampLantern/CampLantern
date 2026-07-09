# Claude Code Index — 지식·지침·스킬 인덱스

에이전트는 `/task-start`에서 이 파일을 **먼저** 읽고, 작업 주제와 매칭되는 파일만 선별 로드한다.  
모든 지침을 매 세션 통째로 로드하면 토큰 낭비다.

## 읽기 전략

1. **이 파일(`INDEX.md`)만 먼저 읽는다** (수백 토큰).
2. 작업 프롬프트의 키워드와 각 항목의 `keywords` 필드를 매칭한다.
3. 매칭된 파일만 `Read`로 펼친다. 매칭 안 되면 읽지 않는다.
4. `RULES.md` (루트)는 **항상 스캔** (불변 제약).
5. `CLAUDE.md`는 **항상 이미 로드되어 있다** (세션 시작 시 주입).

---

## Level 1 — Language & Engine (범용, 최상단)

다른 Unity/C# 프로젝트에도 그대로 적용되는 지식. 프로젝트 도메인보다 상위.

### [knowledge/RULES.md](knowledge/RULES.md) — 항상 스캔 (범용 코딩 강제 규약)
21개 프로그래밍 규약 (R1~R21).
- **keywords:** simplicity, abstraction, generalization, optimization, code review, dead code, naming, refactor, weed, parallel rework, comment
- **when to read:** **항상** 스캔. 이 파일은 1계층이며, 루트 [`../RULES.md`](../RULES.md)(프로젝트 불변 제약, 3계층)와 **다르다**.

### [knowledge/unity-scripting-gotchas.md](knowledge/unity-scripting-gotchas.md)
모델이 자주 틀리는 Unity 스크립팅 함정 3선.
- **keywords:** serialization depth, serialization null, ISerializationCallbackReceiver, SerializeReference, Dictionary serialize, inline serialization, coroutine stop, enabled false coroutine, WaitForSecondsRealtime, timeScale pause, IL2CPP, Managed Code Stripping, link.xml, Preserve, SerializedObject, FindProperty, NullReferenceException, Slider fillRect, Editor script, make-assets, Rigidbody velocity, linearVelocity, FindObjectOfType, FindObjectsOfType, FindFirstObjectByType, Obsolete, Unity 6, 2022 to 6000 migration, activeInputHandler, New Input System, InputSystem, legacy Input, Keyboard.current, InvalidOperationException
- **when to read:** `[Serializable]` 필드 설계, 코루틴으로 타이머/일시정지 구현, iOS·Quest 빌드 실패 (MissingMethod/TypeLoad) 대비, Editor Factory 스크립트 작성 시, Rigidbody/물리 코드나 FindObjectOfType류 코드를 새로 작성/수정할 때

### [knowledge/csharp-dotnet.md](knowledge/csharp-dotnet.md)
C#/.NET 언어 핵심 및 메모리 최적화.
- **keywords:** value type, reference type, struct, class, boxing, string, StringBuilder, event, delegate, subscribe, unsubscribe, generic, async, await, CancellationToken, property, field, LINQ, foreach
- **when to read:** 새 타입 설계, async·이벤트 구현, 자료구조 선택, 박싱/할당(GC) 회피 목적

### [knowledge/unity-mobile-performance.md](knowledge/unity-mobile-performance.md)
범용 Unity 모바일 성능 규칙 (Quest 등 모바일 XR 타겟 공통).
- **keywords:** profiling, GC, garbage collection, batching, draw call, SRP Batcher, texture compression, ASTC, audio compression, Canvas, Raycast Target, LOD, Occlusion Culling, shadow, lightmap, frame budget, thermal throttle, Update loop, object pool
- **when to read:** 프레임 드랍·GC 스파이크·빌드 용량 등 성능 이슈 조사 시, 90Hz 유지가 걸린 코드/에셋 설정 변경 시

### [knowledge/unity-editor-automation.md](knowledge/unity-editor-automation.md)
ClaudeBridge 스택 — Claude가 Unity Editor 작업(에셋 생성, 씬 조작, 컴파일 확인)을 직접 수행하는 방법.
- **keywords:** ClaudeBridge, bridge, inbox, outbox, unity_call, batch, headless, Editor 자동화, 씬 조작, GameObject 생성, 컴파일 확인, 임포트 경합, CS0246, rsp, scriptCompilationFailed, 병렬 에이전트, Play Mode 검증, 스모크 테스트, 플레이 검증, EnterPlaymode, ExitPlaymode, isPlaying, ScreenCapture, 스크린샷, 로그 기준점, Reflection.Invoke, Editor.log, 직접 파일 드롭, 봇 주입, 자가 검증 봇, 무인 통합테스트, Component.Add, stale 경고, CS0618 재출력
- **when to read:** Unity Editor 조작이 필요한 작업 시 (에셋/씬/프리팹 생성, Play Mode 확인, 컴파일 검증). **에이전트가 직접 컴파일·플레이 검증을 해야 할 때(런북 포함).** `/make-assets`·`/run`·`/qa` 스킬과 연계

### [knowledge/debugging/](knowledge/debugging/) — 디버깅 원칙 (per-file)
디버깅 방법론 원칙 10개.
- **keywords:** debugging, assumption, classify, Bohrbug, Heisenbug, symptom, root cause, fix, log, Debug.Log, print debugging, assertion, Assert, binary split, bisect
- **when to read:** 버그·예외(NRE)·오작동 조우 시 및 추적 시나리오 작성

### [knowledge/qa/](knowledge/qa/) — QA 원칙 (per-file)
QA 방법론 원칙 10개. 이 프로젝트(VR·인디) 맥락으로 적용.
- **keywords:** QA, test, testing, 검증, 릴리즈, regression, 회귀, 버그 리포트, severity, pass, fail, localization, 현지화, functional, non-functional, 90Hz, APK, 빌드 스모크
- **when to read:** `/qa` 스킬 실행 시, 릴리즈 전 검증 요청 시, 버그 리포트 작성 시

---

## Level 2 — Project Domain (이 프로젝트 전용)

GDD: [domain/gdd/VR_코지_소셜_영지_게임_기획서.docx](domain/gdd/VR_코지_소셜_영지_게임_기획서.docx) (원본, .docx라 Read 불가 — 아래 distilled 문서를 통해 참조)

### [domain/tech-stack-decisions.md](domain/tech-stack-decisions.md)
GDD엔 없는 세션 결정 사항 — Photon Fusion 2 vs PUN 2, Meta Avatars SDK(EOF v40.0.1), 영구 저장 백엔드 미정.
- **keywords:** Photon, Fusion, PUN, Voice, App Id, 넷코드, netcode, Meta Avatars, EOF, 백엔드, backend, 서버 DB, 패키지 버전, manifest.json, 음소거, mute, VoiceNetworkObject, Recorder, Speaker, 음성 아바타, AssembliesToWeave, 위빙, weave, NetworkProjectConfig, Unsupported Plugin, CleanBuildCache, 아바타, avatar, AvatarController, AvatarSpawnerFusion, FusionAvatarSdk28Plus, AvatarBehaviourFusion, FusionBBEvents, OvrAvatarManager, SampleInputManager, AvatarSdkManagerStyle2Meta, VRPlayerRig, OVRCameraRig, OVRManager, VR 리그, entitlement, Sample Scenes 임포트, AvatarEditorUtils, yRotate, Rebuild Prefab Table
- **when to read:** 네트워킹/멀티플레이 코드, 아바타 시스템, 저장소 관련 작업 착수 전

### [domain/room-architecture.md](domain/room-architecture.md)
Photon Room 공간 구조 — 로비/낚시터/사냥터/영지 4개 공간, 저장 데이터와 실시간 Room 분리.
- **keywords:** Room, roomName, estate_, 로비, 낚시터, 사냥터, 존 분할, zone, 샤딩, sharding, 마스터 클라이언트, 서버 권한, authoritative, 파티 연속성, 오프라인 방문, StartSession, SceneLauncher, LobbyHarness, FishingGroundHarness, HuntZoneHarness, EstateHarness, fishing_shard, hunt_zone_a, deviceUniqueIdentifier, SaveService, PlayerState, ContentRegistry, player_save.json, persistentDataPath, JSON 저장
- **when to read:** 씬 전환, 매칭, Room 생성/파괴, 파티 시스템 관련 작업 시

### [domain/estate-system.md](domain/estate-system.md)
영지 시스템 — 배치, 구매 방식, 캠프 수용량, 방문 권한, 오브젝트 카테고리.
- **keywords:** 영지, estate, 캠프 수용량, 배치, placement, 스냅, snap, 읽기 전용, 공동 편집자, 오브젝트 카테고리, 등급, 일반, 희귀, 에픽, 확장, 상한
- **when to read:** 영지 배치/편집 UX, 오브젝트 카테고리 추가, 성능 상한(수용량) 관련 작업 시

### [domain/resource-loop.md](domain/resource-loop.md)
낚시/요리/사냥 — 활동별 역할, 밸런스 원칙, 조작 상세.
- **keywords:** 낚시, fishing, 요리, cooking, 사냥, hunting, 레시피, recipe, 챔질, 릴링, 조합, 숙련도, 만찬, 도감, 트로피, 재료
- **when to read:** 자원 채집/가공/사냥 활동 구현, 재화 소스 밸런스 조정 시

### [domain/social-cooperation.md](domain/social-cooperation.md)
협동 유도 4단계 구조, Shared Ledger(공유 진행도) 규칙.
- **keywords:** 협동, Shared Ledger, 공유 진행도, 기여, 파티, party, 다구리, 재접속, 복원, 방문 보너스, 공용 시설
- **when to read:** 협동 이벤트, 파티 진행도 동기화, 재접속 복원 로직 구현 시

### [domain/mvp-scope.md](domain/mvp-scope.md)
MVP 단계 게이트(P0/P1/P2), 검증 지표, 착수 금지 목록.
- **keywords:** MVP, P0, P1, P2, 프로토타입, prototype, 검증 지표, 통과 기준, 로드맵, roadmap, 범위 제외, UGC, 우선순위
- **when to read:** **새 기능/시스템 착수 전 항상** — 해당 기능이 어느 단계인지, 착수 금지 목록에 있는지 확인

### [domain/economy.md](domain/economy.md)
재화 구조(코인+지정 재료), 소스-싱크, 수익화 모델.
- **keywords:** 코인, 재화, currency, 소스, 싱크, source, sink, 소프트 캡, soft cap, DLC, 수익화, 유료, IAP, 가격
- **when to read:** 재화 획득/소비 로직, 상점, 가격 책정, 유료 콘텐츠 설계 시

### 낚시 시스템 — [domain/fishing-system.md](domain/fishing-system.md) (구현 결정·암묵지, **구현 완료 2026-07-09**) + [gdd/낚시_시스템_구현_지시서.md](gdd/낚시_시스템_구현_지시서.md) (설계 스펙 §1~9 원본) + [gdd/낚시_시스템_세부_기획서.docx](gdd/낚시_시스템_세부_기획서.docx) (직접 Read 불가)
낚시 플로우 5단계(조준·캐스팅/입질/파이팅/후킹 직전/후킹 이후), 물고기 FSM(Idle/Approach/Bite/Fight-평상/Fight-도망/Fight-비늘털이/Hooked/Caught), 줄 색(흰색/빨간색/초록색) 기반 텐션·체력 파이팅 수식, 개체 생성 공유 품질 롤. 산출식(TODO(FORMULA))·임시 데이터(TODO(DATA))·튜닝 값(TODO(TUNING)) 3종 임시 처리 규칙 명시. 구현은 `Assets/Scripts/Fishing/` — fishId=FishDef.Id 연결 고리·새 어종 추가 4종 세트·무인 검증 봇은 domain 문서 참조.
- **keywords:** 낚시, fishing, 캐스팅, 입질, 챔질, 파이팅, 릴링, 텐션, tension, 도망, 비늘털이, 스윙, swing QTE, 후킹, hooked, 줄 색, 미끼, 낚싯대, rod, 내구도, durability, 물고기 FSM, Fish, FishState, FishingRod, FishingSpot 스포너, FishingFormulas, FishingTuning, FishingRodInput, FishingLoopSelfTest, 검증 봇, FishSpeciesData, fishId 매핑, 새 어종 추가, 실루엣, 에지 트리거, power, sizeMin, sizeMax, lengthMin, lengthMax, weightMin, weightMax, 공유 품질 롤, TODO(FORMULA), TODO(DATA), TODO(TUNING)
- **when to read:** 낚시(캐스팅/입질/파이팅/텐션·체력 수식/비늘털이 QTE) 코드 수정·어종 추가·튜닝 전 — domain/fishing-system.md 먼저, 수식 근거는 지시서 §7

### 전투 시스템 — [gdd/전투_시스템_세부_기획서.docx](gdd/전투_시스템_세부_기획서.docx) (원본, 직접 Read 불가·distilled md 아직 없음) + [gdd/전투_시스템_구현_지시서.md](gdd/전투_시스템_구현_지시서.md) (Claude Code 단계별 구현 프롬프트 모음, 0~8단계)
사냥터 전투 — 근접 무기 유효타 판정, 몬스터 FSM(Idle/Chase/Attack/Exhausted/Return), 어그로(근접 타겟팅+약점 고정/감쇄), 스킬 시스템(hit window), 활/화살(당김 기반 초속), 플레이어 규칙(다운/소생/전멸/HP 스케일링/홀스터·내구도), 마지막 단계로 네트워크 권한 분리.
- **주의:** 구현 지시서 md는 범용 템플릿 문구가 섞여 있음 (프로젝트명 "Camp & Craft", 네임스페이스 `CampCraft.Combat`, 경로 `Docs/...`) — 실제 착수 시 이 프로젝트 컨벤션(`CampLantern.Combat`, `Assets/Scripts/Combat/`, `.claude/domain/gdd/` 경로)으로 치환해서 진행할 것.
- **keywords:** 전투, combat, 근접, melee, 유효타, 무효타, 활, bow, 화살, arrow, 몬스터, monster, FSM, 어그로, aggro, 히스테리시스, 약점, weakpoint, 스킬, skill, SkillItem, 판정, hit window, hitWindowStart, hitWindowEnd, 판정 규약, 물리 콜백 금지, OnTriggerEnter 금지, IDamageable, ApplyDamage, HitInfo, 다운, 소생, 전멸, 홀스터, holster, 내구도, durability, HP 스케일링, 기여, contribution, MonsterData, MeleeWeaponData, BowData, ArrowData, CombatBalanceData
- **when to read:** 전투(근접무기/활·화살/몬스터 AI/어그로/스킬/다운·소생) 관련 코드 착수 전, 구현 지시서 단계 진행 시

---

## Level 3 — Immutable Constraints

### [../RULES.md](../RULES.md) — 항상 스캔
프로젝트의 절대 불변 제약. 위반 시 시스템이 실제로 망가진다.
- **RULE-01:** Domain Reload 트리거 금지 (InitializeOnLoad, asmdef autoReferenced)
- **RULE-02:** Unity 에셋 파일(.meta/.prefab/.unity/.asset 등) 직접 편집 금지
- **RULE-03:** 물리 연산은 FixedUpdate에서만
- **RULE-04:** ProjectSettings는 Claude가 직접 수정하지 않음
- **keywords:** domain reload, InitializeOnLoad, asmdef, meta, prefab, scene, FixedUpdate, physics, ProjectSettings
- **when to read:** 모든 작업 착수 시. 특히 에셋 파일, 물리, 프로젝트 설정 작업 시. (저장 데이터·플랫폼 분기 등 프로젝트 고유 RULE은 해당 시스템이 생기면 추가된다.)

---

## Level 4 — Path-scoped Rules

특정 경로/파일에 한정된 절대 코딩 규칙.

### [rules/scripts.md](rules/scripts.md)
범용 스크립팅 기본값 — 컴포넌트 캐싱, CancellationToken, 이벤트 구독 해제, Awake 초기화, GameObject 활성화 소유권, UI 초기화 순서. 프로젝트 고유 컨벤션이 정해지면 이 파일에 갱신.
- **keywords:** GetComponent caching, Update, FixedUpdate, CancellationToken, async, coroutine, event -=, event +=, OnDestroy, OnDisable, Awake, SetActive, Rigidbody isKinematic, Collider isTrigger, UI initialization order, OnEnable
- **when to read:** **C# 스크립트(.cs) 생성/수정 시 항상.** 특히 컴포넌트 초기화, 비동기 메서드 생성, 이벤트 구독, UI 초기화 시 필수 확인.

---

## Level 5 — Skills (on-demand)

`.claude/skills/` 하위 SKILL.md 파일로 정의된 커스텀 스킬. `/스킬명` 으로 호출.

| 스킬 | 파일 | 호출 시점 |
|---|---|---|
| `/task-start` | `skills/task-start/SKILL.md` | 코드 작업 착수 전 (자동 수행) |
| `/task-done` | `skills/task-done/SKILL.md` | 코드 작업 완료 후 |
| `/design` | `skills/design/SKILL.md` | 기획을 단계별 실행 계획으로 분해할 때 |
| `/make-assets` | `skills/make-assets/SKILL.md` | Unity 에셋(UI Prefab, Particle, Material 등) 생성할 때 |
| `/debug` | `skills/debug/SKILL.md` | 버그·예외·오작동 원인 추적할 때 |
| `/qa` | `skills/qa/SKILL.md` | 기획·규칙 기준으로 구현 검증할 때 |
| `/self-update` | `skills/self-update/SKILL.md` | 세션에서 습득한 지식을 지식 베이스에 반영할 때 |
| `/lsp-setup` | `skills/lsp-setup/SKILL.md` | C# LSP 연결 안 될 때 (자동 호출) |

## 인덱스 갱신 규칙
- `domain/` 폴더에 새 기획서나 시스템 설명서가 추가되면 이 인덱스(`Level 2`)에 `keywords`와 함께 반드시 등록한다.
- `keywords`는 작업 프롬프트에 실제로 등장할 고유 명사 위주로 작성한다.
