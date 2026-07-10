# Unity Scripting — 고위험 함정 3선

다른 `knowledge/*.md`·`CLAUDE.md`·`RULES.md` 와 겹치지 않고, 모델이 **자주 틀리는** 항목만. 이 프로젝트는 **Unity 6000.3 (Unity 6)** 기준 — 학습 데이터가 Unity 2022 LTS 시절 API에 치우쳐 있으면 아래 §5의 리네임/폐기 API를 특히 주의.

---

## 1. 직렬화 (Serialization)

### 1-1. 깊이 7 제한

중첩된 `[Serializable]` class/struct/List/배열이 **7 단계를 넘으면 그 아래는 조용히 저장 안 됨**. 런타임 기본값으로 돌아온다.

- 재귀 트리 (노드가 자식 `List<Node>`) 는 이 벽에 거의 항상 부딪힌다.
- 회피: 자식을 `UnityEngine.Object` 파생 (ScriptableObject 등) 으로 **참조**로 끊기 → 직렬화가 참조 지점에서 종료.

### 1-2. null 필드 자동 부활

`[Serializable]` 커스텀 class 타입 필드가 `null` 이면, Unity 는 **그 타입의 기본 생성자로 새 인스턴스를 만들어 채운다.** `if (field == null)` 기반 로직이 망가진다.

- "비어 있음" 을 표현해야 하면 옵션:
  - `bool hasValue` 플래그를 같이 둔다 (가장 단순)
  - 타입을 `UnityEngine.Object` 파생으로 바꾼다 — Object 참조는 null 을 유지함
  - `[SerializeReference]` + 명시적 null — 이 속성은 예외적으로 null 을 허용

### 1-3. 인라인 복제

non-`UnityEngine.Object` `[Serializable]` 클래스는 **값처럼 인라인 직렬화**. 두 필드가 같은 인스턴스를 참조해도 저장→로드 후엔 **별개의 두 객체**가 된다. 공유가 필요하면 ScriptableObject 로 빼서 그걸 참조.

### 1-4. `[SerializeReference]` (2019.3+) — 다형성·null 허용

`Animal[]` 에 `Dog`/`Cat` 을 섞고 싶거나 null 을 유지하고 싶을 때:

```csharp
[SerializeReference] public IAction[] actions; // Dog/Cat/null 모두 OK
```

주의: reference 로 저장되므로 동일 asset 내부에서의 **참조 공유**도 유지된다 (§1-3 회피책으로도 쓸 수 있다). 단, GUID 기반이 아닌 내부 ID 라 에셋 경계 넘어서는 못 쓴다.

### 1-5. `ISerializationCallbackReceiver` 로 Dictionary 저장

Unity 는 `Dictionary<K,V>` 를 직렬화하지 않는다. 정석 패턴:

```csharp
public class Table : MonoBehaviour, ISerializationCallbackReceiver {
    [SerializeField] List<string> _keys = new();
    [SerializeField] List<int>    _vals = new();
    public Dictionary<string,int> Runtime = new();

    public void OnBeforeSerialize() {
        _keys.Clear(); _vals.Clear();
        foreach (var kv in Runtime) { _keys.Add(kv.Key); _vals.Add(kv.Value); }
    }
    public void OnAfterDeserialize() {
        Runtime.Clear();
        for (int i = 0; i < _keys.Count; i++) Runtime[_keys[i]] = _vals[i];
    }
}
```

`OnAfterDeserialize` 는 **메인 스레드가 아닐 수 있음** → Unity API 호출 금지. 순수 데이터 변환만.

---

## 2. 코루틴 — 자주 틀리는 것

### 2-0. 코루틴 시작 전 반드시 중복 방지

`StartCoroutine`을 아무 생각 없이 호출하면 같은 코루틴이 여러 개 동시에 돌 수 있다.
**반복·연속 호출 가능성이 있는 코루틴은 항상 핸들을 저장하고 시작 전에 끊는다.**

```csharp
// ✅ 권장
private Coroutine m_handle;

void StartWatch() {
    if (m_handle != null) StopCoroutine(m_handle);
    m_handle = StartCoroutine(MyRoutine());
}

private IEnumerator MyRoutine() {
    // ... 작업 ...
    m_handle = null; // 정상 종료 시 핸들 초기화
}

void StopWatch() {
    if (m_handle != null) {
        StopCoroutine(m_handle);
        m_handle = null;
    }
}

// ❌ 금지
void StartWatch() {
    StartCoroutine(MyRoutine()); // 핸들 없음 → 중복 실행, 정지 불가
}
```

체크리스트:
- `StartCoroutine` 결과를 `Coroutine` 필드에 저장한다
- 시작 전 `if (handle != null) StopCoroutine(handle)` 로 기존 것을 먼저 끊는다
- 코루틴이 정상 완료되면 핸들을 `null` 로 초기화한다
- 외부에서 강제 중단할 경로도 만들어둔다 (`OnDestroy`, `OnDisable` 등)

### 2-1. 코루틴 중단 조건 — 자주 틀리는 것

| 트리거 | 코루틴 중단? |

|---|---|
| `StopCoroutine(handle)` / `StopAllCoroutines()` | ✅ |
| `gameObject.SetActive(false)` | ✅ 즉시 |
| `Destroy(gameObject)` / `Destroy(component)` | ✅ |
| **`behaviour.enabled = false`** | ❌ **계속 돈다** |
| 씬 언로드 | ✅ |

→ `enabled=false` 로 컴포넌트를 "꺼도" 그 MB가 시작시킨 코루틴은 살아서 상태를 계속 바꾼다. 끄려면 반드시 명시적 `StopCoroutine` 또는 GO 비활성.

### `WaitForSeconds` GC — 반드시 캐싱

`new WaitForSeconds(t)` 를 코루틴 루프 안에서 매 반복 호출하면 매번 힙 할당 → GC 압력.
필드에 **한 번만 생성**해두고 재사용:

```csharp
// ✅ 권장
private readonly WaitForSeconds m_interval = new WaitForSeconds(0.5f);

private IEnumerator MyLoop() {
    while (condition) {
        DoWork();
        yield return m_interval; // 할당 없음
    }
}

// ❌ 금지
private IEnumerator MyLoop() {
    while (condition) {
        yield return new WaitForSeconds(0.5f); // 매 반복 GC
    }
}
```

`WaitForEndOfFrame`, `WaitForFixedUpdate` 도 동일하게 캐싱.

### `WaitForSeconds` vs `WaitForSecondsRealtime`

| | `timeScale = 0` 일 때 | 용도 |
|---|---|---|
| `new WaitForSeconds(t)` | **영원히 멈춤** | 게임 내 시간 (일시정지 영향 받음) |
| `new WaitForSecondsRealtime(t)` | 그대로 진행 | 일시정지 UI·메뉴 애니메이션·토스트 |

`Pause()` 만들면서 `WaitForSeconds` 쓴 타이머가 얼어붙는 버그 흔함. 실시간 기준이 맞다면 Realtime 쪽.

---

## 3. Editor 스크립트: SerializedObject.FindProperty 주의점

`FindProperty("fieldName")`은 존재하지 않는 필드명이면 `null`을 반환한다.
반환값 확인 없이 `.objectReferenceValue`에 접근하면 NRE 발생.

```csharp
// ✅ 권장 — null guard 또는 직접 프로퍼티 할당
slider.fillRect = fillGo.GetComponent<RectTransform>();

// ❌ 금지 — 필드가 없으면 FindProperty가 null 반환 → NRE
sliderSo.FindProperty("m_FillRect").objectReferenceValue = fillRect;
```

**특히 Unity 내장 컴포넌트(`Slider`, `ScrollRect` 등)의 private 필드명은 Unity 버전마다 바뀔 수 있다.** 존재가 확실하지 않으면 직접 public 프로퍼티로 설정한다.

- `Slider.fillRect` — `FindProperty("m_FillRect")` 대신 직접 할당
- `Slider`에는 `m_BackgroundRect` 직렬화 필드가 없음 (존재하지 않는 필드)
- 불가피하게 `FindProperty`를 써야 한다면: `var prop = so.FindProperty("name"); if (prop != null) prop.objectReferenceValue = value;`

---

## 4. IL2CPP Managed Code Stripping (iOS·콘솔·WebGL 빌드)

IL2CPP 백엔드는 스트리핑을 **끌 수 없다.** 런타임에 사용하는 타입·메서드가 정적 참조로 잡히지 않으면 링커가 제거 → `MissingMethodException` / `Type not found`.

### 잘리는 대표 케이스

- `Type.GetType("Foo.Bar")` / `Activator.CreateInstance(type)` — 문자열로만 참조되는 타입
- `JsonUtility.FromJson<T>` 의 `T` 가 어디서도 `new T()` 되지 않을 때 (순수 역직렬화 대상 POCO)
- `MakeGenericMethod` / `MakeGenericType` — AOT 가 조합을 미리 알아야 함
- `[SerializeField] MyPoco x;` 의 `MyPoco` 가 데이터로만 등장

### 보존 방법

#### `[Preserve]` — 소스에 직접

```csharp
using UnityEngine.Scripting;

[Preserve]
public class SaveDataV2 {
    [Preserve] public int level;
}
```

#### `link.xml` — `Assets/` 어디든

```xml
<linker>
  <assembly fullname="Assembly-CSharp">
    <type fullname="Project.Core.SaveData" preserve="all"/>
    <type fullname="Project.Combat.Effects.*"/>
  </assembly>
  <assembly fullname="ThirdPartyPlugin" ignoreIfMissing="1">
    <type fullname="ThirdParty.Foo" preserve="all"/>
  </assembly>
</linker>
```

- `preserve="all"`: 타입·멤버 전부 유지
- `ignoreIfMissing="1"`: 해당 어셈블리가 빌드에 없어도 에러 안 냄 (플러그인 방어)
- 네임스페이스 와일드카드 `Project.X.*` 로 묶기 가능

### IL2CPP 에서 아예 깨지는 것

- `System.Reflection.Emit` (런타임 IL 생성) — AOT 불가
- 동적 어셈블리 로드 (`Assembly.Load(byte[])`) — 일부 구성만 가능, 대부분 실패

### 실전 흐름

1. 개발 빌드: Managed Stripping Level = **Minimal/Low**
2. 릴리스 직전 **High** 전환 → 주요 경로 플레이테스트
3. `MissingMethodException` / `TypeLoadException` 뜨면 해당 타입을 `link.xml` 에 등록
4. 플러그인은 기본적으로 `ignoreIfMissing="1" preserve="all"` 로 선방어

---

## 5. Unity 2022 → Unity 6 (6000.x) API 리네임/폐기 — 학습 데이터 편향 주의

모델이 Unity 2022.3 LTS 시절 코드 패턴을 그대로 재현하면 6000.x에서 **경고 또는 컴파일 에러**가 난다. 이 프로젝트(6000.3.10f1)에서 특히 자주 틀리는 것:

### 5-1. `Rigidbody.velocity` / `angularVelocity` → `linearVelocity` / `angularVelocity`

Unity 6부터 `Rigidbody.velocity`(3D)는 `Rigidbody.linearVelocity`로 이름이 바뀌었다 (2D도 동일 패턴). 기존 이름은 **Obsolete 경고**를 내며 여전히 동작하지만, 신규 코드는 반드시 새 이름을 쓴다.

```csharp
// ✅ 권장 (Unity 6+)
rb.linearVelocity = dir * speed;

// ⚠️ Obsolete (2022 스타일 — 컴파일은 되지만 경고)
rb.velocity = dir * speed;
```

`RopeController`/`BobberController` 등 물리 기반 낚시 로직(RULE-03 대상)을 만지거나 새로 작성할 때 특히 확인.

### 5-2. `Object.FindObjectOfType` / `FindObjectsOfType` → `FindFirstObjectByType` / `FindObjectsByType`

2023.1부터 폐기(Deprecated). Unity 6에서는 확실히 경고가 뜬다.

```csharp
// ✅ 권장
var mgr = Object.FindFirstObjectByType<SomeManager>();
var all = Object.FindObjectsByType<SomeManager>(FindObjectsSortMode.None); // sortMode 필수 인자

// ❌ 금지 (Obsolete)
var mgr = FindObjectOfType<SomeManager>();
var all = FindObjectsOfType<SomeManager>();
```

정렬이 필요 없으면 `FindObjectsSortMode.None`이 더 빠르다 — 프로젝트 관행상 매 프레임 호출하는 코드가 아니어야 함(캐싱 원칙은 `scripts.md` 참조).

---

## 6. 입력 시스템 모드 확인 — legacy Input 런타임 예외

`ProjectSettings/ProjectSettings.asset`의 `activeInputHandler`가 **1(New Input System 전용)**이면
`UnityEngine.Input.GetKey` 등 legacy API가 **컴파일은 통과하지만 런타임에 InvalidOperationException**을 던진다.
(0=legacy 전용, 1=New 전용, 2=둘 다.) 컴파일러가 못 잡는 함정이라 입력 코드를 새로 쓰기 **전에** 반드시 확인한다:

```
Grep "activeInputHandler" ProjectSettings/ProjectSettings.asset
```

전용 모드면 `UnityEngine.InputSystem`을 쓴다 — `Keyboard.current`/`Mouse.current`는 장치 부재 시 **null**이므로 가드 필수.
`OVRInput`(Meta XR)은 자체 네이티브 백엔드라 이 설정과 무관하게 동작한다(장치 없으면 0 반환).

---

## 7. 런타임 AddComponent 조립 — Awake는 Configure보다 먼저 실행된다

`AddComponent<T>()` 호출 순간 T의 `Awake`가 **동기 실행**된다(활성 GameObject 기준). 그 시점엔 데이터
주입(Configure 류)도, 뒤이어 추가될 형제 컴포넌트도 아직 없다 — 프리팹 인스턴스(직렬화 완료 후 Awake)와
동작이 갈라지는 지점이다. 증상: 프리팹에선 되는데 코드 조립(검증 봇·런타임 스포너)에선 **조용히 비활성**.

- 대응 ①: Configure(데이터) 메서드가 파생 상태(내구도=최대치 등)까지 초기화한다 — Awake와 이중 초기화 허용.
- 대응 ②: 형제 컴포넌트 참조는 Awake 캐싱 대신 **지연 해석 프로퍼티**(null이면 GetComponent 재시도, 캐싱).

(실측 2026-07-09~10: 무기 내구도 0 오판, FSM 컨트롤러의 스킬 러너 미인식 — 둘 다 이 함정.)

---

## 8. 프레임 차분(스윕) 판정 + 순간이동 = 유령 히트

"이전 프레임 위치 → 현재 위치" 경로를 스윕하는 판정은 텔레포트(수납·리스폰·네트워크 위치 보정)를
실제 이동 경로로 오판해 경로상의 대상을 때린다. **변위 임계값 가드만으론 부족하다** — 로딩 히칭(긴 프레임)의
정상 이동도 프레임당 변위가 크고, 히칭과 겹친 텔레포트는 속도(변위/dt)가 정상 스윙 범위로 떨어져
속도로도 구분 불가(실측). **텔레포트시키는 주체가 명시적으로 통지하는 API**(이전 위치 무효화 —
`NotifyTeleported` 류)가 정석이고, 자동 가드(변위+속도 복합 조건)는 보조 안전망으로만 쓴다.

