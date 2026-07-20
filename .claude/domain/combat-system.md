# 전투 시스템 (구현 결정·암묵지 — 구현 완료 2026-07-10)

## 한 줄 요약
사냥터 전투 전체(근접 유효타·활/화살·몬스터 FSM·어그로·스킬·다운/소생/전멸·홀스터·Fusion 권한 분리)가 지시서 0~8단계대로 구현 완료. 로컬 우선(Combat은 Fusion 무참조) + 네트워크 계층(Hunting/Networking)이 감싸는 구조이며, 검증은 무인 봇 8종이 담당한다.

## 핵심 타입 / 진입점
- **설계 원본**: [`gdd/전투_시스템_구현_지시서.md`](gdd/전투_시스템_구현_지시서.md)(인라인 규칙이 스펙), 분해 계획 `design/combat-detailed/`(로컬 11단계) + `design/combat-detailed-network/`(네트워크 5단계, `VERIFICATION.md` 포함).
- **Combat**(`Assets/Scripts/Combat/`, Fusion 무참조): 계약 `IDamageable`/`HitInfo`/`HitVolume`/`WeaponKind`/`CombatPlayers`/`CombatScenes` — `Data/`(SO 6종) — `Weapons/`(`MeleeWeapon`/`Bow`/`Arrow`/`ArrowQuiver`) — `Monsters/`(`MonsterController` 상태 패턴+`IOverlayState`, `MonsterHealth`, `AggroController`, `SkillRunner`/`SkillHitCheck`, `MonsterAnimationDriver`) — `Player/`(`PlayerHealth`/`PlayerReviver`/`PartyWipeWatcher`/`ScreenTintOverlay`/`WeaponHolster`/`Blacksmith`).
- **네트워크 계층**: `Hunting/NetworkedHuntMonster`(권한 라우터)+`NetworkedPlayerBodies`, `Networking/NetworkedCombatPlayer`+`CombatPlayerSpawner`. 기존 `HuntTarget`/`HuntLedger`는 **무수정 병행**(사슴/멧돼지/구 곰 P0 라인 유지, 네트워크 전투 곰 `HuntMonster_Bear`는 4번째 스폰).
- **에디터 팩토리**: `CombatDataFactory`(테스트 SO), `CombatWeaponWiringFactory`(Sword/Spear/Bow/Arrow 배선 — Weapons Force Recreate 후 재실행 필요), `CombatMonsterPrefabFactory`(로컬 곰+Ensure Components+Sync Skill Anim Durations), `NetworkedHuntMonsterFactory`(네트워크 곰+DebugJoinHuntZone), `CombatPlayerFactory`, `CombatSandboxFactory`(로컬 검증 씬, 멱등 Ensure).

## 구현 결정 (지시서에 없는 세션 결정)
- **판정 규약 채택 형태**: 물리 콜백 전면 금지. 근접=타격부 세그먼트 프레임 간 스윕 캐스트+오버랩 병용(터널링 방지+정지 접촉), 몬스터 Melee 스킬=부채꼴 거리·각도 수학 판정(CombatPlayers 목록 순회 — 콜라이더 불요), 화살=자체 운동학 적분+경로 스윕(비행·판정 단일 소스, RULE-03 소거), 회수/소생=거리 판정. 물리 *쿼리*는 읽기 전용이라 Update 허용(RULE-03은 Rigidbody 조작 규칙).
- **약점 배율은 수신 측(몬스터) 적용** — `HitInfo.BaseDamage`는 배율 미적용. 배율 데이터 소유자가 몬스터이고, 네트워크의 "호스트가 데미지 최종 확정"과 경계 일치.
- **텔레포트 규약(`MeleeWeapon.NotifyTeleported`)**: 무기를 순간이동시키는 모든 주체(홀스터 수납/귀환·스폰 배치)는 호출 필수 — 텔레포트 경로가 스윕에 잡히면 유령 타격. 자동 가드(변위+속도 복합)는 보조일 뿐, 히칭 프레임과 겹친 텔레포트는 속도로 구분 불가(실측).
- **몬스터 이동 = 평면 스티어링(NavMesh 미사용)**: 존이 작고 평탄 + 베이크가 씬 전제조건이 되면 봇 검증·씬 팩토리가 무거워짐. `MoveTowards` 한 곳만 교체하면 이관 가능.
- **플레이어 인지 = `CombatPlayers` 레지스트리**: FindObjectsOfType 매 틱 금지(90Hz)·태그는 RULE-04(TagManager). 다운 플레이어는 타겟 제외. 등록 주체 — 로컬: 하네스/봇, 네트워크: `NetworkedCombatPlayer`(전 클라).
- **권한 분리 구조(§5)**: 감지는 클라(무기 판정 무수정), 적용은 State Authority — 비권한 `ApplyDamage`→RPC(공격자는 자기 `Runner.LocalPlayer` 스탬핑)→권한자가 로컬 `MonsterHealth` 실행(배율·스케일링·기여 재사용). FSM/어그로/스킬은 권한자 전용 `enabled` 게이팅(마스터 이전은 `HasStateAuthority` 폴링 재게이팅). 비권한 표현은 `[Networked] NetStateName`/HP 변화→`MonsterAnimationDriver.ApplyStateByName`/`ApplyHitReaction`.
- **기여 신원**: 원격 공격자는 `NetworkedPlayerBodies`(PlayerRef→몸체) 해석, 미등록이면 PlayerRef당 1:1 자식 프록시 — MonsterHealth의 GameObject 기여 Set/HP 스케일링을 무수정 재사용하기 위한 브리지. 기여·보상 확정은 `HuntLedger`(무수정)로 통일.
- **`MonsterData.huntDef` ↔ `HuntTargetDef`**: 분리+참조 — 보상·필요 인원·저장 Id는 HuntTargetDef, 전투 스탯은 MonsterData. `HuntTargetDef.MaxHealth`는 구 라인 전용(중복 정리는 정식 스키마 때).
- **다운/소생/전멸의 Shared Mode 해석**: 다운/소생은 소유 클라 적용+`[Networked] IsDowned` 동기화, 전멸은 **복제 상태 유도**(전원 IsDowned를 각 클라가 독립 관찰 — RPC 불요, 마스터 확정과 등가).
- **씬 게이트(`CombatScenes`)**: 무기 활성화는 사냥터 씬만 — 씬 이름 기반 임시(지시서 명시). 프리팹 무기만 `RequireCombatScene=true`, 런타임 조립(봇)은 기본 false.
- **애니는 배타적 bool 스위칭**: BearAnimator 파라미터가 전부 Bool(m_Type 4 실측) — SetTrigger 아님. 판정과 완전 분리(드라이버 제거해도 전투 불변), 애니메이션 이벤트 금지.

## 무인 검증 봇 (회귀 재사용)
`MeleeSwingSelfTest`/`MonsterFsmSelfTest`/`AggroSelfTest`/`SkillSelfTest`/`BowSelfTest`/`HolsterSelfTest`/`PlayerRulesSelfTest`(Combat)+`NetworkedHuntSelfTest`(Hunting, 더미 피어 자가 생성 2인). 플레이 중 브릿지 `Component.Add`로 주입, `[태그] RESULT ALL PASS` grep 판정. 씬 무관(격리 좌표 1000~2200), 수치 기대값이 코드 안에 닫혀 있어 수식 회귀까지 검증.
- **격리 조건**: `PlayerRulesSelfTest`는 **단독 플레이 세션 필수** — 전멸 감시가 `PlayerHealth.All` 전역을 봐서 타 봇 플레이어(생존)와 동시 주행 시 오탐(실측 2026-07-10). 나머지 6종은 동시 주행 가능.
- 네트워크 검증 실측: 2클라 동시 타격 HP 정합 350/450(스케일링 포함 결정론) — 상세 절차는 `design/combat-detailed-network/VERIFICATION.md`.

## 새 몬스터 추가 절차
1. `CombatDataFactory`류로 `MonsterData`(+스킬) 에셋 → 2. `CombatMonsterPrefabFactory` 패턴으로 로컬 프리팹(비주얼+피격/약점 볼륨 — 머리 위치는 휴리스틱이라 육안 조정) → 3. 애니 파라미터 매핑을 드라이버 인스펙터/팩토리에서 교체 → 4. 네트워크판은 `NetworkedHuntMonsterFactory` 패턴(+`huntDef` 연결, `RoomScenesFactory.SetHuntPrefabs` 배열 추가) → 5. 봇/샌드박스 회귀.

## 이동 방식 — RVRF식 텔레포트 (GDD 확정, 2026-07-20)
- **사냥터 이동은 텔레포트(위치 지정 + 썸스틱)로 통일** — 목적은 멀미 대응. Meta Interaction SDK의 Locomotor가 이미 Slide(스틱 연속 이동)/Teleport 두 방식을 지원하고 두 방식용 GameObject 세트도 프로젝트 리그(`OVRComprehensiveInteractionRig`)에 배선돼 있지만, `MovingSetting.ControllerMovement` 기본값이 `Slide`라 텔레포트가 켜진 적이 없었다(2026-07-17엔 화면 암전만 고치고 이건 발견 못 함 — 2026-07-20 GDD 재확인 중 드러남).
  - `HuntZoneHarness.SetLocomotionStyle()`이 `Awake()`에서 `Teleport`로, `OnDestroy()`에서 `Slide`로 되돌린다 — 리그가 `DontDestroyOnLoad`라 되돌리지 않으면 사냥터를 나간 뒤에도 텔레포트가 다른 공간에 새어나간다.
  - **P0Harness(단일 씬 통합 테스트)엔 적용 안 함** — 여러 활동이 한 씬에 섞여 있어 "사냥터만 텔레포트"를 재현할 방법이 없다. 필요해지면 재검토.
- **컨트롤러 직접 이동(고릴라 태그식 손 당김 이동)은 채택하지 않는다** — 프로젝트에 애초에 구현돼 있지 않음, GDD가 이 방향을 명시적으로 배제.
- **회피 밸런싱은 별도로 두지 않는다** — 몬스터 공격 애니메이션의 예고(telegraph) 시간을 넉넉히 잡는 쪽으로 난이도를 낮게 유지하는 게 GDD 방침. 별도의 회피 판정/무적 프레임 시스템을 추가하지 말 것 — 스킬 텔레그래프 타이밍(`SkillRunner`/`MonsterAnimationDriver`) 튜닝으로 충분해야 한다.
- **실기(헤드셋) 검증 전** — 코드는 컴파일 확인만 했고, 텔레포트 아크가 실제로 뜨는지·썸스틱 트리거가 맞는지는 플레이 확인 필요.

## 팀 결정 대기 / 후속
- 구 HuntTarget 3종 → 전투 몬스터 교체 여부 (병행 검증 후).
- 사냥 시작(HuntActive) 전 몬스터 FSM 활성(타격만 게이트 — HuntTarget 의미론 계승) — 시작 전 공격 억제 여부.
- 더미 피어 쪽 전투 존재(NetworkedCombatPlayer) 스폰·다운/소생 2피어 실측, 마스터 이전 실측 — 실기 항목.
- Point/SelfAoe/Ranged 몬스터 스킬 실구현, Stunned 원격 표현, 디버프 부여 주체.
- `TODO(TUNING)` 22건(전부 SO/SerializeField — Quest 실기 튜닝)·`TODO(SPEC)` 9건(수리 단가·화살 가격·상태이상·디버프 수치) — `Grep "TODO\((TUNING|SPEC)\)" Assets/Scripts/Combat/`.

## 수정 시 주의
- `WeaponPrefabFactory`의 **Weapons (Force Recreate)는 전투 배선을 유실**시킨다 — 직후 `Combat — Wire Melee Weapons`/`Wire Bow & Arrow` 재실행.
- 곰 프리팹 컴포넌트가 늘면 `Combat Monster — Ensure Components`에 등록(재생성은 skip 정책 — 수동 조정 보존).
- Combat 코어에 Fusion 참조를 넣지 말 것 — 로컬 봇 검증·오프라인 폴백이 깨진다. 네트워크 확장은 Hunting/Networking 계층에서 감싼다.
- 사냥 보상 이벤트가 **두 계보**다: 구 라인 `HuntLedger.RewardGranted`(사슴/멧돼지/구 곰)와 `NetworkedHuntMonster.RewardGranted`(신 곰 — HuntLedger 무수정 계약 때문에 대행 발화). **하네스는 둘 다 구독해야 보상이 인벤토리에 들어간다** (2026-07-13 QA에서 신 곰 미구독 발견·수정 — `HuntZoneHarness.Update` 참조). 새 사냥감/하네스 추가 시 어느 계보인지 확인할 것.
