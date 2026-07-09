# Step 06: Combat — 스킬 시스템 + Attack / Exhausted + Stunned 실구현

- **영역:** `Combat` (`Monsters/` 중심, `Player/PlayerHealth` 최소판 포함)
- **선행 단계:** step-03 (FSM 상태 슬롯), step-05 (CurrentTarget)
- **후행 단계:** step-07(애니 드라이버가 Attack/스킬 이벤트 구독), step-09(PlayerHealth 확장)
- **스펙:** 지시서 5단계 (§4-5 ~ §4-7)

---

## 목적
FSM에 **Attack**과 **Exhausted** 상태를 추가하고, 스킬 선택 규칙(§4-7)과 hit window 판정(§4-6)을 구현한다. 이번 단계 실동작은 **Melee 타입만** — Point/SelfAoe/Ranged는 골격+TODO. Stunned 오버레이를 실구현한다. 플레이어 피격은 임시 `PlayerHealth`(HP+로그)로 받는다.

---

## 에이전트 실행 지침

`/task-start` 호출 후, [`전투_시스템_구현_지시서.md`](../../domain/gdd/전투_시스템_구현_지시서.md) **5단계** 본문 규칙을 그대로 구현한다.

### 생성/수정 파일
- `Assets/Scripts/Combat/Monsters/` — Attack/Exhausted 상태 클래스 추가, 스킬 실행기(파일 구성은 에이전트 설계 — 예: `SkillRunner.cs`, `MeleeSkillHitCheck.cs`)
- `Assets/Scripts/Combat/Monsters/StunnedOverlay.cs` — step-03 구조를 실구현으로 승격
- `Assets/Scripts/Combat/Player/PlayerHealth.cs` — 생성 (`CampLantern.Combat.Player`, **최소판** — HP·피격 로그·`Damaged` 이벤트. 다운/소생은 step-09에서 확장 주석 명시)

### 구현 규칙 (지시서 그대로)
- **스킬 선택 (§4-7)**: Chase 중 매 판단 틱 —
  후보 = skills 중 (쿨타임 완료) AND (타겟 거리 ≥ minEngage) AND (Melee/Point는 사거리 내).
  후보 있음 → **균등 랜덤** 1개 시전(Attack 진입). 후보 없음 + 사거리 내 → Exhausted. 후보 없음 + 사거리 밖 → Chase 유지.
  **중복 방지 로직 금지. weight는 읽기만 하고 균등 랜덤 유지** (지시서 명시).
- **Attack**: `animDuration` 타이머 시전 → 종료 후 `recoverTime` 무방비(Recover) → 재판단. 애니메이션 없이도 타이머로 동작해야 함.
- **Exhausted**: 지속 = 가장 빨리 도는 스킬의 잔여 쿨타임(런타임 계산). 이동 정지, 피격 정상. 탈진 중 타겟이 추격 반경 이탈 → 탈진 끊고 Return.
- **hit window 판정 (§4-6)**: 판정 규약 준수(물리 콜백 금지). `EnableHitCheck()`/`DisableHitCheck()` 캡슐화, 활성 구간 매 틱 쿼리. Attack 타이머 경과 비율이 `hitWindowStart` 도달 시 Enable, `hitWindowEnd`에서 Disable. **애니메이션 이벤트 사용 금지.** Melee 기법은 전방 부채꼴 거리·각도 수학 판정 권장(에이전트 선택+근거).
  **안전장치**: Attack 상태 `Exit`에서 판정 무조건 Disable (Stunned 취소 시 판정 잔류 방지).
  시전당 플레이어별 최대 1회 피격. 플레이어 피격도 `IDamageable.ApplyDamage(HitInfo)` 경유(적용 단일 지점 유지).
- **Point/SelfAoe/Ranged**: 클래스 골격 + TODO 주석만 (단일 시점 판정·거리/경로 판정 예정 내용을 TODO에 명기).
- **Stunned 오버레이 실구현**: 메인 상태 타이머 일시정지, 진행 중 공격 취소(→Exit 안전장치 검증), 해제 시 Chase 복귀. **Exhausted 위에도 적용됨을 테스트로 확인** (에디터 컨텍스트 메뉴/하네스 버튼 등 트리거 수단 포함).

### 선행 산출물 의존성
- `SkillItem`(hitWindow/minEngage/타입별 필드) — step-01. `MonsterController.TransitionTo`/상태 인터페이스 — step-03. `AggroController.CurrentTarget` — step-05.

### 제약
- animDuration 클립 동기화 헬퍼는 **step-07로 이월** (지시서 5단계 내용이지만 곰 클립 매핑과 함께 처리) — 이번 단계는 임시값 타이머만.
- 판단 틱 주기는 SerializeField 노출(`// TODO(TUNING)`), 매 프레임 후보 정렬 금지.
- rules/scripts.md: PlayerHealth는 UI/매니저 초기화 순서 원칙 준수(스스로 외부 조회 금지).

### 완료 판정
- [ ] 스킬 1개 몬스터(Monster_Bear_Test)가 "추격 → 공격 → 탈진 → 추격" 리듬으로 동작, 탈진 중 프리딜 가능 (CombatSandbox)
- [ ] hit window 밖에서 피격 없음, Attack 중단(Stunned) 시 판정 잔류 없음 (Exit 안전장치 로그)
- [ ] 시전당 플레이어별 1회 피격 확인 (판정 창에 오래 서 있어도 1회)
- [ ] Stunned가 Exhausted 위에 적용되는 테스트 통과
- [ ] `Grep "class PlayerHealth" Assets/Scripts/Combat/Player/` + 컴파일 통과
- [ ] 수동 작업 체크리스트 보고 (공격 부위 기준점, 판정 파라미터 세팅 — 가능하면 CombatMonsterPrefabFactory에 Ensure 추가)

---

## 금지 사항
- Point/SelfAoe/Ranged 실동작 구현 금지 — 골격+TODO까지만.
- 스킬 선택에 가중치/중복 방지 로직 추가 금지 (지시서 명시 — weight는 필드만).
- 다운/소생/전멸 선작업 금지 — PlayerHealth는 HP+로그 최소판까지만.
