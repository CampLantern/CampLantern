# Step 04: EditorTools — CombatSandbox 로컬 검증 씬 팩토리

- **영역:** `EditorTools` (`Assets/Scripts/Editor/`)
- **선행 단계:** step-02(무기 배선), step-03(곰 프리팹) 완료 필요
- **후행 단계:** step-05~11의 플레이 검증 무대. step-11 하네스가 이 씬에 배선됨
- **스펙:** 지시서 각 단계의 "완료 판정"을 재현할 무대 (지시서 자체 단계는 아님)

---

## 목적
전투를 **네트워크 없이** 검증할 로컬 샌드박스 씬을 팩토리로 생성한다. `PersistentPlayer`가 어느 씬에서든 자동 스폰되므로(리그 2겹 구조 — CLAUDE.md) VR 리그는 배치하지 않는다. 이후 단계 산출물이 생길 때마다 팩토리를 재실행해 씬을 갱신한다(멱등).

---

## 에이전트 실행 지침

`/task-start` 호출 후 수행. 씬 생성 패턴은 [`RoomScenesFactory`](../../../Assets/Scripts/Editor/RoomScenesFactory.cs)·`P0PlaySceneFactory`를 참조한다.

### 생성/수정 파일
- `Assets/Scripts/Editor/CombatSandboxFactory.cs` — 생성 (`CampLantern.EditorTools`)
- (선택) `Assets/Scripts/Editor/CombatSandboxPlayTestMenu.cs` — 씬 열기/플레이 진입 메뉴 (`RoomScenesPlayTestMenu` 패턴)

### 씬 구성 (`Assets/Scenes/CombatSandbox.unity`)
- 메뉴 `Tools > Make Assets > Combat Sandbox Scene`. **멱등** — 이미 있으면 구성 요소를 Ensure(있으면 갱신, 없으면 추가)한다. 이후 단계(하네스 등) 산출물도 재실행 시 자동 배선되도록 Ensure 단위로 쪼갠다.
- 구성:
  - 지면(Plane, 충분한 크기 — 곰 추격 반경 테스트 가능하게) + 라이트
  - `PlayerSpawnPoint` (기존 `CampLantern.Player.PlayerSpawnPoint`)
  - `CombatMonster_Bear.prefab` 인스턴스 1 (스폰 지점 = Return 원위치)
  - 무기 테이블: `Sword`/`Spear`(전투 배선본) + `Bow`/`Arrow`는 step-08 후 재실행 시 추가
  - `DummyPlayer` 자리(비활성 GO 2개 — step-05 어그로 2인 테스트·step-09 소생 테스트용 마커. 실제 구동은 step-11 하네스)
  - step-03이 NavMesh를 채택했다면 NavMeshSurface 추가+베이크 호출(코드로 가능한지 확인, 불가 시 수동 체크리스트)
- **RULE-02**: `.unity`는 `EditorSceneManager` API로만 생성/저장. 직접 텍스트 작성 금지.

### 선행 산출물 의존성
- `CombatMonster_Bear.prefab` — step-03 팩토리. `Sword`/`Spear` 배선본 — step-02 팩토리.

### 제약
- 네트워크 오브젝트(`SessionLauncher`/`NetworkRunner`) 배치 금지 — 이 씬은 로컬 전용. 네트워크 검증은 step-12에서 기존 사냥터 씬으로.
- 빌드 세팅 등 `ProjectSettings/` 수정 금지(RULE-04) — 씬은 에디터 플레이로만 연다.

### 완료 판정
- [ ] 팩토리 실행 → `Assets/Scenes/CombatSandbox.unity` 생성, 재실행해도 중복 없음(멱등)
- [ ] ClaudeBridge 플레이 스모크: 씬 진입 시 NRE 0, `PersistentPlayer` 자동 스폰, 곰이 Idle 배회
- [ ] **step-02·03 완료 판정 소급 재현**: 휘두르면 속도 비례 로그·무효타·약점 ×1.5 / 배회→추격→귀환 사이클
- [ ] 수동 작업 체크리스트 보고 (NavMesh 베이크 확인 등)

---

## 금지 사항
- 씬에 게임플레이 로직 스크립트를 새로 만들지 않는다 — 배선만. IMGUI 하네스는 step-11 몫.
- `.unity`/`.prefab` 텍스트 직접 편집 금지 (RULE-02).
