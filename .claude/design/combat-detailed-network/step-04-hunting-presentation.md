# Step 04: Hunting — 비권한 클라 표현 동기화 (상태→애니, 이동 보간)

- **영역:** `Hunting` (`Assets/Scripts/Hunting/`)
- **선행 단계:** step-01 (`NetStateName` 기록), step-02 (프리팹 — 검증 무대)
- **후행 단계:** step-05 검증
- **스펙:** 지시서 8단계 "상태를 동기화" 후반부 — 표현 계층

---

## 목적
비권한 클라에서 몬스터가 "살아 보이게" 한다 — FSM은 권한자 전용이므로, 비권한 클라는 `[Networked] NetStateName`의 변화를 감지해 `MonsterAnimationDriver`를 구동하고, 위치는 `NetworkTransform` 보간을 확인한다. 판정·로직은 일절 없음(순수 표현).

---

## 에이전트 실행 지침

`/task-start` 호출 후 수행.

### 생성/수정 파일
- `Assets/Scripts/Hunting/NetworkedHuntMonster.cs` — 수정: 비권한 표현 브리지 (Render + ChangeDetector)
- `Assets/Scripts/Combat/Monsters/MonsterAnimationDriver.cs` — 수정 최소화: 외부에서 상태명 문자열로 구동할 공개 진입점 1개 (`ApplyStateByName(string)` 등 — 기존 이벤트 구독 경로 불변)

### 구현 규칙
- 권한자: 기존 이벤트 경로 그대로 (드라이버가 StateChanged 직접 구독 — 무변경).
- 비권한: `Render()`에서 `NetStateName` 변화 감지(ChangeDetector — HuntTarget.Render 패턴) → `driver.ApplyStateByName(newName)`. 드라이버의 파라미터 매핑/배타 bool 스위칭 재사용. 피격/사망 연출은 `NetCurrentHp` 변화로 트리거(감소=피격, 0=사망).
- 드라이버 수정은 진입점 추가만 — 로컬 경로(이벤트 구독) 회귀 금지.
- 90Hz: Render 내 문자열 비교 1회 수준 유지.

### 완료 판정
- [ ] 멀티 피어 더미 2인: 더미(비권한) 시점 로그/스크린샷에서 곰 이동(NetworkTransform)과 [MonsterAnim] 파라미터 전환이 권한자 상태와 일치
- [ ] 권한자 쪽 애니 경로 회귀 없음 (CombatSandbox [MonsterAnim] 로그 동일)
- [ ] 컴파일 + 봇 회귀 ALL PASS

---

## 금지 사항
- 비권한 클라에서 FSM/판정 코드 실행 금지 — 표현만.
- MonsterAnimationDriver의 기존 구독 경로 변경 금지.
