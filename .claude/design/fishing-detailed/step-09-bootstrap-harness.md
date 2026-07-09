# Step 09: Bootstrap — FishingGroundHarness 통합 (최종)

- **영역:** `Bootstrap` (`Assets/Scripts/Bootstrap/`)
- **선행 단계:** step-08 완료 (스포너·신 API·심 정리)
- **후행 단계:** 없음 — 이 단계로 낚시 정교화 완결. **완료 시 TODO 3종 전체 목록 보고(전체 완료 조건).**

---

## 목적
`FishingGroundHarness`를 신 낚시 시스템으로 갈아탄다: 구 `Cast(spot)/Reel()` IMGUI 조작 → 신 플로우(캐스팅/챔질/릴링 홀드/스윙/낚아올림 디버그 버튼 + 상태·줄색·체력·텐션 표시), 구 `FishCaught(FishDef)` 구독 → 신 `FishCaught(FishInstance, FishReward)` 구독으로 교체하고 **보상 적용**(인벤토리+코인)을 배선한다. step-08까지 남겨둔 Obsolete 심을 최종 제거한다.

---

## 에이전트 실행 지침

`/task-start` 후 진행. 기존 하네스의 음성 UI·인벤토리 패널·세션 접속 부분은 **건드리지 않는다**.

### 수정 파일
- `Assets/Scripts/Bootstrap/FishingGroundHarness.cs` — 수정 (낚시 부분만 교체)
- `Assets/Scripts/Fishing/` — step-08에서 이월된 Obsolete 심·구 enum 최종 제거

### 핵심 배선
```csharp
// 1) 참조: m_rod(신 FishingRod), m_spot(신 FishingSpot), (선택) FishingRodInput, FishResultPanel
//    씬 배선은 RoomScenesFactory의 기존 SetObjectRef 패턴이 그대로 유효한지 확인 — 필드명이 바뀌면
//    RoomScenesFactory.CreateFishingGround의 배선도 같이 갱신(같은 커밋).

// 2) 획득 구독 (Awake에서 -= 후 +=, OnDestroy 해제):
m_rod.FishCaught += OnFishCaught;
void OnFishCaught(FishInstance fish, FishReward reward)
{
    // fishId → FishDef: m_registry.FindItem(fish.Species.fishId) — ContentRegistry의 실제 조회 메서드명은
    //   Assets/Scripts/Core/ContentRegistry.cs에서 확인 후 사용 {TODO: verify}
    // 인벤토리: m_state.Inventory.Add(fishDef)  → 기존 InventoryPanel이 Changed로 자동 갱신(아이콘 표시)
    // 코인: m_state.Wallet — 실제 API(Add/Earn 등)는 Assets/Scripts/Core/Wallet.cs에서 확인 후 사용 {TODO: verify}
    // XP: 저장처 없음(§9 XP 용처 미정) — 하네스 로컬 필드 누적 + IMGUI 표시만. // TODO(DATA): XP 시스템 미정
    // 결과 UI: FishResultPanel.Show(fish, reward) — 길이/무게/보상 표기(§2-5)
    m_state.Save(); // 기존 저장 흐름 유지
}

// 3) IMGUI 디버그(개발용, Quest 전 제거 대상 주석 유지):
//    상태 줄: 물고기 상태/줄색/체력/텐션/미끼/내구도 표시
//    버튼: [캐스팅(최근접)] [챔질] [릴링 홀드 토글] [스윙] [낚아올림]  — 전부 코어 API 호출
```

### 선행 산출물 의존성
- `FishingRod.FishCaught(FishInstance, FishReward)` — step-06
- `FishingSpot.TryGetNearestFish` — step-08
- `FishResultPanel` — step-06
- `ContentRegistry`/`PlayerState`(Wallet/Inventory) — 기존 Core (수정 금지, 조회 API는 파일에서 확인)

### 제약
- 하네스의 **낚시 외 부분(음성/음소거, 세션 접속, 로비 복귀, 인벤토리 패널 바인딩) 불변**.
- Core(`Wallet`/`Inventory`/`ContentRegistry`) 수정 금지 — 기존 API 그대로 사용. 메서드명은 추측 말고 파일에서 확인.
- 씬 재생성 없이도 동작해야 함: 기존 FishingGround.unity의 배선(m_rod/m_spot)이 신 컴포넌트로 자연 승계되는지 확인, 아니면 `RoomScenesFactory` 갱신 + **Wire/재배선 실행을 완료 절차에 포함**(ClaudeBridge로 직접 실행).
- 이벤트 구독 규칙(rules/scripts.md) 준수.

### 완료 판정
- [ ] `Grep "Obsolete" Assets/Scripts/Fishing/` — 심 0건(최종 제거)
- [ ] `Grep "FishCaught" Assets/Scripts/` — 구 시그니처(FishDef 단독) 참조 0건
- [ ] 컴파일 + ClaudeBridge 플레이 스모크(런북): FishingGround 플레이 → 실루엣 스폰 → IMGUI로
      캐스팅→챔질→릴링→(도망 절제)→후킹→낚아올림 → **인벤토리 패널에 물고기 아이콘 표시 + 코인 증가 + 결과 패널 표시**, 예외 0
- [ ] **전체 완료 보고**: `Grep "TODO(FORMULA)\|TODO(DATA)\|TODO(TUNING)" Assets/Scripts/Fishing/ Assets/Scripts/Bootstrap/` 로 3종 태그 전체 목록 수집·정리해 보고. 문서 수치·수식과 코드가 다른 부분이 있으면 이유 명시(구현 지침 '완료 조건').

---

## 금지 사항
- 낚시 외 시스템(요리·사냥·영지) 코드 수정 금지.
- XP 시스템·미끼 상점·수리대 구현 금지 — §9 미결. 스텁/로그까지만.
- P0Playground(P0Harness) 갱신은 이 단계 범위 밖 — 필요 시 별도 후속 작업으로 보고만.
