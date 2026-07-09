# Step 01: Combat — 데이터 스키마(SO) + 공용 계약

- **영역:** `Combat` (`Assets/Scripts/Combat/` 루트 + `Data/`) + EditorTools 동반(에셋 팩토리)
- **선행 단계:** 없음
- **후행 단계:** step-02~12 전부가 이 단계의 타입·계약을 참조
- **스펙:** 지시서 1단계 (§4-1, §4-5, §1-5)

---

## 목적
전투 전체의 **어휘**를 확정한다 — 몬스터/스킬/무기/화살/전역 밸런스 ScriptableObject 스키마와, 모든 데미지가 지나는 공용 계약(`IDamageable`/`HitInfo`). 테스트 에셋은 Editor 팩토리로 생성한다(RULE-02). 아직 동작(MonoBehaviour 로직)은 없다.

---

## 에이전트 실행 지침

`/task-start` 호출 후, [`전투_시스템_구현_지시서.md`](../../domain/gdd/전투_시스템_구현_지시서.md) **1단계** 본문을 열어 필드 구성을 그대로 옮긴다.

### 생성 파일
- `Assets/Scripts/Combat/HitInfo.cs` — 생성 (`CampLantern.Combat`)
- `Assets/Scripts/Combat/IDamageable.cs` — 생성
- `Assets/Scripts/Combat/WeaponKind.cs` — 생성 (홀스터 슬롯 검사용 enum — step-10)
- `Assets/Scripts/Combat/Data/MonsterData.cs` — 생성 (`CampLantern.Combat.Data`)
- `Assets/Scripts/Combat/Data/SkillItem.cs` — 생성 (SkillType/RangedSubtype enum 포함)
- `Assets/Scripts/Combat/Data/MeleeWeaponData.cs` — 생성
- `Assets/Scripts/Combat/Data/BowData.cs` — 생성
- `Assets/Scripts/Combat/Data/ArrowData.cs` — 생성
- `Assets/Scripts/Combat/Data/CombatBalanceData.cs` — 생성
- `Assets/Scripts/Editor/CombatDataFactory.cs` — 생성 (`CampLantern.EditorTools`, 테스트 에셋 메뉴)

### 핵심 심볼 (시그니처)
```csharp
// HitInfo.cs — 약점 배율은 수신 측이 적용하므로 BaseDamage는 배율 미적용 값 (README 아키텍처 결정)
public struct HitInfo
{
    public int        BaseDamage;   // 무기 산출 기본 데미지 (약점 배율 미적용)
    public bool       IsWeakpoint;  // 약점(머리) 볼륨 적중 여부 — 어그로(step-05)가 소비
    public float      Speed;        // 실측 타격/착탄 속도 (로그·이펙트·햅틱용)
    public Vector3    Point;        // 접촉 지점 (월드)
    public GameObject Attacker;     // 공격 주체 루트 — step-12에서 PlayerRef 직렬화로 확장
}

// IDamageable.cs — 데미지 적용 단일 지점 (지시서 규칙 5, step-12 네트워크 분리 경계)
public interface IDamageable
{
    bool IsDamageable { get; }          // false = Return 무적 등 — 공격 측은 판정 스킵 가능
    void ApplyDamage(in HitInfo hit);
}

public enum WeaponKind { Sword, Spear, Bow }

// MonsterData.cs — §4-1 + 어그로 필드(§4-3, step-05에서 사용 — 스키마는 지금 확정)
[CreateAssetMenu(menuName = "CampLantern/Combat/Monster", fileName = "Monster_")]
public class MonsterData : ScriptableObject
{
    public int   baseHp;                          // hp 기준값 (HP 스케일링의 기준 — §4-2)
    public float roamRadius;                      // 활동 반경
    public float detectRadius;                    // 감지 반경
    public float chaseRadius;                     // 추격 반경
    public float idleMoveSpeed;
    public float chaseMoveSpeed;
    public float weakpointMultiplier = 1.5f;      // 약점 배율
    public float aggroHysteresisMeters = 2f;      // TODO(TUNING) — 규칙 2
    public float weakpointLockSeconds  = 5f;      // TODO(TUNING) — 규칙 3
    public float weakpointDecaySeconds = 1.5f;    // TODO(TUNING) — 규칙 4
    public List<SkillItem> skills;
}

// SkillItem.cs — [Serializable] 임베디드 flat 클래스.
// SO 분리 대신 임베디드: P0 몬스터 수가 적고 몬스터별 독립 튜닝이 기본.
// 타입별 서브클래스+SerializeReference 대신 flat: 직렬화 함정 회피(knowledge/unity-scripting-gotchas.md).
// 이견 시 아키텍트 보고 후 계획 갱신.
public enum SkillType     { Point, SelfAoe, Melee, Ranged }
public enum RangedSubtype { Projectile, Beam }

[Serializable]
public class SkillItem
{
    public string    displayName;
    public SkillType skillType;
    public int       damage;
    public float     cooldown;
    public float     recoverTime;
    public float     animDuration;                 // 시전 시간 — step-07에서 클립 길이로 동기화
    [Range(0f, 1f)] public float hitWindowStart;   // animDuration 대비 정규화 비율
    [Range(0f, 1f)] public float hitWindowEnd;     // Melee만 start<end 구간, 나머지는 start==end 단일 시점
    public float     minEngage;
    public float     weight = 1f;                  // 미사용 — 균등 랜덤 유지 (지시서 5단계 명시)
    [Header("Point")]   public float maxCastRange;
    [Header("Point/SelfAoe")] public float aoeRadius;
    [Header("Melee")]   public float range; public float arcAngle;   // 부채꼴 각도(deg)
    [Header("Ranged")]  public RangedSubtype rangedSubtype;
    public int projectileCount; public float projectileSpeed;        // Projectile
    public float castHeight; public float beamWidth;                 // Beam(레이저)
    [Header("미사용 - 추후")] public string statusEffectId;           // TODO(SPEC): 상태이상 부여
}

// MeleeWeaponData.cs — §1-3
[CreateAssetMenu(menuName = "CampLantern/Combat/Melee Weapon", fileName = "Weapon_")]
public class MeleeWeaponData : ScriptableObject
{
    public WeaponKind kind = WeaponKind.Sword;
    public float damageCoeff;          // 데미지 계수
    public float damageConst = 0.25f;  // 지시서의 ×0.25f 상수 — 데이터화 명시 요구
    public float vMin; public float vMax;
    public int   maxDurability;
    public int   repairCost = 0;       // TODO(SPEC): 수리 단가 미정 — 임시 0
}

// BowData.cs / ArrowData.cs — §1-5
[CreateAssetMenu(menuName = "CampLantern/Combat/Bow", fileName = "Bow_")]
public class BowData : ScriptableObject
{
    public float elasticity;       // E (최대값) — v0 = E×d − c×W
    public int   maxDurability;    // 발사 1회당 -1
}

[CreateAssetMenu(menuName = "CampLantern/Combat/Arrow", fileName = "Arrow_")]
public class ArrowData : ScriptableObject
{
    public float weight;           // W
    public int   price = 1;        // TODO(SPEC): 가격 미정 — 임시 1
}

// CombatBalanceData.cs — 전역 밸런스 상수 (지시서 6·7단계에서 소비 — 스키마는 지금 확정)
[CreateAssetMenu(menuName = "CampLantern/Combat/Balance", fileName = "CombatBalance")]
public class CombatBalanceData : ScriptableObject
{
    [Header("활/화살 (§1-4)")]
    public float bowDrawConstC;             // v0 식의 c
    public float arrowDamageK;              // clamp(v,vMin,vMax) × K × (1 + b×W)
    public float arrowWeightBonusB;
    public float arrowVMin; public float arrowVMax;
    public int   maxArrows = 30;
    public float arrowRetrieveRadius;       // 회수 상호작용 반경
    [Header("HP 스케일링 (§4-2)")]
    public float hpScalePerExtraPlayer = 0.5f;
    [Header("다운/소생 (§2)")]
    public float reviveHoldSeconds = 3f;
    [Range(0f, 1f)] public float reviveHpRatio = 0.5f;
}
```

### EditorTools 동반 — `CombatDataFactory`
- 메뉴 `Tools > Make Assets > Combat Data (Create All)` — `Assets/Data/Combat/`에 테스트 에셋 임시값 1개씩:
  `Monster_Bear_Test.asset`(**근거리 Melee Skill1 하나만 보유** — 지시서 명시), `Weapon_Sword.asset`, `Weapon_Spear.asset`, `Bow_Wooden.asset`, `Arrow_Basic.asset`, `CombatBalance.asset`.
- 기존 존재 시 스킵+경고 (WeaponPrefabFactory `Skip` 패턴). 폴더 생성은 `AssetDatabase.CreateFolder`.

### 선행 산출물 의존성
- 없음.

### 제약
- **Id/primary key 필드 금지** (지시서 명시 — 정식 스키마 때 일괄 부여). ContentRegistry 등록도 하지 않는다.
- 수치 임시값은 SO 기본값 + `// TODO(TUNING)`, 스펙 미정은 `// TODO(SPEC)`.
- RULE-01: asmdef 신설 금지. RULE-02: `.asset`은 팩토리로만 생성.

### 완료 판정
- [ ] `Grep "interface IDamageable" Assets/Scripts/Combat/` 등 10개 파일 정의 확인
- [ ] Unity 컴파일 통과 (ClaudeBridge — `error CS` 0)
- [ ] 팩토리 실행 후 `Assets/Data/Combat/` 에셋 6개 존재
- [ ] 수동 확인(체크리스트로 보고): 인스펙터에서 SkillItem 필드가 Header 그룹별로 보이는지

---

## 금지 사항
- MonoBehaviour·동작 로직 작성 금지 — 이 단계는 데이터·계약까지만.
- 필드명/시그니처를 임의로 바꾸지 않는다. 후속 11개 단계가 전부 이 어휘를 참조한다.
