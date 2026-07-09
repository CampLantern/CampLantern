# Step 08: Fishing — FishingSpot 개편(스포너) + 구 코드 정리

- **영역:** `Fishing` (`Assets/Scripts/Fishing/`)
- **선행 단계:** step-07 완료 (코어+입력 전부 존재)
- **후행 단계:** step-09에서 하네스가 이 스포너/신 API로 갈아탐

---

## 목적
`FishingSpot`을 "어종 추첨기"에서 **물고기 스포너/관리자**로 개편한다: 스폿 안에 실루엣 물고기(`Fish`)를 여러 마리 스폰·유영시키고(§3-1 Idle), 캐스팅 타겟 질의를 제공하며, 잡힌 자리는 재스폰한다. 동시에 신 시스템과 기존 자산(FishDef/에디터 팩토리)의 **연결 고리**를 확정하고, step-03에서 남긴 임시 컴파일 가드/구 코드 잔재를 정리한다.

---

## 에이전트 실행 지침

`/task-start` 후 §3-1(Idle: 원래 위치 기준 자유 유영, 실루엣 표시)·§5-3(개체 롤) 기준.

### 수정 파일
- `Assets/Scripts/Fishing/FishingSpot.cs` — **개편** (스포너화)
- `Assets/Scripts/Fishing/FishingRodInput.cs` — 수정 (타겟 획득 스텁 → 스폿 질의로 교체)
- `Assets/Scripts/Fishing/FishSpeciesTable.cs`(step-01 산출) — 수정 (fishId ↔ FishDef.Id 정렬, 아래 참조)

### 핵심 심볼
```csharp
public class FishingSpot : MonoBehaviour
{
    [SerializeField] private FishDef[] m_fishTable;      // 유지! — 에디터 팩토리 배선 호환 (아래 '연결 고리')
    [SerializeField] private int m_fishCount = 3;        // 동시 실루엣 수 // TODO(TUNING)
    [SerializeField] private float m_radius = 4f;        // 유영 반경 // TODO(TUNING)
    [SerializeField] private FishingTuning m_tuning;

    // 시작 시 m_fishCount 마리 스폰: 어종은 m_fishTable→FishSpeciesTable 매핑에서 추첨,
    // FishInstance.Roll(species, Random.value)로 개체 생성 → Fish.Initialize.
    // 실루엣 비주얼은 런타임 프리미티브(납작 스피어 등, 스케일 = Instance.Size 반영 §5-1) —
    // 프리팹 없이 코드 생성이라 RULE-02 무관.
    public bool TryGetNearestFish(Vector3 point, float maxRange, out Fish fish);  // 캐스팅 타겟(사거리 = rod.length 게이트 §5-2)
    // Fish.Caught 구독 → 해당 개체 파괴 + 재스폰(지연 후). 구독 해제 OnDestroy.
}
```

### 연결 고리 (중요 — 신 시스템 ↔ 기존 자산)
- **`FishSpeciesData.fishId`는 기존 `FishDef.Id`와 일치시킨다**: `fish_crucian`(순한 소형) / `fish_trout`(중형) / `fish_golden_carp`(힘센 대형). 이러면:
  - 획득 시 `fishId`→`ContentRegistry` 조회로 **기존 FishDef(아이콘·SellPrice 포함)를 그대로 인벤토리에 추가** 가능(step-09).
  - `m_fishTable`(FishDef[]) 배선을 유지해 **기존 에디터 팩토리(P0PlaySceneFactory·RoomScenesFactory의 `SetObjectArray(spot, "m_fishTable", ...)`)가 깨지지 않는다.**
  - step-01에서 fishId를 다르게 지었다면 이 단계에서 위 3개 Id로 정정한다.
- 구 `FishDef.BiteWindowSeconds/MinWait/MaxWaitSeconds`는 신 시스템에서 미사용(입질 윈도우는 전 어종 고정 §2) — 필드는 남겨두되(다른 참조·에셋 호환) 신 코드에서 읽지 않는다. 제거는 후속 정리로 미룬다.

### 구 코드 정리
- `FishingSpot.PickRandomFish()` 제거(스포너 API로 대체).
- step-03에서 남긴 임시 컴파일 가드(구 `Cast(FishingSpot)`/`Reel()` 흔적)를 정리하되, **하네스 컴파일이 깨지면 안 됨** — 하네스가 아직 구 API를 부르면 `[System.Obsolete]` 최소 심(내부에서 no-op+경고)으로 유지하고 step-09에서 최종 제거를 명시 주석으로 남긴다.
- 구 `FishingState` enum(Idle/Casting/Waiting/Biting/Caught/Missed)이 어디서도 안 쓰이면 제거, 쓰이면 심과 함께 step-09로 이월.

### 선행 산출물 의존성
- `Fish.Initialize/Caught`, `FishInstance.Roll`, `FishSpeciesTable` — step-01/02
- `FishingRodInput`의 타겟 스텁 — step-07 (여기서 교체)

### 제약
- 스폰은 **런타임 코드 생성**(프리팹 금지 아님이지만 불필요) — RULE-02 회피 겸 단순화.
- 유영은 가벼운 코사인/랜덤 워크 수준 — 물리 미사용(RULE-03 무관), 90Hz 경량. 실패 복귀 시 "원래 위치 기준"(§3-1) 유지.
- `TryGetNearestFish`는 Idle/Approach 상태 물고기만 반환(파이팅 중 개체 재타겟 금지).
- 재스폰 지연·마릿수는 `TODO(TUNING)`.

### 완료 판정
- [ ] `Grep "TryGetNearestFish" Assets/Scripts/Fishing/` — 스폿 구현 + 입력 어댑터 호출부 확인
- [ ] `Grep "PickRandomFish" Assets/Scripts/` — 0건(제거 확인)
- [ ] `FishSpeciesTable`의 fishId 3종이 `fish_crucian/fish_trout/fish_golden_carp`와 일치
- [ ] 컴파일 + 스모크: FishingGround 플레이 시 실루엣 m_fishCount마리 스폰·유영, 예외 0
- [ ] 기존 에디터 팩토리(m_fishTable 배선) 미파손 — `Grep "m_fishTable" Assets/Scripts/Editor/` 배선 코드가 그대로 유효

---

## 금지 사항
- 하네스(`Bootstrap/`) 수정 금지 — step-09. 컴파일 유지용 Obsolete 심까지만.
- FishDef 에셋(.asset)·에디터 팩토리 수정 금지 — 연결 고리는 코드(fishId 매핑) 쪽에서 맞춘다.
- 레어도 가중 추첨·날씨/시간 스폰 등 P1 기능 추가 금지 (mvp-scope).
