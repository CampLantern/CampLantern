# Step 11: Bootstrap — CombatSandboxHarness + 로컬 종합 검증

- **영역:** `Bootstrap` (`Assets/Scripts/Bootstrap/`)
- **선행 단계:** step-09·10 완료 필요 (로컬 전투 전 기능)
- **후행 단계:** step-12는 이 단계의 종합 검증 통과 후에만
- **스펙:** 지시서 각 단계 완료 판정의 종합 재현 (지시서 자체 단계는 아님 — 프로젝트 하네스 관례)

---

## 목적
CombatSandbox 씬에 IMGUI 디버그 하네스를 붙여 **로컬 전투 루프 전체를 사람/봇이 검증 가능하게** 만든다. 기존 하네스 관례(P0Harness/HuntZoneHarness — IMGUI는 개발용 임시, Quest 빌드 전 제거 대상)를 따른다.

---

## 에이전트 실행 지침

`/task-start` 호출 후 수행. [`HuntZoneHarness`](../../../Assets/Scripts/Bootstrap/HuntZoneHarness.cs)의 IMGUI/더미 패턴을 참조한다.

### 생성/수정 파일
- `Assets/Scripts/Bootstrap/CombatSandboxHarness.cs` — 생성 (`CampLantern.Bootstrap`)
- `Assets/Scripts/Editor/CombatSandboxFactory.cs` — 수정 (하네스 GO Ensure 배선 + 재실행)

### 하네스 기능 (IMGUI)
- **표시**: 몬스터 HP/MaxHp(스케일링 확인)/현재 상태/어그로 타겟·고정 잔여, 플레이어 HP/다운 여부, 무기 내구도, 화살 수량.
- **조작 버튼**:
  - 더미 플레이어 추가/제거 (어그로 2인 테스트·HP 스케일링·소생·전멸 테스트용 — 어그로 플레이어 목록에 등록/해제)
  - 더미로 타격(일반/약점) — 어그로 타겟 스틸 재현
  - 로컬 플레이어 강제 다운 / 더미로 소생 시작 / Stunned 오버레이 발동
  - 몬스터 리셋 (재사냥)
- **데스크톱 조작 어댑터**(마우스/키보드로 스윙·발사 대체)는 검증에 필요한 최소한만 — 코어의 입력 무관 진입점(step-02 속도 주입 가능 여부는 구현 확인, `Bow.SetDrawRatio`/`TryFire` 등)을 호출.

### 종합 검증 (이 단계의 핵심 산출물 — 결과를 보고서로)
지시서 각 단계 완료 판정 전체 재확인 + 다음 시나리오:
1. 근접: 유효타/무효타/약점 ×배율/몬스터별 첫 접촉 1회/내구도 소진 → 홀스터 귀환 → 수리
2. 몬스터: 배회→추격→공격(hit window)→탈진(프리딜)→추격 리듬, 이탈 시 귀환(무적·회복), Stunned(탈진 위 포함), Death(박힌 화살 보존)
3. 어그로: 근접 타겟팅·히스테리시스·약점 고정 5s·감쇄 −1.5s·소진 시 타겟 스틸·다운 시 재계산
4. 활: 당김-초속·낙차·무게 보너스·박힘/회수/만땅 무반응
5. 플레이어: 다운→소생(3s 홀드, HP 절반)→전멸→Scene 복귀, 2인째 유효타 시 HP 스케일링(잔여 플랫 가산)
6. 기여: 유효타 1회 이상만 기여자 목록 이벤트에 포함
- 검증은 ClaudeBridge 플레이 런북(`knowledge/unity-editor-automation.md`)으로 에이전트가 직접 수행 — 낚시의 `FishingLoopSelfTest` 같은 자가 검증 봇 도입 여부는 에이전트 판단.
- **`TODO(TUNING)`/`TODO(SPEC)` 전체 목록 + 지시서 대비 차이(이유 포함) 보고** (README 완료 조건).

### 선행 산출물 의존성
- step-01~10 전 산출물. 특히 이벤트: `ValidHit`/`WeaponBroken`/`Damaged`/`Died`/`Downed`/`Revived`/`PartyWiped`/`HuntSucceeded`/`CountChanged`.

### 제약
- IMGUI는 개발용 임시 — 실사용 UI는 VR UI 토대(VRUIPanel/VRUIButton)로 후속 (CLAUDE.md 관례, 주석 명시).
- 하네스는 이벤트 구독·표시·주입만 — 전투 로직을 하네스에 넣지 않는다.
- rules/scripts.md: 구독 해제, OnGUI 경량 유지.

### 완료 판정
- [ ] 종합 검증 시나리오 1~6 전부 통과 (실패 항목은 원인과 함께 보고 — 통과할 때까지 이 단계에서 수정)
- [ ] ClaudeBridge 플레이 스모크: 씬 진입~전투 루프 NRE 0
- [ ] TODO 목록·차이 보고서 작성
- [ ] `/task-done`으로 판정 규약·전투 구현 결정의 `domain/combat-system.md` 승격 제안 (fishing-system.md 전례)

---

## 금지 사항
- 네트워크 코드 착수 금지 — step-12는 이 단계의 종합 검증 **통과 후** 별도 세션.
- 검증 중 발견된 스펙 이견을 임의 수정 금지 — 아키텍트 보고 (지시서 운영 팁: 절 번호로 되묻기).
