# Step 05: Combat — 어그로 시스템 (근접 타겟팅 + 약점 고정/감쇄)

- **영역:** `Combat` (`Monsters/`)
- **선행 단계:** step-02 (약점 적중 정보 = `MonsterHealth.Damaged`의 `HitInfo.IsWeakpoint`), step-03 (MonsterController가 타겟을 조회할 지점)
- **후행 단계:** step-06(스킬 선택이 CurrentTarget 사용), step-09(다운 시 재계산 훅 연결), step-12(호스트 전용 실행으로 이동)
- **스펙:** 지시서 4단계 (§4-3) — 규칙 6개

---

## 목적
`AggroController`를 분리 구현하고 `MonsterController`가 타겟을 여기서 조회하도록 교체한다(step-03의 임시 "가장 가까운 플레이어" 로직 대체). 규칙 6개를 전부 구현한다.

---

## 에이전트 실행 지침

`/task-start` 호출 후, [`전투_시스템_구현_지시서.md`](../../domain/gdd/전투_시스템_구현_지시서.md) **4단계** 본문 규칙 6개를 그대로 구현한다.

### 생성/수정 파일
- `Assets/Scripts/Combat/Monsters/AggroController.cs` — 생성 (`CampLantern.Combat.Monsters`)
- `Assets/Scripts/Combat/Monsters/MonsterController.cs` — 수정 (타겟 조회를 AggroController로 위임)
- `Assets/Scripts/Editor/CombatMonsterPrefabFactory.cs` — 수정 (곰 프리팹에 AggroController Ensure)

### 핵심 심볼
```csharp
public class AggroController : MonoBehaviour
{
    public Transform CurrentTarget { get; }
    public float LockRemainingSeconds { get; }     // 약점 고정 잔여 — 0 이하면 고정 없음
    /// <summary>유효타 수신 시 호출 — MonsterHealth.Damaged 구독으로 배선. 재계산 트리거(규칙 1)+약점 고정(규칙 3~5).</summary>
    public void NotifyValidHit(GameObject attacker, bool isWeakpoint);
    /// <summary>타겟 다운 시 즉시 재계산 (규칙 6 후반) — step-09에서 PlayerHealth가 호출. 지금은 훅만.</summary>
    public void NotifyTargetDowned(GameObject player);
    /// <summary>Return 진입 시 무조건 리셋 (규칙 6 — 약점 고정보다 우선).</summary>
    public void ResetAggro();
}
```

### 규칙 6개 (지시서 그대로 — 수치는 전부 MonsterData 필드, step-01에 이미 존재)
1. **기본 타겟팅**: 가장 가까운 플레이어. 재계산 트리거는 "새로운 공격(유효타)이 들어올 때".
2. **히스테리시스**: 현재 타겟보다 `aggroHysteresisMeters`(임시 2m) 이상 가까워야 전환 — 프레임 단위 타겟 튐 방지.
3. **약점 고정**: 약점 유효타 시 해당 플레이어에게 `weakpointLockSeconds`(임시 5s) 어그로 고정.
4. **감쇄**: 고정 중 다른 플레이어가 약점 타격 시 잔여 고정시간 −`weakpointDecaySeconds`(임시 1.5s).
5. **감쇄 소진**: 잔여 ≤ 0이 되면, 그렇게 만든 마지막 공격자에게 신규 고정(weakpointLockSeconds).
6. **리셋**: 가장 가까운 플레이어가 추격 반경 밖 → 무조건 즉시 리셋(약점 고정보다 우선) → Return 진입. 타겟 다운 시 즉시 재계산(훅만 — 다운 시스템은 step-09).

### 추가 요구 (지시서)
- 디버그: 현재 타겟·고정 잔여시간을 인스펙터(SerializeField 노출)와 기즈모(`OnDrawGizmosSelected` — 타겟 라인, 반경 원)로 표시.
- 플레이어 목록 공급: step-03의 임시 탐색 방식을 이어받되 한 곳으로 모은다(에이전트 설계 — step-11 하네스가 더미 플레이어를 등록·해제할 수 있는 형태, step-12에서 네트워크 플레이어 목록으로 교체 가능한 형태).

### 선행 산출물 의존성
- `MonsterHealth.Damaged(HitInfo)` — step-03. `HitInfo.IsWeakpoint`/`Attacker` — step-01. `MonsterData` 어그로 필드 3종 — step-01.

### 제약
- 거리 재계산은 유효타 트리거 + 필요 최소 빈도만 — 매 프레임 전 플레이어 거리 정렬 금지(90Hz).
- rules/scripts.md: `Damaged` 구독은 `-=` 후 `+=`, `OnDestroy` 해제.

### 완료 판정
- [ ] `Grep "class AggroController" Assets/Scripts/Combat/Monsters/` 확인
- [ ] 컴파일 통과, MonsterController의 임시 타겟팅 코드 제거 확인
- [ ] CombatSandbox(더미 플레이어 2개 마커 활용 or 임시 구동 코드)에서: 근접 타겟팅·약점 고정·감쇄·소진 시 타겟 스틸이 로그로 확인
- [ ] 기즈모로 현재 타겟·고정 잔여 확인 가능

---

## 금지 사항
- 규칙 6개의 수치·우선순위를 임의 변경 금지 (특히 규칙 6이 약점 고정보다 우선하는 순서).
- 스킬/Attack 상태 선작업 금지 — step-06 몫.
