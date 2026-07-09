# Step 05: Fishing — 도망 + 비늘털이 스윙 QTE

- **영역:** `Fishing` (`Assets/Scripts/Fishing/`)
- **선행 단계:** step-04 완료 (Fight-평상)
- **후행 단계:** step-06(후킹·획득)

---

## 목적
파이팅의 위험 축을 구현한다: 도망 간격 타이머(§7-3)로 **Fight-도망(빨강)** 또는 **Fight-비늘털이(점프 QTE)** 발동, 도망 중 릴링 시 텐션 감소(§7-2), 텐션 0 → 줄 끊김(실패 리셋 §3-2), 비늘털이 스윙 성공 시 힘 -20% 디버프(§7-5). 이 단계로 "빨간색일 때 참는 절제" 핵심 긴장이 완성된다.

---

## 에이전트 실행 지침

`/task-start` 후 §7-2·§7-3·§7-4·§7-5(비늘털이) 전문을 그대로 옮긴다.

### 수정 파일
- `Assets/Scripts/Fishing/Fish.cs` — 수정 (FightEscape/FightShake + 도망 간격 타이머 + 디버프)

### 핵심 심볼
```csharp
// Fish.cs — Fight-평상에서 도망 간격 타이머 관리(§7-3, 간격 기산점 = 이전 도망 종료 시점)
//  타이머 만료:
//     shakeChance(§7-5) 판정 → 성공 시 FightShake, 아니면 FightEscape. (직전이 비늘털이면 반드시 일반 도망, §7-5 연속 방지)
//
//  FightEscape (빨강, §7-2/7-3/7-4):
//     Line = Red, 지속 = FishingFormulas.EscapeDuration(EffectivePower)
//     rod.Reeling 이면 Tension -= FishingFormulas.TensionDecayPerSecond(EffectivePower, isShake:false) * Δt
//     Tension <= 0 → 줄 끊김 → 실패 리셋(BreakLine): Idle 복귀 + Health 리셋(§3-2)
//     지속 종료 → FightNormal (다음 도망 간격 재산정)
//
//  FightShake (점프 QTE, §7-5):
//     체공 shakeAirborneSeconds 동안 스윙 판정 윈도우. rod.Reeling 이면 Tension -= decay * 1.5 (isShake:true)
//     체공 중 FishingRod.SwingDetected(step-07) 수신 시 성공 → 착수 시 디버프(유효힘 *0.8, shakeDebuffSeconds) → FightNormal
//     스윙 실패 → 디버프 없이 FightNormal (페널티 없음)
//     Tension <= 0 → 줄 끊김
private float EffectivePower { get; }  // Species.power * (디버프 활성 시 shakeDebuffMul). 디버프 타이머로 관리
public void OnSwing();                 // step-07 입력 어댑터가 체공 중 호출 → 스윙 성공 등록
private void BreakLine();              // 텐션 0: Idle + Health 리셋(§3-2)
```

### 선행 산출물 의존성
- `FishingFormulas.NextEscapeInterval/EscapeDuration/TensionDecayPerSecond/ShakeChance` — step-01 (§7-2~7-5)
- `FishingTuning`(shakeChance/shakeDebuffMul/shakeDebuffSeconds/shakeAirborneSeconds/tensionDecayCoeff) — step-01
- `FishState.FightEscape/FightShake`, `Fish.Tension` — step-02/04

### 제약
- **텐션 감소는 도망(빨강)/비늘털이 + 트리거 홀드 중에만**(§7-2). 흰색 릴링·빨강 릴 정지 시 감소 없음. 비늘털이 홀드는 ×1.5(§7-5).
- 도망 간격 기산점 = **이전 도망 종료 시점**(§7-3 소프트락 방지 — 흰색 최소 ~1초 보장).
- **힘 -20% 디버프는 EffectivePower를 참조하는 모든 수식**(텐션감소·도망간격·도망지속)에 적용(§7-5). 유효힘 하나로 일원화.
- 비늘털이 직후 도망은 반드시 일반 도망(§7-5 연속 방지 플래그).
- 스윙 실패엔 페널티 없음(§7-5). `OnSwing`은 FightShake 체공 중에만 유효.
- 줄 끊김·챔질 실패 모두 §3-2 통일 리셋(Idle + 체력 리셋, 실루엣 유지).

### 완료 판정
- [ ] `Grep "NextEscapeInterval\|EscapeDuration\|TensionDecayPerSecond" Assets/Scripts/Fishing/Fish.cs`
- [ ] 도망 간격 기산점이 "이전 도망 종료 시점"인지 확인(§7-3)
- [ ] EffectivePower 하나에 디버프가 반영되고 세 수식이 그걸 참조하는지 확인(§7-5)
- [ ] 텐션 0 → BreakLine → Idle+체력리셋(§3-2) 경로 확인
- [ ] 컴파일 + 스모크: FightEscape에서 홀드 유지 시 Tension 감소→끊김, 절제 시 안 끊김(디버그 값)

---

## 금지 사항
- 스윙 **입력 감지**(각속도/방향전환) 구현 금지 — step-07. 여기서는 `OnSwing()` 수신 훅만.
- 후킹·획득·UI 금지 — step-06.
