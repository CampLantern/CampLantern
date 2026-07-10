# Step 02: EditorTools — HuntMonster_Bear 프리팹 + 사냥터 씬 추가 스폰

- **영역:** `EditorTools` (`Assets/Scripts/Editor/`)
- **선행 단계:** step-01 (`NetworkedHuntMonster`)
- **후행 단계:** step-03~05가 이 프리팹으로 검증
- **스펙:** step-12 고정 방침 — "기존 P0 라인 무손상, 추가 스폰"

---

## 목적
전투 몬스터 네트워크판 프리팹 `HuntMonster_Bear.prefab`을 만들고 사냥터 씬(HuntZone_A)에 **4번째 사냥감으로 추가**한다. 기존 단순 HP 3종(사슴/멧돼지/곰)은 그대로 — 검증 후 교체는 팀 결정.

---

## 에이전트 실행 지침

`/task-start` 호출 후 수행. `HuntTargetPrefabFactory`(NetworkObject 플래그)·`CombatMonsterPrefabFactory`(전투 컴포넌트 조립)·`RoomScenesFactory.WireHuntAnimalsIntoHuntZone`(씬 스폰 배열 배선) 패턴 재사용.

### 생성/수정 파일
- `Assets/Scripts/Editor/NetworkedHuntMonsterFactory.cs` — 생성
- (수정 대안 허용) `RoomScenesFactory.SetHuntPrefabs`의 경로 배열에 신규 프리팹 추가 — 최소 diff로

### 프리팹 구성 (`Assets/Prefabs/HuntMonster_Bear.prefab`)
- 루트: `NetworkObject`(**MasterClientObject + ~DestroyWhenStateAuthorityLeaves** — HuntTargetPrefabFactory 규칙 그대로) + `NetworkTransform`(비권한 위치 수신) + `NetworkedHuntMonster` + `HuntLedger` + Combat 조립(MonsterController/MonsterHealth/Aggro/SkillRunner/SkillHitCheck/MonsterAnimationDriver — CombatMonsterPrefabFactory와 동일 Configure, balance 포함)
- `Monster_Bear_Test.asset`의 `huntDef`에 `Hunt_Bear.asset` 주입 (RequiredParticipants 2 — 협동 게이트)
- Visual: Bear_4 중첩 인스턴스 + 피격/약점 볼륨 (CombatMonsterPrefabFactory.AddHitVolumes 재사용 — internal화 or 복제 중 선택, 근거)
- 메뉴: `Tools > Make Assets > Hunt Monster — Bear (Networked)` + 씬 배선 메뉴(HuntZone_A의 HuntZoneHarness `m_huntPrefabs`/`m_huntSpawnPositions`에 4번째 항목 추가, 멱등)

### 제약
- RULE-02: PrefabUtility/SerializedObject만. 스폰 위치는 기존 3종과 겹치지 않게 (예: (0,0,12)).
- Fusion 프리팹 테이블 자동 베이킹 확인 — 무음 실패 시 Rebuild Prefab Table 후 재확인 (tech-stack-decisions).
- HuntZoneHarness.cs 코드 수정 금지 — SerializedObject 배선만.

### 완료 판정
- [ ] 프리팹 생성 + `NetworkObject.Flags` 마스터 규칙 확인 (HuntTarget.prefab과 동일)
- [ ] HuntZone_A 씬 열고 플레이(브릿지): 세션 접속 → 4번째 사냥감 스폰 → `NetworkedHuntMonster` 권한자에서 FSM 활성(Idle 배회), 기존 3종 스폰 무손상
- [ ] 로컬 폴백 회귀: CombatSandbox 봇 7종 ALL PASS 유지
- [ ] 수동 체크리스트 보고 (스폰 위치·존 밀도)

---

## 금지 사항
- 기존 HuntTarget 3종 프리팹·스폰 제거 금지 (추가만).
- CombatSandbox 씬에 네트워크 프리팹 배치 금지 (로컬 씬 순수성).
