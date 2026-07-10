# Unity Editor 자동화 — ClaudeBridge 스택

이 프로젝트는 **Unity Editor 작업을 Claude가 직접 수행**할 수 있도록 자체 도구를 내장한다. 에이전트는 Editor 조작을 "사용자에게 시키는" 대신 이 스택을 써서 끝낸다.

## 계층

```
Claude Desktop 채팅
        │
        ▼
  claude-bridge MCP (Python)             ← scripts/claude-bridge-mcp/server.py
        │  unity_call / unity_batch_flush / unity_bridge_status
        ▼
  .claude-bridge/inbox/*.json            ← 커맨드 드롭
        │
        ▼
  ClaudeBridge (Unity C# Editor)         ← Assets/Editor/ClaudeBridge/
        │  Dispatcher → Ops.* (리플렉션)
        ▼
  .claude-bridge/outbox/*.json           ← 결과 드롭
```

## 두 가지 실행 모드

| 모드 | 트리거 | 사용 시점 |
|---|---|---|
| **GUI 상주** (실시간) | `/run editor` 후 Window > Claude Bridge > Start | Editor 화면 보면서 단계별 확인이 필요할 때, 사용자가 Play Mode 누를 예정 |
| **Headless 배치** | `/run bridge` 또는 MCP `unity_batch_flush()` | 사용자가 Editor 안 켜놓음. 여러 커맨드를 한 번에 몰아 실행 |

## 에이전트 호출 패턴

에이전트가 Unity 작업을 해야 할 때 순서:

1. **상태 확인** — `unity_bridge_status()` 로 Editor 가동·큐 점검. `editor_running: null` 이면 macOS/Linux 아닌 환경이거나 정보 부족.
2. **모드 결정**
   - 사용자가 Editor를 보면서 진행 중이면 → 그대로 `unity_call` 반복
   - 사용자가 자리 비운 상태 또는 Editor 안 떠 있음 → `unity_call` 로 쌓고 마지막에 `unity_batch_flush()`
   - MCP 래퍼가 없고 Filesystem MCP만 있는 환경 → `.claude-bridge/inbox/<id>.json` 직접 쓰기 + `/run bridge` 호출
3. **실행** — [`../Assets/Editor/ClaudeBridge/README.md`](../../Assets/Editor/ClaudeBridge/README.md) 의 op 레퍼런스 사용
4. **검증** — outbox의 `ok: true` 확인. 실패면 `error` 필드 사용자 보고 후 다음 단계 중단

## SVG 아이콘 파이프라인 (Claude가 직접 그리는 경로)

Claude는 **단순 도형 SVG를 직접 작성**해서 Unity Sprite로 임포트할 수 있다. 이미지 생성 모델·외부 래스터라이저 모두 필요 없다.

```
1) SVG 작성 (인라인 문자열 또는 Assets/Art/Icons/<name>.svg)
2) unity_call("Sprite.ImportFromSvg", { svgText|svgPath, pngPath, width, height, ppu, ... })
      └─ Unity Vector Graphics (com.unity.vectorgraphics) 가 파싱·테셀레이트·렌더
      └─ Texture2D.EncodeToPNG → 디스크 저장
      └─ AssetDatabase.ImportAsset + TextureImporter(textureType=Sprite) 까지 한 op
```

자주 쓰는 viewBox="0 0 100 100" SVG 패턴:
- 원형 배지 / 둥근 사각형 / 별 / 체크 / X / 화살표 / 다이아·하트·스페이드·클로버 / 기어 / 방패 / 말풍선

상세 예시와 op 호출 인자는 [`../skills/make-asset/SKILL.md`](../skills/make-asset/SKILL.md) §4-4-A.

**외부 바이너리 제거됨**: 과거에는 `rsvg-convert` / `magick` / `qlmanage` 로 PNG 래스터화했지만 이제 Unity 내장 렌더링(`VectorUtils.RenderSpriteToTexture2D` + MSAA 4x)으로 대체. 개발자 머신에 추가 설치 불필요, `com.unity.vectorgraphics` 패키지만 있으면 OK (manifest.json 기본 포함).

**한계**: Unity Vector Graphics 는 `filter`, `mask`, 외부 이미지 참조(`<image href>`) 등 일부 스펙 제한적. 결과가 어긋나면 SVG 를 path/polygon/rect/circle + 단색 fill/stroke 로 단순화한다.

## 자주 쓰는 op 조합

### 씬 초기 조립 (솔리테어 예)

```
Scene.New              → Assets/Scenes/Solitaire.unity
GameObject.Create      → Canvas
Component.Add          → /Canvas, UnityEngine.Canvas
Component.Add          → /Canvas, UnityEngine.UI.CanvasScaler
Component.Add          → /Canvas, UnityEngine.UI.GraphicRaycaster
Component.SetField     → /Canvas, Canvas.renderMode = ScreenSpaceOverlay
GameObject.Create      → GameRoot, parent=/Canvas
Component.Add          → /Canvas/GameRoot, Project.Core.SolitaireGame
Component.SetField     → cardPrefab ← Assets/Prefabs/Card.prefab
Scene.Save
```

### UGUI 프리팹 중첩 (Card 안에 Suit 아이콘)

```
Prefab.Open              → Card.prefab
Prefab.InstantiateAsChild → Suit.prefab as /Card/Suit  (원본 링크 유지)
Component.SetRectTransform → /Card/Suit, anchor/pivot/position
Prefab.Save
Prefab.Close
```

### Variant 만들기

```
Prefab.CreateVariant → sourcePath=Card.prefab, variantPath=Card_Back.prefab
Prefab.Open          → Card_Back.prefab
Component.SetField   → Image.sprite ← card_back.png  (오버라이드만 변이)
Prefab.Save
Prefab.Close
```

## 컴파일 · Play Mode 검증 런북 (직접 파일 드롭 방식) — 실측 확인됨 2026-07-09

MCP 래퍼(`unity_call`)가 없는 환경에서도 **`.claude-bridge/inbox/<id>.json` 직접 쓰기 + outbox 폴링**만으로 컴파일·플레이 검증을 끝낼 수 있다. GUI 상주 서버가 떠 있으면(`[ClaudeBridge] Started` 로그) 200ms 폴링으로 픽업된다. **이 절차로 "사용자에게 플레이 시키지 말고" 에이전트가 직접 검증한다.**

### 커맨드 봉투 & 폴링

```jsonc
// 드롭: .claude-bridge/inbox/<id>.json
{"id":"zzX","op":"Reflection.Invoke","argsJson":"{\"typeName\":\"...\",\"methodName\":\"...\",\"targetInstanceId\":\"\",\"argTypes\":[],\"argsJson\":[]}"}
// argsJson은 op별 구조체를 "문자열로" 직렬화(내부 따옴표 이스케이프). 정적 메서드면 targetInstanceId="".
```
PowerShell 폴링: `while(-not(Test-Path outbox\zzX.json)){Start-Sleep -Milliseconds 400}` → `ok:true` 확인. 처리된 inbox 파일은 브릿지가 자동 삭제.

### 컴파일 검증 (2가지, 병행)
1. `Asset.Refresh`(argsJson `"{}"`) → 재컴파일. **응답은 리로드에 먹힐 수 있으니** 결과 판정은 로그로: `%LOCALAPPDATA%\Unity\Editor\Editor.log`에서 `error CS` 0건.
2. **가장 확실**: 이번에 추가/수정한 `public static` 메서드를 `Reflection.Invoke`로 **직접 호출**. `ok:true`(Method not found 아님)면 = 그 어셈블리가 최신 코드로 컴파일·로드됨이 증명된다. Editor 어셈블리(팩토리)가 런타임 어셈블리를 참조하므로 **양쪽 클린 컴파일이 전이적으로 증명**된다. (대안: `Reflection.Invoke UnityEditor.EditorUtility.get_scriptCompilationFailed` == `"False"`.)

### Play Mode 스모크 테스트 절차
Reflection.Invoke 대상(전부 `UnityEditor.EditorApplication` 정적, 무인자): `EnterPlaymode`(void) / `get_isPlaying`(bool) / `ExitPlaymode`(void). 스크린샷은 `UnityEngine.ScreenCapture.CaptureScreenshot` (argTypes `["System.String"]`, argsJson `["<절대경로>.png"]`, 다음 프레임에 비동기 기록).

1. `Scene.Open` 으로 검증할 씬 열기 (argsJson `{"path":"Assets/Scenes/Xxx.unity"}`).
2. **로그 기준점 기록**: `(Get-Content Editor.log | Measure-Object -Line).Lines` → `N`. ← **필수**. 안 하면 직전 플레이의 예외가 섞여 오탐.
3. `EnterPlaymode` → 8~11초 대기 → `get_isPlaying` == `"True"` 확인 (에디터 안 멈춤 = 씬 로드 OK).
4. `CaptureScreenshot` → 3초 대기 → PNG를 Read 툴로 직접 눈으로 확인(렌더/아바타 손 등).
5. **신규 로그만 검사**: `Get-Content Editor.log | Select-Object -Skip N | Select-String 'AssertionException|NullReferenceException|error '`. 내 코드 태그(`[FishingGroundHarness]` 등)도 같이 grep.
6. `ExitPlaymode` (실패해도 반드시). 마지막에 `zz*.json` 임시 파일 inbox/outbox에서 삭제.

### 함정 (이번에 실제로 겪음)
- **한글 Debug.Log는 콘솔에서 깨진다(mojibake)** → grep은 ASCII 마커로: `[MakeAssets]`, `error CS`, `AssertionException`, `ClipUpgradeHelper`(아바타 로딩 정상 신호).
- **`Asset.Refresh`/`SaveAsPrefabAsset` 후 도메인 리로드로 내 `[MakeAssets]` 메시지가 로그 아래로 밀린다** → `-Tail 400` 이상 넓게 검색.
- **검증 불가 구간**: 상대 아바타·음성(진짜 2피어 필요 — 더미 피어는 아바타·음성 둘 다 안 띄움), 헤드셋 상호작용(스크린샷 정지 화면까지만).
- **연쇄 필수 참조**: 리그 필드처럼 "A를 넣으면 B도 필수"인 경우가 있다(예: `FirstPersonLocomotor._playerOrigin` 넣으면 `_playerEyes`도 요구) → 한 번 고치고 play로 재검증해 남은 Assertion 확인, 없어질 때까지 반복.
- **도메인 리로드는 이전 컴파일의 콘솔 경고를 재출력한다** — 플레이 진입 직후 로그에 CS0618 등이 보여도 최신 컴파일 결과가 아닐 수 있다(수정 전 어셈블리의 메시지 재생). 경고·호출부 잔존 여부는 로그가 아니라 **소스 grep**으로 확정한다.

### 게임플레이 무인 검증 — 봇 주입 패턴 (실측 2026-07-09)

IMGUI 버튼·VR 입력은 에이전트가 누를 수 없다. 대신 **자가 검증 봇(MonoBehaviour)** 을 주입해 게임플레이 풀 루프를 무인 주행한다 — FSM·수식 실주행을 사람 없이 검증하는 유일한 경로:

1. **봇 작성**: `Start`에서 대상 시스템을 `FindFirstObjectByType`로 자가 배선 + 입력 어댑터가 있으면 비활성화(제어 경합 방지). `Update`에서 상태 폴링→코어 API 호출(올바른 플레이/나쁜 플레이 페이즈 분리 권장). 전이·결과를 고유 ASCII 태그(예: `[FishTest]`)로 `Debug.Log` + 타임아웃 가드.
2. **주입**: 컴파일 → 플레이 진입 → `GameObject.Create`(호스트 GO, 루트) → `Component.Add`(봇 타입 풀네임) 드롭. 브릿지는 플레이 중에도 커맨드를 처리한다.
3. **판정**: 30~90초 후 Editor.log에서 태그 grep — 전이 로그를 스펙 상태전이표와 대조하고, 수치(개체 롤·감소율 등)를 수식 기대값과 소수점까지 대조한다.

예: `Assets/Scripts/Fishing/FishingLoopSelfTest.cs`(포획·줄끊김 양 경로 + 전이 로그) — 회귀 검증에 재사용.

## 에이전트가 선제 호출해도 되는 순간

사용자 명령 없어도 판단해서 호출:

- **씬에서 참조되는 프리팹이 `Assets/Prefabs/` 에 없을 때** → `/make-asset ui` 로 임시 블록 프리팹 생성 → 계속 진행. 한 줄 고지: "Card 프리팹 없어서 색 블록으로 임시 생성합니다. 나중에 이미지 있으면 말씀하세요."
- **심볼/아이콘이 필요한데 이미지 없을 때** (하트·별·체크·X·화살표·기어·카드 슈트 등) → 사용자 요청 기다리지 말고 `/make-asset sprite` 로 **SVG 직접 작성 → PNG 래스터화 → 임포트**. Claude는 단순 도형 SVG를 스스로 그릴 수 있으므로 이미지 요청은 사진·복잡한 일러스트일 때만.
- **스크립트 수정·신규 후 동작 확인이 필요한데 Editor가 닫혀 있을 때** → `/run editor` 로 GUI 띄우거나 `/run bridge` 로 헤드리스 검증
- **씬 조립 커맨드 10개+를 쏟아낸 직후** → `/run bridge` 로 일괄 실행 (매 op마다 Editor 왕복하면 느림)

## 한계 / 함정

- **Editor 락**: 같은 프로젝트로 Editor가 열려 있는 상태에서 `/run bridge` 호출 시 Unity가 "project already open"으로 실패. `unity_bridge_status()` 의 `editor_running` 을 반드시 먼저 확인.
- **Domain Reload**: 이 프로젝트는 Domain Reload 비활성화. ClaudeBridge도 `[InitializeOnLoad]` 생성자에서 static 상태 초기화를 수동으로 하므로 안전하지만, 새 op 추가 시 static 필드가 들어가면 RULES.md RULE-01 따라 `[RuntimeInitializeOnLoadMethod]` 초기화 붙이기.
- **재컴파일이 in-flight 응답을 먹는다**: `Asset.Refresh`(또는 `.cs` 변경 후 Refresh)가 스크립트 재컴파일→어셈블리 리로드를 유발하면, 그 커맨드의 outbox 응답이 리로드에 삼켜져 `NO_RESPONSE`로 보인다(브릿지는 리로드 후 재등록됨). **원격 재컴파일 성공 여부는 outbox 응답이 아니라 읽기전용 op를 새로 드롭해 확인** — `Reflection.Invoke get_scriptCompilationFailed`(== `"False"`면 성공), 그리고 `Editor.log`에서 마지막 `FinalizeReload` 이후 `error CS` 없음을 교차 확인.
- **대용량 임포트 중 폴링 정지**: 브릿지는 `EditorApplication.update`에서 200ms 폴링하는데, 대용량 에셋 임포트(예: Meta Avatars 샘플)나 Editor 비포커스 시 update 루프가 멈춰 커맨드가 처리되지 않는다(무응답). 드롭한 커맨드는 큐에 남으니 재드롭 없이 outbox를 계속 폴링하면 idle 복귀 즉시 처리된다. `Get-Process Unity`의 CPU 델타가 0에 수렴하면 임포트 완료 신호.
- **파일 락 경합**: GUI 서버가 200ms 폴링하는 동안 배치 모드 진입은 불가. 모드 전환은 명시적으로(`Stop` → 배치 → 끝나면 `Start`).
- **JsonUtility 한계**: Dictionary / polymorphic 타입 직렬화 약함. 새 op은 구조체(POCO)로 args/result 정의 필수.
- **리로드 경합**: `Asset.Refresh` 직후의 `Reflection.Invoke`는 **구 어셈블리로 처리될 수 있다**. 3형태 — Type not found(신규 타입) / Method not found(기존 타입의 신규 메서드) / **구버전 메서드 무증상 실행**(가장 위험 — 결과 로그 내용으로 신구 판별). FinalizeReload가 10~25초 걸리므로 대기 후에도 결과가 이상하면 동일 커맨드 재드롭이 정석. 신규 `public static` 직접 호출은 컴파일 증명을 겸한다(위 컴파일 검증 §2와 연계).
- **Editor.log 플러시 지연·스택트레이스 스팸**: 로그 1건당 스택트레이스 ~10라인이 붙어 `-Tail` 수백 라인으로는 마커가 금방 밀린다. 플레이 중 마커 검색은 수천~수만 라인 창으로. 이전 플레이의 로그가 늦게 플러시되어 새 기준점 뒤에 섞이기도 하므로, 같은 봇을 반복 주행했다면 **마지막 블록만** 신뢰한다.
- **멀티 피어 스폰은 추가 로드된 아레나 씬(P0NetArena)으로 들어간다**: 브릿지 `GameObject.Find`(활성 씬 루트 탐색)로는 스폰된 NetworkObject를 못 찾는다. 스폰 검증은 로그 마커·스크린샷·`Runner.GetAllBehaviours` 경유로 (실측 2026-07-10).
- **봇 격리**: 전역 상태(static 레지스트리, `PlayerHealth.All` 류)를 판정에 쓰는 검증 봇은 **단독 플레이 세션**에서 주행한다 — 같은 세션의 다른 봇 잔존 오브젝트가 오탐을 만든다 (실측: 전멸 감시 봇).

## 확장 가이드

새 op 추가 순서:
1. `Assets/Editor/ClaudeBridge/Protocol.cs` — args/result 구조체 추가
2. `Ops/XxxOps.cs` — 핸들러 함수 작성 (`string argsJson → string resultJson`)
3. `Dispatcher.cs` — `["Op.Name"] = Ops.XxxOps.Handler` 한 줄 등록
4. `Assets/Editor/ClaudeBridge/README.md` op 레퍼런스 표 업데이트
5. Python MCP 서버는 재시작 불필요 — `unity_call(op, args)` 의 op 문자열을 그대로 통과시키므로 자동 지원

## 병렬 작업 시 임포트 경합 주의

에이전트가 `.cs`를 쓰는 도중 다른 주체가 `Asset.Refresh`를 실행하면 GUID는 등록되나 컴파일 대상에서 빠지는 임포트 손상이 발생할 수 있다. `touch`·`ImportAsset(ForceUpdate)` 재임포트로 복구 불가 — `AssetDatabase.DeleteAsset` 후 파일 재생성만 통한다 (참조 없는 스크립트면 GUID 변경 무해).

- **규칙:** 병렬 에이전트 작업 중에는 Refresh 금지, 전원 완료 후 1회만.
- **증상:** 파일도 네임스페이스도 멀쩡한데 CS0246 (타입 못 찾음)이 반복.
- **진단:** `Library/Bee/artifacts/*.dag/Assembly-CSharp.rsp` 에서 해당 파일 누락 확인.
- **컴파일 검증:** `Asset.Refresh` → `Reflection.Invoke`로 `EditorUtility.get_scriptCompilationFailed` == `"False"` 확인. 에러 상세는 `%LOCALAPPDATA%/Unity/Editor/Editor.log`에서 `grep -a "error CS"`.
