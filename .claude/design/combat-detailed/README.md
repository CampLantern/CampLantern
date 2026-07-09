# 전투 시스템 (사냥터 근접·활·몬스터 AI·플레이어 규칙)

## 한 줄 요약
사냥터 전투를 **로컬 우선**으로 구현한다 — 속도 비례 근접 유효타, 당김 기반 활/화살, 상태 패턴 몬스터 FSM(Idle/Chase/Attack/Exhausted/Return + Stunned/Death 오버레이), 어그로(근접 타겟팅 + 약점 고정/감쇄), 다운/소생/전멸·HP 스케일링·홀스터/내구도. 마지막 단계에서만 Fusion 권한 분리로 기존 `HuntTarget`/`HuntLedger`와 통합한다.

## 원문 / 근거
- **스펙**: [`.claude/domain/gdd/전투_시스템_구현_지시서.md`](../../domain/gdd/전투_시스템_구현_지시서.md) — 0~8단계 프롬프트 모음. **각 단계 본문에 규칙이 인라인으로 기술되어 있고, 세부기획서 원본(`전투_시스템_세부_기획서.docx`)은 직접 Read 불가이므로 지시서 인라인 텍스트가 읽을 수 있는 유일한 스펙이다.** "§X-X" 표기는 그 인라인 규칙의 출처 표시로만 이해한다.
- 라우팅: [`.claude/INDEX.md`](../../INDEX.md) Level 2 "전투 시스템" 항목.
- 도메인: [`resource-loop.md`](../../domain/resource-loop.md) 사냥 섹션, [`social-cooperation.md`](../../domain/social-cooperation.md)(기여·공유 보상), [`mvp-scope.md`](../../domain/mvp-scope.md).
- MVP 위상: P0 "2인 협동 사냥"의 심화(낚시 정교화 `fishing-detailed`와 동일 위상). 착수 금지 목록의 "전투 중심 스킬 트리"는 **플레이어** 스킬 트리이며, 이 설계의 `SkillItem`은 **몬스터** 스킬이라 무관.

### 치환표 (지시서의 범용 템플릿 문구 → 이 프로젝트)
| 지시서 문구 | 이 프로젝트 |
|---|---|
| Camp & Craft / `CampCraft.Combat` | Camp Lantern / `CampLantern.Combat` |
| `Docs/전투_시스템_세부_기획서_v0.1.md` | `.claude/domain/gdd/전투_시스템_구현_지시서.md` (인라인 규칙) |
| XR Interaction Toolkit | **Meta XR Interaction SDK** (`Grabbable`/`GrabInteractable`, 리그는 `OVRComprehensiveInteractionRig`) |
| 0단계 "CLAUDE.md 작성" | 이 README의 **공통 규약**으로 흡수 (CLAUDE.md 이미 존재 — 새로 쓰지 않는다) |
| "[Photon Fusion / PUN2 / NGO]" (8단계) | **Photon Fusion 2 Shared Mode** (`tech-stack-decisions.md`) |
| "에디터 수동 작업 체크리스트" | 가능한 항목은 EditorTools 팩토리로 자동화(RULE-02 준수 경로), 자동화 불가 항목만 수동 체크리스트로 보고 |

## 아키텍처 결정
- **새 영역 `Combat` 신설**: `Assets/Scripts/Combat/{Data, Weapons, Monsters, Player}`, 네임스페이스는 폴더 1:1 (`CampLantern.Combat`, `.Data`, `.Weapons`, `.Monsters`, `.Player` — Core/Networking의 서브폴더=서브네임스페이스 관례를 따름). 공용 계약(`IDamageable`/`HitInfo`/`WeaponKind`)은 `Combat` 루트. asmdef 신설 금지(RULE-01) — 단일 Assembly-CSharp 유지.
- **데이터는 ScriptableObject** (낚시의 plain C# 하드코딩과 다름): 지시서 1단계가 SO를 명시 요구하고, 프로젝트에 이미 SO Def + Editor 팩토리 체계(P0DataFactory)가 있다. `.asset` 생성은 `CombatDataFactory`(Editor)로만(RULE-02). **Id/primary key 필드는 넣지 않고**(지시서 방침 — 정식 스키마 때 일괄 부여) ContentRegistry에도 등록하지 않는다(전투 데이터는 저장 파일에서 참조되지 않음).
- **판정 규약** (지시서 0단계 규칙 7 — 아래 공통 규약에 전문 수록): 물리 충돌 콜백 금지, 매 틱 능동 쿼리, 콜라이더는 피격 볼륨 정의 전용, 터널링 금지. 구체 기법(스윕/오버랩/거리 계산)은 각 단계 에이전트가 선택하고 **근거를 주석으로 남긴다**.
- **RULE-03과의 정합**: 물리 *쿼리*(Raycast/Overlap/SphereCast 등)는 읽기 전용이라 Update/렌더 틱에서 허용된다. RULE-03이 금지하는 것은 Rigidbody 상태 *조작*(AddForce/velocity/MovePosition)을 FixedUpdate 밖에서 하는 것 — 화살 등 Rigidbody 사용 시 힘/속도 조작은 FixedUpdate에서만.
- **약점 배율은 수신 측(몬스터)이 적용**: 무기는 `HitInfo{BaseDamage, IsWeakpoint, ...}`를 보내고, `MonsterHealth.ApplyDamage`가 자기 `MonsterData.weakpointMultiplier`를 곱한다. 근거 — 배율 데이터의 소유자가 몬스터이고, 8단계 "클라 감지 → 호스트가 데미지 최종 확정"과 경계가 일치한다.
- **데미지 적용 단일 지점**: 모든 데미지는 `IDamageable.ApplyDamage(in HitInfo)` 한 곳으로(지시서 규칙 5). 이 지점이 step-12 네트워크 분리 경계이며, 기존 `HuntTarget.ApplyHit`(비권한 클라 → RPC → State Authority 적용, [HuntTarget.cs](../../../Assets/Scripts/Hunting/HuntTarget.cs))가 통합 시 참조 모델이다.
- **기존 `Hunting/`은 step-12 전까지 수정 금지**: `HuntTarget`/`HuntLedger`/`HuntZoneHarness`는 P0 검증 라인으로 그대로 두고, 전투는 로컬 샌드박스 씬에서 완성한다. 통합 시 보존할 계약 — `TryStartHunt` 2인 게이트(social-cooperation ②), `HuntLedger` 기여(유효 행동 1회 이상=참여)·`RewardGranted` 공유 보상, `HuntTargetDef.RewardMaterials`(사냥 보상은 코인이 아닌 고유 재료).
- **무기 아트 연계**: `Assets/Prefabs/Weapons/`(Sword/Bow/Arrow/Spear)는 `WeaponPrefabFactory`가 팀원 임포트 팩을 감싼 산출물 — 현재 비주얼+Meta 그랩만 있다. 전투 컴포넌트 부착은 각 단계에서 EditorTools 배선 팩토리로 한다. **주의**: `Weapons (Force Recreate)` 실행 시 배선이 유실되므로 배선 팩토리는 멱등(재실행 복구 가능)으로 만든다.
- **곰 아트 연계**: `HuntTarget_Bear.prefab`(네트워크 프리팹)은 건드리지 않고, 로컬 전투용 `CombatMonster_Bear.prefab`을 별도 생성(Bear_4 비주얼 + `BearAnimator.controller`). 컨트롤러 트리거 파라미터(Idle/Combat Idle/Run Forward/WalkForward/Attack1~8/Get Hit Front/Stunned Loop/Death)가 FSM과 1:1 대응 — 매핑은 step-07 `MonsterAnimationDriver`. **판정은 애니메이션과 무관하게 데이터(hit window)로만** — 애니메이션 이벤트 금지(지시서 5단계).
- **코어 로직은 입력 무관, VR은 어댑터**(낚시와 동일 구조): 근접 스윙 속도는 Transform 프레임 차분(물리 velocity 의존 금지 — 지시서), 활 당김은 `SetDrawRatio(float)` 같은 입력 무관 진입점으로. Meta SDK 양손 인터랙션·햅틱 배선은 각 단계 수동 체크리스트/후속.
- **에디터 배선 동반 원칙**: 에셋·프리팹·씬 배선이 필요한 단계는 해당 단계에서 `EditorTools` 팩토리를 함께 생성/확장한다(이 프로젝트의 확립된 패턴 — FishResultPanelFactory 등). "한 단계 = 한 영역"의 예외로 명시 허용.

## 터치 영역
| 영역 | 경로 | 역할 |
|---|---|---|
| Combat | `Assets/Scripts/Combat/` | 데이터(SO)·무기·몬스터 AI·플레이어 규칙 (대부분, **신설**) |
| EditorTools | `Assets/Scripts/Editor/` | 데이터 에셋·프리팹 배선·샌드박스 씬 팩토리 (각 단계 동반) |
| Bootstrap | `Assets/Scripts/Bootstrap/` | `CombatSandboxHarness` IMGUI 검증 (step-11) |
| Hunting/Networking | `Assets/Scripts/Hunting/`, `Networking/` | **step-12 전까지 수정 금지** — 통합 단계에서만 |
| Core | `Assets/Scripts/Core/` | 참조만 (`Wallet` — Blacksmith 스텁, `HuntTargetDef` — step-12) |

## 의존성 그래프
```
Combat/Data (SO 스키마) + IDamageable/HitInfo          (step-01)
    │
    ├─ Weapons/MeleeWeapon + DummyMonster               (step-02)
    │       │
    ├─ Monsters/MonsterController FSM + MonsterHealth   (step-03)  ← DummyMonster 대체
    │       │
    │   CombatSandbox 씬 팩토리                          (step-04)
    │       │
    │   Monsters/AggroController                        (step-05)
    │       │
    │   Monsters/스킬 + Attack/Exhausted + Stunned       (step-06)  → Player/PlayerHealth 최소판
    │       │
    │   Monsters/MonsterAnimationDriver (곰 트리거)      (step-07)
    │
    ├─ Weapons/Bow·Arrow                                (step-08)  ← step-01만 선행, 03~07과 병렬 가능
    │
    ├─ Player/다운·소생·전멸 + HP 스케일링·기여           (step-09)
    ├─ Player/홀스터 + 내구도 귀환 + Blacksmith 스텁      (step-10)
    │
    Bootstrap/CombatSandboxHarness                      (step-11)
    │
    네트워크 권한 분리 + HuntTarget/HuntLedger 통합       (step-12, 마지막 — 착수 전 재분해)
```

## 단계
1. [step-01-combat-data.md](step-01-combat-data.md) — Combat/Data: SO 스키마 + `IDamageable`/`HitInfo` + 테스트 에셋 팩토리 (지시서 1단계)
2. [step-02-combat-melee.md](step-02-combat-melee.md) — Combat/Weapons: 근접 유효타 판정 + DummyMonster + 무기 프리팹 배선 (지시서 2단계)
3. [step-03-combat-monster-fsm.md](step-03-combat-monster-fsm.md) — Combat/Monsters: 몬스터 FSM(Idle/Chase/Return) + 오버레이 구조 + 곰 프리팹 (지시서 3단계)
4. [step-04-editor-sandbox.md](step-04-editor-sandbox.md) — EditorTools: CombatSandbox 로컬 검증 씬 팩토리
5. [step-05-combat-aggro.md](step-05-combat-aggro.md) — Combat/Monsters: 어그로 6규칙 (지시서 4단계)
6. [step-06-combat-skills.md](step-06-combat-skills.md) — Combat/Monsters: 스킬 시스템 + Attack/Exhausted + Stunned 실구현 (지시서 5단계)
7. [step-07-combat-monster-anim.md](step-07-combat-monster-anim.md) — Combat/Monsters: 곰 애니메이션 드라이버 + animDuration 동기화
8. [step-08-combat-bow.md](step-08-combat-bow.md) — Combat/Weapons: 활/화살·회수 (지시서 6단계)
9. [step-09-combat-player-rules.md](step-09-combat-player-rules.md) — Combat/Player: 다운·소생·전멸 + HP 스케일링·기여 (지시서 7단계 전반)
10. [step-10-combat-holster.md](step-10-combat-holster.md) — Combat/Player: 홀스터·내구도 귀환·Blacksmith 스텁 (지시서 7단계 후반)
11. [step-11-bootstrap-harness.md](step-11-bootstrap-harness.md) — Bootstrap: CombatSandboxHarness IMGUI + 로컬 종합 검증
12. [step-12-combat-network.md](step-12-combat-network.md) — 네트워크 권한 분리 + Hunting 통합 (지시서 8단계, **착수 전 상세 재분해**)

## 병렬 실행 가능성
- **step-01 → 이후 전부** 순차 필수 (모든 단계가 Data 어휘를 참조).
- step-02와 step-03은 지시서상 병렬 가능하나, step-03이 step-02의 DummyMonster를 대체하므로 **02 → 03 순차 권장** (병렬 시 대체 작업만 03 마지막에).
- **step-08(활)은 step-01 직후 언제든 병렬 가능** — 몬스터 라인(03~07)과 파일이 겹치지 않는다. 판정 대상은 `IDamageable`이면 충분.
- step-04는 02·03 완료 후. step-05 → 06 → 07 순차 (같은 Monsters 파일 확장).
- step-09는 06(PlayerHealth 최소판) 후, step-10은 02(WeaponBroken)·08(활 슬롯 타입) 후. 09와 10은 서로 병렬 가능.
- step-11은 09·10 완료 후. step-12는 전부 완료 + 로컬 검증 통과 후에만.

## 공통 규약 (모든 단계)
- **[판정 규약]** 모든 히트/접촉 판정에 물리 충돌 콜백(`OnTriggerEnter`, `OnCollisionEnter`, `OnTriggerStay` 등)을 사용하지 않는다. 판정 주체가 매 틱 능동적으로 검사하는 쿼리 방식(스윕/캐스트/오버랩/거리 계산)으로 통일한다. 콜라이더는 쿼리 대상(피격 볼륨 정의)으로만 사용한다. 시스템별 구체 기법은 구현 에이전트가 선택하되 **선택 근거를 코드 주석으로 남긴다**. 고속 이동체(빠른 스윙, 화살)도 판정 누락이 없어야 한다(터널링 금지).
- **재설계 금지**: 지시서 인라인 규칙(데미지 공식·어그로 6규칙·스킬 선택·활 계산식 등)은 그대로 구현한다. "네가 판단/선택"으로 위임된 항목만 에이전트 재량(근거 설명 의무). 이견이 있으면 아키텍트에 보고 후 계획 갱신.
- **수치 하드코딩 금지**: 밸런스 수치는 전부 SO 필드(지시서 규칙 4). 지시서가 준 임시값은 SO 기본값으로 넣고 `// TODO(TUNING)` 주석. 스펙 미정 항목(수리 단가·화살 가격·상태이상 등)은 자리만 만들고 `// TODO(SPEC)` + `[Header("미사용 - 추후")]`.
- **네트워크 코드 작성 금지 (step-12 전)**: 단, 데미지 적용은 `IDamageable.ApplyDamage` 한 곳으로 모아 분리 지점을 유지한다(지시서 규칙 5).
- **컨벤션**: 네임스페이스 `CampLantern.Combat[.Sub]`, `m_`/`k_` 접두사, `[SerializeField]`+프로퍼티, 이벤트 `+=` 전 `-=`·`OnDestroy/OnDisable` 해제, Awake 코드 초기화 (rules/scripts.md). 한글 주석. 스펙 참조 주석: `// Spec: 지시서 N단계 (§X-X)`.
- **RULES**: RULE-01(asmdef 신설·InitializeOnLoad 금지), RULE-02(.asset/.prefab/.unity 직접 편집 금지 — 전부 EditorTools 팩토리 경유), RULE-03(Rigidbody 조작은 FixedUpdate — 쿼리는 예외), RULE-04(ProjectSettings 불가침). VR 90Hz — Update 경량 유지.
- **검증**: 각 단계 (1) 컴파일 0 에러, (2) 가능하면 ClaudeBridge 플레이 스모크 — 런북은 [`knowledge/unity-editor-automation.md`](../../knowledge/unity-editor-automation.md). step-04 이후는 CombatSandbox 씬에서 지시서의 단계별 완료 판정을 재현한다.
- **완료 조건(전체)**: step-11 후 `TODO(TUNING)`/`TODO(SPEC)` 전체 목록과 지시서 대비 차이(있다면 이유)를 정리해 보고. `/task-done`으로 판정 규약·전투 구현 결정을 도메인 문서(`domain/combat-system.md` 신설)로 승격 제안.

## 첫 단계 착수
```
/task-start
```
그다음 [step-01-combat-data.md](step-01-combat-data.md)의 지시를 수행.
