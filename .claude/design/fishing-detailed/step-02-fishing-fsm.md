# Step 02: Fishing — 물고기 FSM 스켈레톤

- **영역:** `Fishing` (`Assets/Scripts/Fishing/`)
- **선행 단계:** step-01 완료 (`FishInstance`, `FishingTuning`, `FishingFormulas`)
- **후행 단계:** step-03~06이 이 `Fish`의 상태·전이·이벤트를 채운다

---

## 목적
스펙 §3-1 물고기 FSM의 **뼈대**를 세운다. 8개 상태 enum, 줄 색 enum, 상태 전이 메서드(`SetState`), 외부 훅 이벤트(상태/줄색/체력/후킹/획득)를 정의하되 **각 상태의 동작은 비워둔다**(다음 단계에서 채움). `Fish`는 `FishInstance`를 들고 자신의 체력·텐션을 소유한다.

---

## 에이전트 실행 지침

`/task-start` 후, §3-1 전이표를 열어 상태·전이만 구조화한다.

### 생성 파일
- `Assets/Scripts/Fishing/FishState.cs` — 생성 (enum)
- `Assets/Scripts/Fishing/LineColor.cs` — 생성 (enum)
- `Assets/Scripts/Fishing/Fish.cs` — 생성 (FSM 본체 MonoBehaviour)

### 핵심 심볼
```csharp
public enum FishState { Idle, Approach, Bite, FightNormal, FightEscape, FightShake, Hooked, Caught }
public enum LineColor { None, White, Red, Green }   // §2: 평상=흰, 도망=빨강, 후킹직전=초록

public class Fish : MonoBehaviour
{
    [SerializeField] private FishingTuning m_tuning;   // step-01

    public FishState State { get; private set; }
    public LineColor Line  { get; private set; }
    public FishInstance Instance { get; private set; } // 캐스팅 시 주입
    public float Health { get; private set; }          // 초기 = Instance.MaxHealth
    public float Tension { get; private set; }         // 초기 = rod.tension (Fight 진입 시)

    public event System.Action<FishState> StateChanged;
    public event System.Action<LineColor> LineColorChanged;
    public event System.Action HealthChanged;          // UI/햅틱 훅
    public event System.Action Hooked;                 // 체력 0 도달
    public event System.Action Caught;                 // 낚아올림 성공(§3-1 Caught)

    // 스포너(FishingSpot)가 개체 주입 + 스폰 위치 지정
    public void Initialize(FishInstance instance);

    // 상태 전이 — 이 단계는 시그니처+SetState만. 동작은 후속 단계.
    private void SetState(FishState next);   // 같은 상태면 무시, StateChanged 발화
    private void SetLine(LineColor color);   // LineColorChanged 발화
}
```

### 선행 산출물 의존성
- `FishInstance`, `FishingTuning`, `FishingFormulas` — step-01

### 제약
- 이 단계는 **구조만**: enum, 필드, 프로퍼티, 이벤트, `SetState`/`SetLine`, `Initialize`. 각 상태의 진입/처리/탈출 로직은 비워두고 다음 단계 주석(`// step-03에서 구현` 등)으로 자리만 표시.
- `Initialize`에서 `Health = Instance.MaxHealth`, `State = Idle`, `Line = None`으로 초기화(코드로 확정, scripts.md).
- 이벤트 구독은 없지만, 후속 단계가 붙일 것을 대비해 이벤트는 전부 여기서 선언.
- RULE-03: 아직 물리 미사용. Update에서 타이머는 후속 단계에서 추가(90Hz 경량 유지).

### 완료 판정
- [ ] `Grep "enum FishState" Assets/Scripts/Fishing/` — 8상태 확인 (Idle/Approach/Bite/FightNormal/FightEscape/FightShake/Hooked/Caught)
- [ ] `Grep "class Fish" Assets/Scripts/Fishing/Fish.cs` + 5개 이벤트 선언 확인
- [ ] Unity 컴파일 통과
- [ ] `SetState`가 `StateChanged` 발화, `Initialize`가 `Health=MaxHealth` 설정 확인

---

## 금지 사항
- 상태별 동작(캐스팅/입질/파이팅/도망) 구현 금지 — 이 단계는 뼈대까지만.
- 기존 `FishingRod.cs`의 `FishingState` enum(Idle/Casting/Waiting/Biting/Caught/Missed)과 **혼동·재사용 금지**. 새 `FishState`는 물고기 FSM으로 별개. 구 FishingRod 정리는 step-08.
