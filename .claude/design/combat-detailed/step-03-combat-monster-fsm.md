# Step 03: Combat — 몬스터 FSM (Idle / Chase / Return) + 오버레이 구조

- **영역:** `Combat` (`Monsters/`) + EditorTools 동반(곰 프리팹)
- **선행 단계:** step-01 (MonsterData), step-02 (DummyMonster를 대체하므로 — 병렬 시 대체 작업만 마지막에)
- **후행 단계:** step-05(어그로)가 타겟 조회 지점을, step-06(스킬)이 Attack/Exhausted 상태 슬롯을, step-07(애니)이 상태 전이 이벤트를 사용
- **스펙:** 지시서 3단계 (§4-4)

---

## 목적
몬스터 메인 FSM의 뼈대를 만든다 — 이번 단계는 **Idle / Chase / Return 세 상태만** (Attack/Exhausted는 step-06). 상태 패턴(상태 클래스 분리)으로 구현해 확장 대비하고, 메인 상태와 독립인 오버레이 레이어(`IOverlayState` + Death 실구현, Stunned는 구조만)를 잡는다. 피격은 `MonsterHealth`(IDamageable)로 받아 DummyMonster를 대체한다.

---

## 에이전트 실행 지침

`/task-start` 호출 후, [`전투_시스템_구현_지시서.md`](../../domain/gdd/전투_시스템_구현_지시서.md) **3단계** 본문 규칙을 그대로 구현한다.

### 생성/수정 파일
- `Assets/Scripts/Combat/Monsters/MonsterController.cs` — 생성 (`CampLantern.Combat.Monsters`)
- `Assets/Scripts/Combat/Monsters/IMonsterState.cs` — 생성 (+ 상태 클래스들 — 파일 분리는 에이전트 판단)
- `Assets/Scripts/Combat/Monsters/IOverlayState.cs` — 생성 (+ `DeathOverlay` 실구현, `StunnedOverlay`는 구조만)
- `Assets/Scripts/Combat/Monsters/MonsterHealth.cs` — 생성 (IDamageable — DummyMonster 대체)
- `Assets/Scripts/Combat/Monsters/DummyMonster.cs` — **삭제** (또는 대체 확인 후 제거)
- `Assets/Scripts/Editor/CombatMonsterPrefabFactory.cs` — 생성 (곰 로컬 프리팹)

### 핵심 심볼
```csharp
public interface IMonsterState
{
    void Enter(MonsterController owner);
    void Tick(MonsterController owner, float deltaTime);
    void Exit(MonsterController owner);   // step-06: Attack Exit에서 판정 무조건 Disable하는 안전장치 지점
}

/// <summary>메인 상태와 독립적으로 적용되는 오버레이 (Death 실구현, Stunned는 step-06).</summary>
public interface IOverlayState { /* 적용/해제/틱 — 시그니처는 에이전트 설계, Death·Stunned 둘 다 수용해야 함 */ }

public class MonsterController : MonoBehaviour
{
    public MonsterData Data { get; }               // [SerializeField] m_data
    public Vector3 SpawnPosition { get; }          // Return 목적지 — Awake에서 캡처
    public string CurrentStateName { get; }        // [SerializeField] 문자열 노출 (지시서 — 인스펙터 확인용)
    public Transform CurrentTarget { get; }        // 이번 단계: 씬 내 플레이어 탐색. step-05에서 AggroController 조회로 교체
    public void TransitionTo(IMonsterState next);
    public event Action<IMonsterState, IMonsterState> StateChanged;  // step-07 애니 드라이버 구독 지점
}

public class MonsterHealth : MonoBehaviour, IDamageable
{
    public int  MaxHp { get; }        // Data.baseHp 기준 — HP 스케일링은 step-09
    public int  CurrentHp { get; }
    public bool IsDamageable { get; } // Return 중 false (무적)
    public void ApplyDamage(in HitInfo hit);   // 약점 배율(weakpointMultiplier) 적용 지점
    public void ResetToFull();                 // Return 원위치 도달 시
    public event Action<HitInfo> Damaged;      // step-05 어그로 구독 (약점 여부 포함)
    public event Action Died;                  // HP 0 → DeathOverlay 발동
}
```

### 구현 규칙 (지시서 3단계 그대로)
- **상태 패턴 필수** — enum + switch 금지 (step-06 확장 대비, 지시서 명시).
- **Idle**: 활동 반경(roamRadius) 내 랜덤 지점 배회(idleMoveSpeed), 도착 시 잠시 대기 후 다음 지점.
- **감지**: 플레이어가 detectRadius 진입 → Chase (chaseMoveSpeed로 추적).
- **Return**: 타겟(가장 가까운 플레이어)이 chaseRadius 밖이면 즉시 진입. Return 중 **무적**(`IsDamageable=false` — ApplyDamage 전부 무시) + 어그로 무시. 원위치 도달 시 HP 전량 회복 후 Idle.
- **Death 오버레이**: HP 0 → 모든 상태 즉시 종료, 사망 처리. **박힌 화살 파괴 금지** 주석 명시 (step-08 회수 규칙 대비).
- **이동 방식**: NavMeshAgent 사용 여부는 에이전트 판단+근거 (전제: 사냥터 존이 작고 지형 단순). NavMesh를 쓰면 베이크는 step-04 씬 팩토리에서 처리(또는 수동 체크리스트).
- 플레이어 탐색: 이번 단계는 임시(태그/`PersistentPlayer` 탐색 등 — 에이전트 선택, step-05에서 어그로로 교체됨을 주석).

### EditorTools 동반 — `CombatMonsterPrefabFactory`
- 메뉴 `Tools > Make Assets > Combat Monster — Bear`: `Assets/Prefabs/Combat/CombatMonster_Bear.prefab` 생성 —
  루트(`MonsterController`+`MonsterHealth`+`MonsterData` 참조) + `Visual` 자식(=`Assets/BlinkAnimals/Bear/Prefabs/Bear_4.prefab` 중첩 인스턴스, Animator 포함) + 피격 볼륨 콜라이더 + 머리 약점 볼륨.
- 비주얼 스왑 방식·머티리얼 URP 업그레이드는 [`HuntTargetPrefabFactory.UpgradeBearArt`](../../../Assets/Scripts/Editor/HuntTargetPrefabFactory.cs) 재사용/참조 (`UpgradeMaterialToUrp`는 이미 곰 텍스처 4종 배선 로직 보유).
- **주의**: 네트워크 프리팹 `HuntTarget_Bear.prefab`은 건드리지 않는다. 이건 로컬 전투 전용 별도 프리팹이다.

### 선행 산출물 의존성
- `MonsterData` — step-01. `IDamageable`/`HitInfo` — step-01. (step-02의 MeleeWeapon으로 피격 검증.)

### 제약
- RULE-03: NavMeshAgent 미사용 시 직접 이동은 Transform/CharacterController 기반(물리 조작이면 FixedUpdate).
- 상태 Tick은 90Hz 경량 유지 — 배회 지점 샘플링 등 무거운 연산은 상태 진입 시 1회.
- rules/scripts.md: 이벤트 구독 해제, Awake 초기화.

### 완료 판정
- [ ] `Grep "class MonsterController" Assets/Scripts/Combat/Monsters/` + 상태 클래스 존재 확인 (enum+switch 아님)
- [ ] 컴파일 통과, DummyMonster 참조 잔재 0 (`Grep "DummyMonster" Assets/Scripts/`)
- [ ] (씬 확보 후) 배회 → 접근 시 추격 → 멀어지면 귀환(무적·HP 회복) 사이클 확인 — step-04에서 재현
- [ ] NavMesh 채택 여부와 근거 보고 + 수동 작업 체크리스트 (베이크, 스폰 지점 등)

---

## 금지 사항
- Attack/Exhausted 상태 선구현 금지 — step-06 몫. 상태 슬롯(전이 가능 구조)만 열어둔다.
- 어그로 규칙 선구현 금지 — 이번 단계 타겟팅은 "가장 가까운 플레이어" 임시 구현까지만.
- `HuntTarget_Bear.prefab`/`Hunting/` 코드 수정 금지.
