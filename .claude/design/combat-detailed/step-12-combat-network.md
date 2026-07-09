# Step 12: 네트워크 권한 분리 + Hunting 통합 (마지막 — 착수 전 상세 재분해)

- **영역:** `Combat` + `Hunting` + `Networking` (통합 단계 — "한 단계 = 한 영역"의 의도적 예외)
- **선행 단계:** step-01~11 전부 + **step-11 종합 검증 통과** (지시서: "로컬에서 완전히 돌아간 뒤에만 착수")
- **후행 단계:** 없음 (전투 시스템 완결)
- **스펙:** 지시서 8단계 (§5) — 네트워크 솔루션은 **Photon Fusion 2 Shared Mode** (치환표)

---

## ⚠️ 착수 게이트
이 파일은 **통합 방침만 고정**한다. 실제 착수 시점에 로컬 구현의 실형태(이벤트·클래스 구성)가 확정되어 있으므로, **아키텍트와 함께 이 단계를 3~5개 세부 단계로 재분해**(`/design` 재실행 — `combat-detailed-network` 등)한 뒤 진행한다. 아래 방침과 다른 설계가 필요하면 먼저 보고.

---

## 목적
"클라이언트 감지 → 권한자(State Authority) 적용" 권한 분리를 적용하고, 로컬 전투를 기존 P0 협동 사냥 계약(`HuntTarget`/`HuntLedger`)과 통합한다.

---

## 통합 방침 (고정)

### 권한 분리 (지시서 8단계 그대로)
- **히트 감지는 클라 유지**: 각 클라의 무기/화살 판정 코드 무변경.
- **HitEvent 직렬화 전송**: 감지 결과(대상 몬스터, 부위/약점, 속도, 공격자)를 권한자로 전송. **기존 [`HuntTarget.ApplyHit`](../../../Assets/Scripts/Hunting/HuntTarget.cs) 패턴이 참조 모델** — `HasStateAuthority`면 직접 적용, 아니면 `[Rpc(RpcSources.All, RpcTargets.StateAuthority)]`.
- **권한자 일괄 적용**: 데미지 최종 확정(약점 배율 포함 — 수신 측 적용 결정이 여기서 완성됨), HP 차감, 사망, 어그로 재계산, HP 스케일링, 기여자 등록 → `[Networked]` 상태로 전파.
- **`IDamageable.ApplyDamage` 경계에서 분리**: 로컬(오프라인/샌드박스) 모드에서는 기존처럼 즉시 적용 폴백 유지.
- **몬스터 FSM/어그로/스킬 선택은 권한자에서만 실행**, 상태 동기화(위치·상태명·애니 트리거는 각 클라 재현).
- **소생/다운/전멸 판정도 권한자로 이동.**

### Hunting 계약 보존 (이 프로젝트 고유 — 지시서에 없는 필수 사항)
- **`TryStartHunt` 2인 게이트 보존**: 협동 필수 사냥감은 `RequiredParticipants` 미만이면 사냥 시작 불가 (social-cooperation ② — 전투 몬스터에도 동일 적용 방식 재분해 시 결정).
- **기여 = `HuntLedger`로 통일**: step-09의 로컬 기여자 Set(유효타 1회 이상)을 `HuntLedger.RecordContribution(player, Hit)`로 연결. `ContributionKind.Lure/Assist` 경로도 유지(유인 등 비타격 기여).
- **보상 = `RewardGranted` 유지**: 참여자 전원 동일 보상(차등 금지 — 경쟁적 박탈감 방지 원칙), 보상 내용은 `HuntTargetDef.RewardMaterials`(코인 아닌 고유 재료 — resource-loop 원칙).
- **`MonsterData` ↔ `HuntTargetDef` 관계 결정**: 재분해 시 확정 — 후보: MonsterData가 HuntTargetDef를 참조(보상·인원은 HuntTargetDef 유지, 전투 스탯은 MonsterData) vs 통합 SO. 저장 파일이 참조하는 것은 HuntTargetDef 계열이므로 Id 체계 변화에 주의.
- **`HuntTarget`(단순 HP 네트워크 오브젝트)의 거취**: 전투 몬스터의 네트워크판이 이를 대체할지, HuntTarget이 MonsterController를 품을지 재분해 시 결정. 어느 쪽이든 `HuntZoneHarness`의 기존 P0 검증 흐름이 깨지지 않아야 함.

### 검증 (지시서 그대로)
- 2클라 동시 타격 시 HP 정합성(불일치 없음), 호스트(마스터) 이전 시 상태 유지 — 동기화 테스트 시나리오를 문서로 정리.
- 협동 품질 지표: 진행도 불일치 1% 미만 (mvp-scope P0 기준) 검증 준비.

### 제약
- `NetworkBehaviour` 신설 시 Fusion 프리팹 테이블 재베이크 유의 (무음 실패 시 `Fusion > Rebuild Prefab Table` — tech-stack-decisions).
- RULE-01/02/03/04 전부 해당. 씬/프리팹 배선은 팩토리 확장으로.

### 완료 판정 (개요 — 재분해 시 세분화)
- [ ] 2클라 동시 타격 HP 불일치 없음
- [ ] 로컬(오프라인) 샌드박스 플로우 무손상 (폴백 동작)
- [ ] 기존 HuntZoneHarness P0 루프(2인 협동·보상 공유) 무손상 또는 의도된 대체 확인
- [ ] 동기화 테스트 시나리오 문서화

---

## 금지 사항
- 이 파일만 보고 바로 구현 착수 금지 — 상단 착수 게이트(재분해) 필수.
- 로컬 판정 코드를 권한 분리 명목으로 재작성 금지 — 감지는 그대로, 적용만 분리.
