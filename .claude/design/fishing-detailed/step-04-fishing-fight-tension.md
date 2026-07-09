# Step 04: Fishing — 파이팅 텐션·체력 (Fight-평상)

- **영역:** `Fishing` (`Assets/Scripts/Fishing/`)
- **선행 단계:** step-03 완료 (FightNormal 진입)
- **후행 단계:** step-05(도망/비늘털이)

---

## 목적
**Fight-평상(흰색 줄)**의 코어 릴링을 구현한다: 트리거 홀드 릴링 시 체력 감소(§7-1), 체력 0 → Hooked. 텐션 풀 초기화(§7-2, 시도마다 덮어씀). 흰색 구간에서는 텐션이 줄지 않는다. 이 단계로 "도망만 없으면 반드시 잡히는" 최소 파이팅이 성립한다.

---

## 에이전트 실행 지침

`/task-start` 후 §7-1(체력)·§7-2(텐션 초기화)·§6 스탯 파이프라인 기준.

### 수정 파일
- `Assets/Scripts/Fishing/Fish.cs` — 수정 (FightNormal 처리)
- `Assets/Scripts/Fishing/FishingRod.cs` — 수정 (릴링 홀드 입력 상태)

### 핵심 심볼
```csharp
// FishingRod.cs — 릴링 홀드 상태(입력 어댑터가 세팅, step-07). 지금은 public 프로퍼티/메서드로.
public bool Reeling { get; private set; }
public void SetReeling(bool held);   // 트리거 홀드 on/off

// Fish.cs — FightNormal
//  진입: Tension = rod.tension (§7-2, 덮어쓰기), Line = White
//  Update/FixedUpdate: rod.Reeling && FightNormal 이면 Health -= FishingFormulas.HealthDecayPerSecond(rod, EffectivePower) * Δt
//  Health <= 0 → SetState(Hooked) + Hooked 이벤트 (줄색 Green은 step-06)
//  흰색 구간: 텐션 감소 없음(§7-2)
private float EffectivePower { get; }  // 물고기 힘 (비늘털이 디버프 반영 — step-05에서 확장). 지금은 Instance.Species.power
```

### 선행 산출물 의존성
- `FishingFormulas.HealthDecayPerSecond` — step-01 (§7-1: = rod.power)
- `FishState.FightNormal/Hooked`, `Fish.Health/Tension` — step-02
- `FishingRod.Rod`(RodData) — step-03

### 제약
- **체력 감소는 흰색 + 트리거 홀드 중에만**(§7-1). 그 외엔 감소 없음. 체력 자연 회복 없음(§1).
- 텐션 풀은 Fight 진입 시 `rod.tension`으로 **초기화(이월 없음, §7-2)**.
- `Health -= power * Δt`는 프레임 독립. Update에서 `Time.deltaTime` 사용(물리 아님 → RULE-03 무관, FixedUpdate 불필요). 90Hz 경량.
- `Hooked` 진입 시 이벤트만 발화, 줄색 Green·낚아올림 UI는 step-06.
- `EffectivePower`는 지금은 `Species.power`. step-05에서 디버프 반영 훅을 남겨둔다(주석).

### 완료 판정
- [ ] `Grep "HealthDecayPerSecond" Assets/Scripts/Fishing/Fish.cs` 호출 확인
- [ ] FightNormal 진입 시 `Tension = rod.tension` 코드 확인(§7-2)
- [ ] 흰색 구간 릴링 시 Health만 감소·Tension 불변 확인
- [ ] 컴파일 + 스모크: FightNormal에서 SetReeling(true) 유지 시 Health가 0까지 감소→Hooked 도달(디버그 값으로)

---

## 금지 사항
- 도망(빨간색)·비늘털이·텐션 감소 구현 금지 — step-05.
- 후킹 후 획득 처리·UI 금지 — step-06. 여기서는 `Hooked` 상태·이벤트 도달까지만.
