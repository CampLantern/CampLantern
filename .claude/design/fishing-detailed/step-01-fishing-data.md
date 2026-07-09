# Step 01: Fishing — 데이터·수식 골격

- **영역:** `Fishing` (`Assets/Scripts/Fishing/`)
- **선행 단계:** 없음
- **후행 단계:** step-02~09 전부가 이 단계의 타입·수식을 참조

---

## 목적
정교화 낚시의 **데이터 모델과 수식 골격**을 만든다. 아직 동작(MonoBehaviour)은 없고, 어종/낚싯대/개체 데이터 타입과 §7 수식을 담은 static 클래스, 튜닝 값 홀더만 만든다. 이후 모든 단계가 이 어휘 위에서 동작한다. 스펙 §5(스키마)·§5-3(공유 품질 롤)·§7(수식)을 그대로 옮긴다.

---

## 에이전트 실행 지침

`/task-start` 호출 후, [`낚시_시스템_구현_지시서.md`](../../domain/gdd/낚시_시스템_구현_지시서.md)의 §5·§7·구현 지침을 열어 수치를 그대로 옮긴다.

### 생성 파일
- `Assets/Scripts/Fishing/FishSpeciesData.cs` — 생성 (어종 정적 스탯, plain C#, `TODO(DATA)`)
- `Assets/Scripts/Fishing/RodData.cs` — 생성 (낚싯대 스탯, plain C#, `TODO(DATA)`)
- `Assets/Scripts/Fishing/FishInstance.cs` — 생성 (캐스팅마다 롤되는 개체 스탯)
- `Assets/Scripts/Fishing/FishingFormulas.cs` — 생성 (§7 수식 static)
- `Assets/Scripts/Fishing/FishingTuning.cs` — 생성 (`[SerializeField]` 튜닝 홀더, `TODO(TUNING)`)

### 핵심 심볼 (시그니처)
```csharp
// FishSpeciesData.cs — §5-1. plain C#. // TODO(DATA): 실제 데이터 시스템(ScriptableObject)으로 교체
[System.Serializable]
public class FishSpeciesData
{
    public string fishId;
    public float power;                         // 도망 빈도·지속·텐션감소율
    public float sizeMin, sizeMax;
    public float lengthMin, lengthMax;
    public float weightMin, weightMax;
}

// 어종 3종 하드코딩 (소형 순함 / 중형 / 대형 힘셈). static 배열 or 제공 메서드.
public static class FishSpeciesTable
{
    public static readonly FishSpeciesData[] Species; // 3종. // TODO(DATA)
    public const float PowerMax = 20f;                // §7-3 힘Max (도망 간격 lerp 분모). // TODO(TUNING)
}

// RodData.cs — §5-2. // TODO(DATA)
[System.Serializable]
public class RodData
{
    public string rodId;
    public float power;        // 체력 감소 속도
    public int durability;     // 챔질 성공마다 -1, 0이면 수리 필요
    public float length;       // 캐스팅 사거리
    public float tension;      // 시도마다 초기화되는 줄 내구도 풀 최대
}

// FishInstance.cs — §5-3 공유 품질 롤. 캐스팅 시 어종+롤 t로 생성.
public class FishInstance
{
    public FishSpeciesData Species { get; }
    public float T { get; }         // 품질 롤 (0~1) — 보상 대물 보너스에 재사용
    public float Size { get; }      // lerp(sizeMin,sizeMax,t)
    public float Length { get; }    // lerp(lengthMin,lengthMax,t)
    public float Weight { get; }    // lerp(weightMin,weightMax,t)
    public float MaxHealth { get; } // 크기*0.1 + 길이*0.1 (§5-3, §7-1)
    public static FishInstance Roll(FishSpeciesData species, float t); // t = Random.value
}

// FishingFormulas.cs — §7. 확정 수식은 구현, 미정만 스텁.
public static class FishingFormulas
{
    // §7-1 확정
    public static float HealthDecayPerSecond(RodData rod, float effectivePower);   // = rod.power (흰색+홀드). effectivePower는 물고기힘(디버프 반영)
    // §7-2 확정: 도망(빨강)+홀드 시 텐션 감소량/초 = 물고기힘 * 0.25
    public static float TensionDecayPerSecond(float effectiveFishPower, bool isShake); // *1.5 if 비늘털이(§7-5)
    // §7-3 확정 + 소프트락: lerp(4,1, power/PowerMax) + rand(-0.3,0.3)
    public static float NextEscapeInterval(float effectiveFishPower, float powerMax);
    // §7-4 제안: clamp(rand(1,3) * (0.8 + 0.02*power), 1, 3)
    public static float EscapeDuration(float effectiveFishPower);
    // §7-5 제안: 20% (or 10% + power*1%) — 옵션은 튜닝
    public static float ShakeChance(float effectiveFishPower, FishingTuning tuning);

    // 별도 작성 예정(§9) — 스텁
    public static int RewardXp(FishInstance fish)  { /* TODO(FORMULA): XP 산출식 미정 */ return 1; }
    public static int RewardCoin(FishInstance fish) { /* TODO(FORMULA): 코인 산출식 미정 */ return 1; }
    public static int BaitPrice()  { /* TODO(FORMULA): 미끼 가격 미정 */ return 1; }
    public static int RepairPrice(int durability) { /* TODO(FORMULA): 수리 가격 미정 */ return 1; }
}

// FishingTuning.cs — [SerializeField] 노출용. Fish/FishingRod가 참조. // TODO(TUNING)
[System.Serializable]
public class FishingTuning
{
    [Tooltip("입질 판정 윈도우(초) — 전 어종 고정")] public float biteWindowSeconds = 1.0f;      // §9 권장 0.8~1.2
    [Tooltip("비늘털이 발동 확률(0~1)")]         public float shakeChance = 0.2f;               // §7-5
    [Tooltip("비늘털이 힘 비례 옵션 사용")]        public bool shakeChanceByPower = false;        // §7-5 옵션
    [Tooltip("비늘털이 성공 디버프 배율")]         public float shakeDebuffMul = 0.8f;            // §7-5 -20%
    [Tooltip("비늘털이 디버프 지속(초)")]          public float shakeDebuffSeconds = 1.5f;
    [Tooltip("비늘털이 체공(스윙 윈도우, 초)")]    public float shakeAirborneSeconds = 1.0f;
    [Tooltip("텐션 감소 계수")]                   public float tensionDecayCoeff = 0.25f;        // §7-2
    // 스윙 감지 임계값은 step-07 VR 입력에서 사용 — 여기 함께 선언
    [Tooltip("스윙 최소 각속도(deg/s)")]          public float swingMinAngularSpeed = 180f;      // TODO(TUNING): VR 실기
    [Tooltip("스윙 방향 전환 최소 횟수")]          public int   swingMinDirChanges = 1;
}
```

### 선행 산출물 의존성
- 없음. (단 `FishInstance.Roll`은 `UnityEngine.Mathf.Lerp`/`Random` 사용.)

### 제약
- **RULE-01**: `[InitializeOnLoad]`·asmdef 신설 금지. 순수 C#/`static`만.
- 수치는 §5·§7에서 **그대로** 옮긴다. `PowerMax`(§7-3 분모)는 어종 최대 힘(대형 어종 power)에 맞춘다 — 하드코딩값에 `TODO(TUNING)`.
- 어종 3종은 §구현지침 [2] 예시대로: 순한 소형 / 중간 / 힘센 대형. 각 스탯에 `TODO(DATA)`.
- §7-4·§7-5는 "제안 계수"이므로 값은 그대로 쓰되 `TODO(TUNING)`로 표기(FishingTuning 경유 or 주석).

### 완료 판정
- [ ] `Grep "class FishSpeciesData" Assets/Scripts/Fishing/` 등 5개 타입 정의 확인
- [ ] `Grep "TODO(FORMULA)" Assets/Scripts/Fishing/FishingFormulas.cs` — RewardXp/Coin/BaitPrice/RepairPrice 4개 스텁 확인
- [ ] Unity 컴파일 통과 (ClaudeBridge `Asset.Refresh` → Editor.log `error CS` 0)
- [ ] `FishInstance.Roll`이 크기/길이/무게에 **동일 t**를 쓰는지(§5-3 공유 롤) 코드 확인

---

## 금지 사항
- MonoBehaviour·동작 로직 작성 금지 — 이 단계는 데이터·수식·튜닝 홀더까지만.
- §7 확정 수식(체력·텐션·도망간격·지속)을 `return 1` 스텁으로 두지 말 것 — **구현**한다. 스텁은 §9 미정(보상/가격) 4개뿐.
- 어종/낚싯대 데이터를 ScriptableObject(.asset)로 만들지 말 것(RULE-02·이 단계는 하드코딩). 데이터 시스템 이관은 후속.
