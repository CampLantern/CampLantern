# Step 06: Fishing — 후킹·획득 UI·보상

- **영역:** `Fishing` (`Assets/Scripts/Fishing/`)
- **선행 단계:** step-05 완료 (Hooked 도달 가능)
- **후행 단계:** step-07(VR 입력) — 낚아올림 입력을 여기 진입점에 바인딩

---

## 목적
파이팅의 마무리: **Hooked(초록 줄) → 낚아올림 → Caught → 획득 UI + 보상**. 획득 UI는 물고기 종류·길이·무게·획득 XP·코인을 표시(§2-5). 보상은 임시 1 고정(§7-6, `TODO(FORMULA)`). Fishing은 Core.Wallet/Inventory를 직접 만지지 않고 **`FishCaught` 이벤트**로만 알린다(기존 계약 유지).

---

## 에이전트 실행 지침

`/task-start` 후 §2-4/2-5·§7-6 기준. 획득 UI는 기존 VR UI 토대(`CampLantern.UI.VRUIPanel`)를 재사용.

### 생성/수정 파일
- `Assets/Scripts/Fishing/Fish.cs` — 수정 (Hooked/Caught 처리 + 낚아올림 진입점)
- `Assets/Scripts/Fishing/FishingRod.cs` — 수정 (`FishCaught` 이벤트 재정의: 개체+보상 전달)
- `Assets/Scripts/Fishing/FishResultPanel.cs` — 생성 (획득 결과 표시 컴포넌트)
- `Assets/Scripts/Editor/FishResultPanelFactory.cs` — 생성 (프리팹 생성기, RULE-02) {TODO: verify VRUIFactory 패턴 재사용 가능}

### 핵심 심볼
```csharp
// Fish.cs
//   Hooked 진입: Line = Green (§2-4 후킹 직전)
//   낚아올림 입력 → Caught: 개체 제거, Caught 이벤트
public void Hook();   // 낚아올림 입력(step-07/디버그) 위임. Hooked 상태에서만 유효

// FishingRod.cs — 획득을 개체+보상으로 알린다(구 FishCaught(FishDef) 대체)
public struct FishReward { public int xp; public int coin; }
public event System.Action<FishInstance, FishReward> FishCaught;
//   Fish.Caught 수신 → reward = { FishingFormulas.RewardXp(inst), FishingFormulas.RewardCoin(inst) } → FishCaught 발화

// FishResultPanel.cs — 종류/길이/무게/XP/코인 표시 + 확인 버튼으로 숨김(§2-5)
public class FishResultPanel : MonoBehaviour
{
    public void Show(FishInstance fish, FishReward reward);  // 소유자가 Push(rules/scripts.md)
    public void Hide();
    public event System.Action Confirmed;   // 확인 버튼
}
```

### 선행 산출물 의존성
- `FishingFormulas.RewardXp/RewardCoin` — step-01 (스텁 `return 1`, `TODO(FORMULA)`)
- `FishInstance`(Length/Weight/Species) — step-01
- `FishState.Hooked/Caught` — step-02
- `CampLantern.UI.VRUIPanel`/`VRUIButton` — 기존 (참조만)

### 제약
- **보상 산출은 스텁**(§7-6 XP+1/코인+1). `FishingFormulas.RewardXp/Coin` 경유, `TODO(FORMULA)` 유지.
- **코인/인벤토리 적용은 이 단계에서 하지 않는다** — `FishCaught` 이벤트만 발화. 적용은 하네스(step-09).
- 획득 UI는 표시 전용(rules/scripts.md UI 초기화 원칙: 소유자가 `Show`로 Push). 초기 비활성은 소유 매니저가 관리(자신의 Awake에서 SetActive(false) 금지).
- 프리팹 생성은 Editor 팩토리로(RULE-02). 폰트는 한글 TMP 폴백이 이미 걸려 있어 기본 폰트로 OK.
- Caught 시 개체 제거(§3-1). 획득 UI는 확인 버튼(`Confirmed`)으로 숨김.

### 완료 판정
- [ ] `Grep "event.*FishCaught" Assets/Scripts/Fishing/FishingRod.cs` — 신 시그니처(FishInstance, FishReward)
- [ ] Hooked=Green, 낚아올림→Caught→개체 제거·보상 전달 경로 확인
- [ ] `Grep "TODO(FORMULA)"` — RewardXp/Coin 스텁 유지 확인
- [ ] 컴파일 + 스모크: Hooked에서 Hook() 호출 시 결과 패널 Show + FishCaught 발화(로그) 확인

---

## 금지 사항
- Wallet/Inventory 직접 참조·적용 금지 — 이벤트로만. 적용은 step-09.
- 보상 산출식 구현 금지 — §9 미정, 스텁 유지.
- VR 낚아올림 입력 바인딩 금지 — step-07. 여기서는 `Hook()`/`Confirmed` 진입점만.
