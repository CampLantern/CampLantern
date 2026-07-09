# Step 03: Fishing — 캐스팅·접근·입질(챔질)

- **영역:** `Fishing` (`Assets/Scripts/Fishing/`)
- **선행 단계:** step-02 완료 (`Fish` FSM 스켈레톤)
- **후행 단계:** step-04(챔질 성공 → Fight-평상)

---

## 목적
FSM 앞부분을 구현한다: **Idle→Approach→Bite** 그리고 챔질 판정(성공→FightNormal / 실패→Idle 리셋). 자원 비용(§4)의 미끼 차감(입질 시점)과 챔질 성공 시 내구도 차감을 이 단계에서 건다. 플레이어 진입점 `FishingRod.Cast/Chamjil`을 코어(입력 무관) API로 연다.

---

## 에이전트 실행 지침

`/task-start` 후, §2 플로우·§3-1(Idle/Approach/Bite)·§4 비용 매트릭스를 기준으로.

### 생성/수정 파일
- `Assets/Scripts/Fishing/FishingRod.cs` — **재작성**(구 타이밍 FSM 대체). 낚싯대 = 입력·내구도·텐션 컨트롤러.
- `Assets/Scripts/Fishing/Fish.cs` — 수정 (Idle/Approach/Bite 처리 채움)

### 핵심 심볼
```csharp
// FishingRod.cs — 구 Cast(FishingSpot)/Reel() 대체. RodData 소유 + 미끼/내구도 상태.
public class FishingRod : MonoBehaviour
{
    [SerializeField] private RodData m_rod;          // TODO(DATA) 임시 인스턴스 또는 하드코딩
    [SerializeField] private int m_baitCount = 10;   // TODO(DATA) UserData 미정
    public RodData Rod => m_rod;
    public int BaitCount => m_baitCount;
    public bool Broken => m_rod.durability <= 0;     // 0이면 수리 필요(§5-2)

    public event System.Action<Fish> Hooked;         // 현재 물고기가 Hooked 되면(후속 단계에서 연결)
    // 캐스팅: 조준한 Fish를 Approach로. 사거리(rod.length) 게이트.
    public void Cast(Fish target);
    // 챔질: Bite 윈도우 내 호출 → 성공(내구도 -1, Fight 진입) / 그 외 → 실패 처리
    public void Chamjil();
    public bool TryConsumeBait();                    // 입질 시점 미끼 -1 (§4). 부족하면 false
}

// Fish.cs 추가 처리
//   Idle: 자유 유영(위치는 대충/추후), 실루엣 표시 훅
//   Approach: 미끼 인지 → 미끼로 이동, 도달 시 Bite
//   Bite: 찌 흔들림 + biteWindowSeconds 타이머(§9 튜닝). 진입 시 미끼 -1 (FishingRod.TryConsumeBait)
//         윈도우 내 Chamjil → FightNormal(내구도-1) / 윈도우 초과 or 조기 Chamjil → Idle 리셋(체력 리셋 §3-2)
public void BeginApproach();       // Cast가 호출
public void OnChamjil();           // FishingRod.Chamjil이 위임
```

### 선행 산출물 의존성
- `Fish`/`FishState`/`SetState` — step-02
- `RodData`, `FishingTuning.biteWindowSeconds`, `FishInstance` — step-01

### 제약
- **미끼 차감은 입질(Bite) 진입 시 1회**(§4). 내구도 차감은 **챔질 성공(Fight 진입) 시 1회**(§4). 이중 차감 금지.
- 챔질 실패·윈도우 초과 → **Idle 복귀 + 체력 리셋**(§3-2 통일 규칙). 실루엣 소멸 금지(재도전 가능).
- 입질 윈도우는 전 어종 고정(§2) — `FishingTuning.biteWindowSeconds`.
- 타이머는 코루틴 또는 Update 누산(프레임 대기는 코루틴 권장, scripts.md). 90Hz 경량.
- 미끼 부족 시 캐스팅/입질 진행 불가(로그 + 조기 종료).

### 완료 판정
- [ ] `Grep "public void Cast" Assets/Scripts/Fishing/FishingRod.cs` — 신 시그니처(구 `Cast(FishingSpot)` 아님)
- [ ] Idle→Approach→Bite→(Chamjil)→FightNormal 전이가 §3-1과 일치(코드 확인)
- [ ] 미끼 -1(Bite 진입), 내구도 -1(Fight 진입) 시점 코드 확인
- [ ] 컴파일 통과 + ClaudeBridge 플레이 스모크: 캐스팅→입질→챔질 시 예외 0 (임시로 코드/디버그 버튼에서 Cast/Chamjil 호출)

---

## 금지 사항
- 파이팅(텐션/체력/도망) 구현 금지 — step-04·05. 이 단계는 Fight-평상 **진입까지만**.
- VR 입력 바인딩 금지 — step-07. 여기서는 `Cast/Chamjil` public 메서드만(테스트는 임시 호출).
- FishingSpot 개편·구 코드 삭제 금지 — step-08. 구 `FishingRod`는 재작성하되, `FishingSpot`/하네스가 깨지면 임시 컴파일 가드만(정리는 후속).
