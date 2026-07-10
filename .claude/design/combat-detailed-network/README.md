# 전투 네트워크 권한 분리 + Hunting 통합 (combat-detailed step-12 재분해)

## 한 줄 요약
로컬 완성된 전투(combat-detailed step-01~11)를 Fusion 2 Shared Mode에 올린다 — 히트 감지는 클라 유지, 적용은 State Authority(마스터) 일괄, 몬스터 FSM/어그로/스킬은 권한자 전용 실행 + 상태 동기화, 기존 `HuntLedger` 기여·보상 계약과 `TryStartHunt` 2인 게이트를 보존한다.

## 원문 / 근거
- **고정 방침**: [`combat-detailed/step-12-combat-network.md`](../combat-detailed/step-12-combat-network.md) — 이 재분해의 상위 계약. 지시서 8단계(§5) 그대로.
- 스펙: [`gdd/전투_시스템_구현_지시서.md`](../../domain/gdd/전투_시스템_구현_지시서.md) 8단계. 네트워크 솔루션 = **Photon Fusion 2 Shared Mode** (치환표).
- 도메인: [`tech-stack-decisions.md`](../../domain/tech-stack-decisions.md)(Fusion 위빙·멀티 피어 3종 세트·프리팹 테이블 함정), [`room-architecture.md`](../../domain/room-architecture.md)(hunt_zone_{zoneId}·존 분할), [`social-cooperation.md`](../../domain/social-cooperation.md)(기여·공유 보상).
- 로컬 실형태(전제): `IDamageable.ApplyDamage` 단일 지점, `MonsterHealth`(기여 Set·HP 스케일링·HuntSucceeded), `AggroController`(6규칙), `MonsterController`(상태 패턴+오버레이), `PlayerHealth`(다운/소생/전멸), `CombatPlayers` 레지스트리, `CombatMonster_Bear`(로컬 프리팹).

## 아키텍처 결정
- **권한자 = Shared Mode 마스터 클라이언트** — 기존 [`HuntTarget`](../../../Assets/Scripts/Hunting/HuntTarget.cs)과 동일 모델: `NetworkObjectFlags.MasterClientObject` + `DestroyWhenStateAuthorityLeaves` 해제(권한 이전 시 상태 유지 — HuntTargetPrefabFactory 패턴 재사용).
- **Combat 코어는 Fusion 무참조 유지** — 로컬 봇 검증·오프라인 폴백이 그대로 살아야 한다. 네트워크 계층(`Hunting`/`Networking`)이 Combat을 감싼다:
  - 비권한 클라: `NetworkedHuntMonster`(IDamageable)가 HitEvent를 RPC로 권한자에 전달. 권한자: 로컬 `MonsterHealth.ApplyDamage` 그대로 실행(스케일링·약점 배율·기여 판정 재사용).
  - `HitVolume`에 `OverrideOwner(IDamageable)` 1개 API만 추가(Combat 소폭) — 같은 GO의 MonsterHealth 대신 네트워크 라우터가 피격 수신자가 되도록.
- **권한 게이팅 = 컴포넌트 enabled 토글** — 권한자만 `MonsterController`/`AggroController`/`SkillRunner`/`SkillHitCheck` 활성. 비권한 클라는 `NetworkTransform` 위치 수신 + `[Networked] StateName` 변화로 애니만 재현(판정·FSM 미실행). 지시서 "FSM/어그로/스킬 선택은 호스트에서만 실행하고 상태 동기화" 그대로.
- **기여 = HuntLedger로 통일** — 권한자에서 유효타 적용 시 `HuntLedger.RecordContribution(attackerRef, Hit)`. 공격자 PlayerRef는 감지 클라가 자기 `Runner.LocalPlayer`를 RPC 인자로 스탬핑(§5 "공격자" 직렬화). `RewardGranted` 공유 보상·`Lure/Assist` 경로·참여자 전원 동일 보상 원칙 불변. MonsterHealth의 로컬 GameObject 기여 Set은 오프라인 폴백 전용으로 유지.
- **`MonsterData` ↔ `HuntTargetDef` = 분리 유지 + 참조 연결** — `MonsterData.huntDef` 필드 추가: 보상(RewardMaterials)·필요 인원(RequiredParticipants)·Id(저장 파일 참조)는 HuntTargetDef 소관, 전투 스탯은 MonsterData. `HuntTargetDef.MaxHealth`는 구 HuntTarget 라인 전용으로 남는다(중복 정리는 정식 스키마 때 — TODO(SPEC)).
- **기존 P0 라인 무손상 — 대체가 아니라 추가** — `HuntTarget.prefab`(사슴)/`HuntTarget_WildBoar`/`HuntTarget_Bear`(단순 HP 3종)와 HuntZoneHarness 흐름은 건드리지 않는다. 새 `HuntMonster_Bear.prefab`(전투 몬스터 네트워크판)을 **4번째 스폰으로 추가**해 검증하고, 구 라인 제거는 검증 후 팀 결정.
- **플레이어 동기화는 최소 신규** — 플레이어당 스폰되는 `NetworkedCombatPlayer`(VoiceController/AvatarController와 동일한 SessionStarted 스폰 패턴)가 ① 원격 플레이어를 `CombatPlayers`에 등록(몬스터가 원격도 타겟) ② `[Networked] IsDowned` 동기화 ③ 몬스터 스킬 피격을 소유 클라에 RPC 전달 ④ 전멸은 권한자(마스터)가 전원 다운 관찰 후 브로드캐스트. 소생 홀드 판정은 소생자 클라 감지 → 완료 시 RPC(§5 "소생/다운/전멸 호스트 권한" — Shared Mode에선 마스터가 확정자).
- **에디터 배선 동반 원칙·검증 방식은 combat-detailed와 동일** — 팩토리(RULE-02), ClaudeBridge, 멀티 피어 더미 3종 세트(tech-stack-decisions — PeerMode Multiple·EnqueueIncompleteSynchronousSpawns·StartGameArgs.Scene=P0NetArena).

## 터치 영역
| 영역 | 경로 | 역할 |
|---|---|---|
| Hunting | `Assets/Scripts/Hunting/` | `NetworkedHuntMonster`(권한 라우터·게이팅·동기화) — 신규 2파일 |
| Networking | `Assets/Scripts/Networking/` | `NetworkedCombatPlayer`(플레이어 동기화) |
| Combat | `Assets/Scripts/Combat/` | 소폭: `HitVolume.OverrideOwner`, `MonsterData.huntDef` (계약만, step-01) |
| EditorTools | `Assets/Scripts/Editor/` | `HuntMonster_Bear` 프리팹·사냥터 씬 배선 팩토리 |
| Bootstrap | `Assets/Scripts/Bootstrap/` | (참조만) HuntZoneHarness — 스폰 배열은 팩토리가 배선, 코드 무수정 |

## 의존성 그래프
```
Combat 계약 소폭 확장 (HitVolume.OverrideOwner, MonsterData.huntDef)   (step-01 내 선행)
        │
Hunting/NetworkedHuntMonster (권한 라우팅·게이팅·HuntLedger 연동)      (step-01)
        │
EditorTools: HuntMonster_Bear.prefab + HuntZone_A 배선                (step-02)
        │
Networking/NetworkedCombatPlayer (원격 타겟 등록·다운/전멸 동기화)     (step-03)
        │
Hunting: 비권한 표현 동기화 (StateName→애니, 보간 확인)               (step-04)
        │
검증: 2클라 HP 정합·로컬 폴백 회귀·시나리오 문서                      (step-05)
```

## 단계
1. [step-01-hunting-networked-monster.md](step-01-hunting-networked-monster.md) — Hunting: 권한 라우터/게이팅/2인 게이트/HuntLedger 연동 (+Combat 계약 2건)
2. [step-02-editor-net-prefab.md](step-02-editor-net-prefab.md) — EditorTools: HuntMonster_Bear 프리팹 + 사냥터 씬 추가 스폰
3. [step-03-networking-player.md](step-03-networking-player.md) — Networking: NetworkedCombatPlayer (원격 타겟·다운/소생/전멸 동기화)
4. [step-04-hunting-presentation.md](step-04-hunting-presentation.md) — Hunting: 비권한 클라 표현 (상태 동기화→애니 드라이버)
5. [step-05-verification.md](step-05-verification.md) — 검증: 2클라 정합·폴백 회귀·동기화 시나리오 문서

## 병렬 실행 가능성
- step-01 → 02 → 이후 순차 권장. step-03은 step-01 완료 후 step-02와 병렬 가능(파일 독립).
- step-04는 step-02(프리팹) 필요. step-05는 전부 완료 후.

## 공통 규약 (모든 단계)
- combat-detailed README 공통 규약 전부 승계 (판정 규약·재설계 금지·수치 SO·컨벤션·RULES).
- **로컬 폴백 불변**: Runner 없는 씬(CombatSandbox)에서 기존 봇 7종이 계속 ALL PASS여야 한다 — 각 단계 완료 판정에 포함.
- **Fusion 함정 체크리스트**: NetworkBehaviour 신설 후 스폰 무음 실패 시 `Fusion > Rebuild Prefab Table`(tech-stack-decisions). 멀티 피어 테스트는 3종 세트 전제. `Assembly-CSharp`은 AssembliesToWeave에 이미 포함(확인만).
- **기존 파일 수정 금지 목록**: `HuntTarget.cs`/`HuntLedger.cs`(참조·부착만)/`HuntZoneHarness.cs`(팩토리 배선만)/`SessionLauncher.cs`.

## 첫 단계 착수
```
/task-start
```
그다음 [step-01-hunting-networked-monster.md](step-01-hunting-networked-monster.md)의 지시를 수행.
