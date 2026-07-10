# Step 05: 검증 — 2클라 정합·로컬 폴백 회귀·동기화 시나리오 문서

- **영역:** 검증 전용 (신규 게임 코드 없음 — 봇/문서만)
- **선행 단계:** step-01~04 전부
- **후행 단계:** 없음 (전투 시스템 완결 — /task-done 승격)
- **스펙:** 지시서 8단계 완료 판정 + §12 협동 품질(진행도 불일치 1% 미만) 검증 준비

---

## 목적
지시서 8단계의 완료 판정을 재현 가능한 형태로 확정한다: 2클라 동시 타격 HP 정합, 호스트(마스터) 이전 시 상태 유지, 로컬 플로우 무손상. 결과를 동기화 테스트 시나리오 문서로 남긴다.

---

## 에이전트 실행 지침

`/task-start` 호출 후 수행. 멀티 피어 검증은 tech-stack-decisions "에디터 더미 2인 테스트 3종 세트"와 `Tools > P0 Play Test > Run Coop Hunt Test` 전례를 따른다.

### 산출물
- (선택) `Assets/Scripts/Hunting/NetworkedHuntSelfTest.cs` — 무인 검증 봇: 더미 피어 접속 → TryStartHunt(2인 게이트) → 양 클라 동시 타격 → HP 정합 비교 → 처치 → 양 클라 보상 이벤트 확인. 기존 봇 주입 패턴.
- `.claude/design/combat-detailed-network/VERIFICATION.md` — 동기화 테스트 시나리오 문서:
  1. 2클라 동시 타격 시 HP 정합 (RPC 직렬화 순서 — 불일치 없음 확인 방법)
  2. 마스터 이전(권한자 이탈) 시 상태 유지 — MasterClientObject 플래그 검증 절차
  3. 비권한 클라 표현 일치 (상태명/애니/위치)
  4. 다운/소생/전멸 동기화 시나리오
  5. 로컬 폴백 (Runner 없는 씬) 회귀 목록 — 봇 7종
  6. §12 진행도 불일치 1% 미만 측정 방법 제안 (P0 통과 기준 연결)

### 검증 절차 (봇/브릿지)
- HuntZone_A 플레이 → 세션 접속 → 더미 피어 추가(3종 세트 전제 확인) → 2인 게이트: 1인일 때 TryStartHunt 거부 → 2인 시작 → 양쪽에서 `RPC_ApplyHit` 다발 → 권한자 `NetCurrentHp` 단조 감소·비권한 표시 일치 → 처치 → 참여자 전원 보상 이벤트(각자 클라) → 스케일링(2인째 첫 타격 시 NetMaxHp 증가) 확인.
- 로컬 회귀: CombatSandbox 봇 7종 (Melee/Fsm/Aggro/Skill/Bow/PlayerRules(단독)/Holster) ALL PASS.

### 완료 판정
- [ ] 2클라 동시 타격 HP 불일치 없음 (로그 수치 대조)
- [ ] 로컬(오프라인) 샌드박스 플로우 무손상
- [ ] 기존 HuntZoneHarness P0 루프(사슴 2인 협동·보상 공유) 무손상
- [ ] VERIFICATION.md 작성 완료
- [ ] `/task-done` 승격 제안 준비: 판정 규약·NotifyTeleported 규약·봇 검증 패턴·권한 분리 구조 → `domain/combat-system.md`

---

## 금지 사항
- 검증 중 발견된 스펙 이견 임의 수정 금지 — 아키텍트 보고 (지시서 운영 팁).
- 마스터 이전 검증을 이유로 프로덕션 코드에 테스트 전용 훅 추가 금지 (봇/브릿지로만).
