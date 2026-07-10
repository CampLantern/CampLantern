# 전투 네트워크 동기화 검증 시나리오 (step-05 산출물)

지시서 8단계 완료 판정의 재현 절차와 실측 결과. 재검증 주체: `NetworkedHuntSelfTest` 봇
(HuntZone_A 플레이 중 주입 — `knowledge/unity-editor-automation.md` 봇 주입 패턴).
전제: 멀티 피어 3종 세트 (PeerMode Multiple · EnqueueIncompleteSynchronousSpawns · P0NetArena — tech-stack-decisions).

## 1. 2클라 동시 타격 HP 정합 — **PASS (2026-07-10 실측)**
- 절차: 메인 피어 접속 → 더미 피어 접속(2인) → TryStartHunt → 양 피어에서 각 5회(10dmg) 교차 타격 → 1.5s 정착 대기 → 양 피어의 `NetCurrentHp/NetMaxHp` 대조.
- 정합 원리: 적용이 권한자 한 곳(RPC 직렬화)이라 순서 무관 합산 — HP 스케일링(+150)은 더미의 첫 유효타 시점에 1회.
- 기대값: 300(base) + 150(2인 스케일링) − 100(총데미지) = **350/450** (결정론).
- 실측: main=350/450, dummy=350/450 — 불일치 0.

## 2. 협동 게이트 (social-cooperation ②) — **PASS**
- 1인: `TryStartHunt` 거부 (PlayerCount 1 < RequiredParticipants 2, HuntTargetDef 소관).
- 2인: 시작 성공 + `HuntActive` 더미 피어 복제 확인.

## 3. 처치·보상 (참여자 전원 동일) — **PASS**
- 처치 시 양 피어 각각 자기 클라에서 `RewardGranted(Hunt_Bear)` 발화 (HuntLedger.IsParticipant 로컬 판정).
- `HuntActive` false 전파 — 처치 후 타격 무효 게이트.

## 4. 권한 게이팅·표현 동기화 — **PASS**
- 마스터: MonsterController.enabled=true (FSM 실행). 더미: false (FSM 미실행) + `NetStateName` 수신
  (실측 'Dead' — 처치 직후 상태까지 복제). 비권한 애니는 NetStateName/NetCurrentHp 변화 → 드라이버 구동 (step-04).

## 5. 로컬(오프라인) 폴백 회귀 — **PASS**
- Runner 없는 플레이에서 봇 7종 ALL PASS (2026-07-10):
  Melee(4) / MonsterFsm(4) / Aggro(6) / Skill(4) / Bow(5) / Holster(4) — 동시 주행,
  PlayerRules(5) — **단독 주행 필수** (전멸 감시가 PlayerHealth.All 전역을 보므로 타 봇 플레이어와 동시 주행 시
  오탐 — 실측으로 재확인된 격리 조건).
- 기존 HuntZoneHarness P0 루프: HuntTarget 3종 스폰·IMGUI 무손상 (step-02 스크린샷 — 코드 무수정).

## 6. 마스터 이전 (권한자 이탈) — 절차 정의 (수동/후속)
- 프리팹 플래그: `MasterClientObject | ~DestroyWhenStateAuthorityLeaves` — HuntTarget과 동일 (이탈 시 파괴 방지 + 권한 승계).
- 재게이팅: `HasStateAuthority` 폴링 → 새 마스터에서 FSM enabled 전환 (NetworkedHuntMonster.Render).
- 절차: 2피어 접속 → 사냥 진행 중 메인 피어 Shutdown → 더미가 마스터 승계 → HP/HuntActive 유지 + FSM이 더미에서 활성화되는지 확인.
- **미실측**: 에디터 멀티 피어에서 메인 피어(에디터 주 러너) 종료 시 더미만 남는 시나리오는 하네스 재작업 필요 — 실기 2대 테스트 항목으로 이월. 상태 유지의 구조적 근거(플래그·[Networked] 상태·폴링 재게이팅)는 코드로 확보.

## 7. §12 진행도 불일치 1% 미만 — 측정 방법 제안
- 지표: 사냥 1회당 (각 클라가 관측한 최종 HP 시퀀스 või 처치·보상 이벤트 발화 여부)의 피어 간 불일치율.
- 자동화: NetworkedHuntSelfTest를 N회 반복(사냥 시작→처치) 돌려 `양 피어 보상 발화 일치` 횟수/N ≥ 99% 확인.
  P0 검증 지표 측정 시 이 봇을 반복 하네스로 재사용.

## 알려진 한계 (후속 항목)
- 다운/소생/전멸의 2피어 실측: NetworkedCombatPlayer 스폰은 확인(단일 피어), 더미 피어에는 스포너가 없어
  (씬 GO 기반) 더미 쪽 전투 존재가 스폰되지 않음 — 더미용 스폰 경로는 실기/후속 검증 항목.
- Stunned 오버레이는 메인 상태명이 아니라 원격 표현 미지원 (MonsterAnimationDriver 주석, TODO(SPEC)).
- 몬스터가 HuntActive 전에도 FSM(배회/추격/공격)은 활성 — 타격만 게이트 (HuntTarget 의미론 계승). 시작 전 공격 억제 여부는 팀 결정 필요.
