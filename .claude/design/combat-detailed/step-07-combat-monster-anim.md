# Step 07: Combat — 곰 애니메이션 드라이버 + animDuration 동기화

- **영역:** `Combat` (`Monsters/`)
- **선행 단계:** step-06 완료 필요 (모든 메인 상태·오버레이·스킬 이벤트 존재)
- **후행 단계:** 없음 (비주얼 계층 — 다른 시스템이 의존하지 않음)
- **스펙:** 지시서 3단계 "애니메이션" 언급 + 5단계 "클립 확정 후 animDuration 자동 동기화 헬퍼" (여기로 이월)

---

## 목적
곰 실제 아트의 Animator(`BearAnimator.controller`)를 FSM에 연결한다. **판정은 이미 데이터(hit window)로 완결**되어 있으므로 이 단계는 순수 비주얼 — 애니메이션이 없어도(플레이스홀더 몬스터) 시스템이 동작해야 하고, 있으면 상태에 맞는 클립이 재생된다. 스킬 `animDuration`을 매핑된 클립 길이로 자동 동기화하는 헬퍼를 만든다.

---

## 에이전트 실행 지침

`/task-start` 호출 후 수행.

### 생성/수정 파일
- `Assets/Scripts/Combat/Monsters/MonsterAnimationDriver.cs` — 생성 (`CampLantern.Combat.Monsters`)
- `Assets/Scripts/Editor/CombatMonsterPrefabFactory.cs` — 수정 (곰 프리팹에 드라이버 Ensure + 트리거 매핑 기본값 주입)

### 사용 가능한 Animator 파라미터 (BearAnimator.controller — 사전 조사 완료)
`Idle`, `Combat Idle`, `Run Forward`, `WalkForward`, `WalkBackward`, `Jump`, `Attack1`~`Attack8`, `Buff`,
`Get Hit Front/Back/Left/Right`, `Stunned Loop`, `Eat`, `Sit`, `Sleep`, `Death` (+ 방향 이동 변형들).
클립 원본: `Assets/BlinkAnimals/Bear/Animations/*.fbx`.

### 핵심 심볼
```csharp
/// <summary>
/// FSM/스킬/피격 이벤트 → Animator 트리거 매핑. Animator가 없거나 파라미터가 없으면 조용히 no-op
/// (플레이스홀더 프리미티브 몬스터 호환). 판정은 hit window(데이터)로만 — 애니메이션 이벤트 금지.
/// </summary>
public class MonsterAnimationDriver : MonoBehaviour
{
    [Serializable] public class TriggerMap { /* 상태/이벤트 식별 → 트리거 이름 문자열 */ }
    // 매핑 기본값 (곰): Idle→"Idle", Chase→"Run Forward", Return→"WalkForward",
    // Attack(스킬 인덱스별)→"Attack1".., 피격→"Get Hit Front", Stunned→"Stunned Loop", Death→"Death",
    // Exhausted→"Combat Idle" (탈진 전용 클립 부재 — 가장 근접한 대기 클립. // TODO(SPEC): 탈진 연출 미정)
}
```

### 구현 규칙
- `MonsterController.StateChanged`·스킬 시전 이벤트·`MonsterHealth.Damaged`/`Died` 구독 → 트리거 발화. 구독 해제 철저(rules/scripts.md).
- 파라미터 존재 검사 후 SetTrigger (없으면 no-op — `Animator.parameters` 캐싱, 매 호출 순회 금지).
- **animDuration 동기화 헬퍼**: 스킬 ↔ 클립 매핑이 있으면 클립 길이를 읽어 `SkillItem.animDuration`을 덮어쓴다. 런타임(Awake) 적용이 기본이되, SO 에셋 자체를 갱신하는 Editor 메뉴(`Tools > Make Assets > Combat — Sync Skill Anim Durations`)로 할지는 에이전트 판단+근거 (SO 런타임 변경은 에디터에서 에셋을 오염시키는 함정 주의 — knowledge/unity-scripting-gotchas.md).

### 선행 산출물 의존성
- `StateChanged`/상태 클래스 — step-03·06. 스킬 시전 이벤트 — step-06. `CombatMonster_Bear.prefab` — step-03.

### 제약
- **애니메이션 이벤트(AnimationEvent) 사용 금지** — 판정 타이밍은 전부 hit window 데이터 (지시서 명시).
- FSM 로직이 드라이버에 의존하지 않게(단방향: FSM → 드라이버). 드라이버 제거해도 전투 동작 불변.

### 완료 판정
- [ ] CombatSandbox에서 곰이 상태 전이에 맞춰 클립 재생 (배회=Idle/Walk, 추격=Run, 공격=Attack1, 피격=Get Hit, 사망=Death, 스턴=Stunned Loop)
- [ ] Animator를 제거한 프리미티브 몬스터에서도 오류 0 (no-op 확인)
- [ ] `Monster_Bear_Test`의 Skill1 animDuration이 매핑 클립 길이와 일치 (동기화 헬퍼 동작)
- [ ] 컴파일 통과 + 구독 해제 확인

---

## 금지 사항
- 판정·FSM 로직 수정 금지 — 이 단계는 비주얼 연결만.
- BearAnimator.controller·클립 에셋 직접 편집 금지 (RULE-02). 트리거 매핑은 코드/프리팹 배선으로만.
