# Step 01: Hunting — NetworkedHuntMonster (권한 라우팅·게이팅·2인 게이트·HuntLedger 연동)

- **영역:** `Hunting` (`Assets/Scripts/Hunting/`) + Combat 계약 2건 선행 포함
- **선행 단계:** 없음 (combat-detailed step-01~11 완료 전제)
- **후행 단계:** step-02(프리팹 배선), step-03(플레이어 RPC 대상), step-04(표현 동기화 — 이 클래스의 [Networked] 상태 사용)
- **스펙:** 지시서 8단계 (§5) + step-12 고정 방침

---

## 목적
전투 몬스터의 네트워크 두뇌를 만든다 — **감지는 클라, 적용은 권한자**. 비권한 클라의 `ApplyDamage`를 RPC로 권한자에 전달하고, 권한자는 로컬 `MonsterHealth`를 그대로 실행한다(약점 배율·HP 스케일링·기여 재사용). FSM/어그로/스킬 컴포넌트는 권한자에서만 활성. `TryStartHunt` 2인 게이트와 `HuntLedger` 기여·보상 계약을 계승한다.

---

## 에이전트 실행 지침

`/task-start` 호출 후 수행. [`HuntTarget.cs`](../../../Assets/Scripts/Hunting/HuntTarget.cs)의 RPC/ChangeDetector/게이트 패턴을 참조하되 **수정하지 않는다**.

### 생성/수정 파일
- `Assets/Scripts/Combat/HitVolume.cs` — 수정: `OverrideOwner` API (계약 선행)
- `Assets/Scripts/Combat/Data/MonsterData.cs` — 수정: `huntDef` 참조 필드 (계약 선행)
- `Assets/Scripts/Hunting/NetworkedHuntMonster.cs` — 생성 (`CampLantern.Hunting`)

### 핵심 심볼
```csharp
// HitVolume.cs 추가 — 같은 GO에 IDamageable이 복수일 때(NetworkedHuntMonster + MonsterHealth)
// 수신자를 명시 지정. null이면 기존 GetComponentInParent 해석 유지 (로컬 폴백 불변).
public void OverrideOwner(IDamageable owner);

// MonsterData.cs 추가
[Tooltip("사냥 통합 — 보상·필요 인원·Id는 HuntTargetDef 소관 (전투 스탯만 MonsterData). null이면 순수 로컬 몬스터")]
public HuntTargetDef huntDef;

// NetworkedHuntMonster.cs — HuntTarget의 계승자 (기존 HuntTarget은 무수정)
[RequireComponent(typeof(MonsterHealth))]
public class NetworkedHuntMonster : NetworkBehaviour, IDamageable
{
    [Networked] public int NetCurrentHp { get; set; }
    [Networked] public int NetMaxHp { get; set; }
    [Networked] public NetworkBool HuntActive { get; set; }   // TryStartHunt 게이트 (§ social-cooperation ②)
    [Networked, Capacity(24)] public string NetStateName { get; set; } // step-04 표현 동기화용

    public MonsterHealth Health { get; }       // 권한자 전용 실체
    public event Action<NetworkedHuntMonster> Defeated;  // HuntTarget.Defeated와 동일 계약 — 각 클라 1회

    // IDamageable — HitVolume.OverrideOwner로 피격 수신자가 된다
    bool IDamageable.IsDamageable { get; }     // HuntActive && NetCurrentHp > 0 (+권한자면 Health 위임)
    void IDamageable.ApplyDamage(in HitInfo hit);
    // 권한자: Health.ApplyDamage 직행 / 비권한: RPC_ApplyHit(Runner.LocalPlayer, baseDamage, isWeakpoint)

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_ApplyHit(PlayerRef attacker, int baseDamage, NetworkBool isWeakpoint);

    public bool TryStartHunt();                // HuntTarget.TryStartHunt와 동일 — RequiredParticipants는 Data.huntDef
}
```

### 구현 규칙
- **권한 게이팅**: `Spawned()`에서 `Object.HasStateAuthority` 기준으로 `MonsterController`/`AggroController`/`SkillRunner`/`SkillHitCheck` `.enabled` 토글. 권한 이전(마스터 이탈) 대응 — Fusion Shared의 `StateAuthorityChanged` 시점에 재게이팅 (기법 확인 후 선택, 근거 주석).
- **권한자 데미지 경로**: `Health.ApplyDamage`(로컬 로직 그대로 — 약점 배율·스케일링·기여 Set) 후 `NetCurrentHp/NetMaxHp`에 미러링. `Health.Damaged` 구독으로 어그로는 기존처럼 동작(권한자 전용).
- **기여 = HuntLedger**: 같은 GO의 `HuntLedger.RecordContribution(attacker, Hit)` — attacker는 RPC 인자/권한자 로컬 히트는 `Runner.LocalPlayer`. `HuntLedger`는 부착만, 무수정. HitInfo.Attacker(GameObject)→PlayerRef 변환은 이 클래스에서: **감지한 클라가 자기 LocalPlayer를 스탬핑**한다 (§5 "공격자" 직렬화).
- **처치**: `NetCurrentHp` ChangeDetector로 각 클라에서 `Defeated` 1회 발화(HuntTarget.Render 패턴) → HuntLedger.RewardGranted 경로 보존을 위해 HuntLedger가 구독할 수 있도록 이벤트 시그니처 주의 — HuntLedger는 `HuntTarget.Defeated`를 구독하므로 무수정 원칙과 충돌: **HuntLedger 구독은 하네스/이 클래스가 대행** — 처치 시 참여자 각자 클라에서 `RewardGranted` 상당 로직이 돌도록 `HuntSucceededNetworked(HuntTargetDef)` 이벤트 발화(로컬 판정: `ledger.IsParticipant(Runner.LocalPlayer)`). 계약 보존이 목적 — 정확한 배선 방식은 에이전트가 선택하고 근거 설명(HuntLedger 무수정 유지가 우선).
- **TryStartHunt**: `Data.huntDef.RequiredParticipants` 미만이면 시작 불가, `HuntActive` 게이트로 시작 전/처치 후 타격 무효(HuntTarget과 동일). `Health.ResetToFull` + `ledger.ResetForNewHunt()` 재사냥 초기화.
- **`NetStateName`**: 권한자가 `MonsterController.CurrentStateName`을 틱마다(또는 변화 시) 기록 — step-04가 소비. 이번 단계는 기록만.
- **로컬 폴백**: 이 컴포넌트가 없는 몬스터(CombatMonster_Bear)는 기존 경로 그대로 — Combat 코어 수정은 계약 2건뿐.

### 선행 산출물 의존성
- combat-detailed 전 산출물 (MonsterHealth/Controller/Aggro/Skills/HitVolume/HitInfo).
- `HuntTargetDef`/`HuntLedger` — 기존 (무수정, 부착·호출만).

### 제약
- RULE-01/02/03/04. Fusion 참조는 Hunting/Networking 계층만 (Combat 코어 무참조 유지).
- `[Networked]` string은 고정 Capacity — 상태명 길이 주의 ("Attack(Recover)" 15자 < 24).
- 컴파일 후 NetworkBehaviour 위빙 확인 — 스폰 무음 실패 시 Rebuild Prefab Table (tech-stack-decisions).

### 완료 판정
- [ ] `Grep "class NetworkedHuntMonster" Assets/Scripts/Hunting/` + 컴파일 0 에러
- [ ] `HuntTarget.cs`/`HuntLedger.cs` diff 없음 (무수정 확인)
- [ ] 로컬 폴백 회귀: CombatSandbox 봇 7종 여전히 ALL PASS (HitVolume.OverrideOwner null 경로)
- [ ] 실스폰 검증은 step-02 프리팹 이후 (이 단계는 컴파일·회귀까지)

---

## 금지 사항
- HuntTarget/HuntLedger/SessionLauncher 수정 금지. Combat 수정은 명시된 계약 2건만.
- 플레이어 측 동기화(다운/소생) 선작업 금지 — step-03 몫.
