# Step 02: Combat — 근접 무기 유효타 판정

- **영역:** `Combat` (`Weapons/`, 임시 표적은 `Monsters/`) + EditorTools 동반(프리팹 배선)
- **선행 단계:** step-01 완료 필요 (`IDamageable`/`HitInfo`/`MeleeWeaponData`)
- **후행 단계:** step-03이 DummyMonster를 대체, step-05가 약점 적중 정보를 소비, step-10이 `WeaponBroken`을 소비
- **스펙:** 지시서 2단계 (§1-2, §1-3)

---

## 목적
손에 쥔 근접 무기(검/창)의 **속도 비례 유효타 판정**을 만든다. 무기 단위 상태머신, 프레임 차분 속도 실측, 무효타 처리, 약점 플래그, 내구도 차감까지. 표적은 임시 `DummyMonster`(HP+로그)로 검증한다.

---

## 에이전트 실행 지침

`/task-start` 호출 후, [`전투_시스템_구현_지시서.md`](../../domain/gdd/전투_시스템_구현_지시서.md) **2단계** 본문 규칙을 그대로 구현한다.

### 생성/수정 파일
- `Assets/Scripts/Combat/Weapons/MeleeWeapon.cs` — 생성 (`CampLantern.Combat.Weapons`)
- `Assets/Scripts/Combat/Monsters/DummyMonster.cs` — 생성 (**임시** — step-03에서 MonsterHealth로 대체 예정 주석 명시)
- `Assets/Scripts/Editor/CombatWeaponWiringFactory.cs` — 생성 (Sword/Spear 배선 메뉴)

### 핵심 심볼
```csharp
public class MeleeWeapon : MonoBehaviour
{
    // 무기 상태머신: Idle → 공격 → 마무리 → Idle (지시서 2단계)
    public MeleeWeaponData Data { get; }              // [SerializeField] m_data
    public int  CurrentDurability { get; }
    public bool IsBroken { get; }
    /// <summary>유효타 발생 — (무기, 판정 정보, 피격 대상). 햅틱/이펙트/사운드 훅 지점.</summary>
    public event Action<MeleeWeapon, HitInfo, IDamageable> ValidHit;
    /// <summary>내구도 0 도달 — 홀스터 강제 귀환은 step-10에서 구독. 지금은 이벤트만.</summary>
    public event Action<MeleeWeapon> WeaponBroken;
}
```

### 구현 규칙 (지시서 2단계 그대로)
- **판정**: 공통 판정 규약(README) — 물리 콜백 금지, 매 틱 능동 쿼리, 터널링 금지. 기법은 에이전트 선택 + **근거 주석 의무** (권장 후보: 타격부 기준점 두 개(자루→끝)의 프레임 간 스윕 캐스트 — 선택은 자유).
- **속도 실측**: 물리 엔진 velocity 의존 금지 — 프레임 간 위치 차분 등 자체 계산 (기법 선택+근거).
- **데미지**: `clamp(속도, vMin, vMax) × damageCoeff × damageConst` → `HitInfo.BaseDamage`. **약점 배율은 곱하지 않는다** — 수신 측(몬스터) 소관 (README 결정).
- **무효타**: 속도 < vMin — 데미지 0, `ValidHit` 미발화(사운드/이펙트/햅틱 훅 호출 금지).
- **판정 규칙 (§1-2)**: 몬스터별 첫 접촉 1회. 대상이 다르면 각각 새 공격. 같은 몬스터의 몸통→머리 훑기는 첫 접촉 부위만 인정. 접촉에서 완전히 벗어난 몬스터는 재공격 가능 목록 복귀.
- **약점**: 머리 부위 식별 방식(별도 쿼리 볼륨/본 기준 반경 등)은 에이전트 선택+근거. `HitInfo.IsWeakpoint`에 기록.
- **내구도**: 유효타 1회당 -1. 0이면 `WeaponBroken` 발화.
- **DummyMonster**: `IDamageable` 구현 — HP, 피격 로그(속도/데미지/약점 여부) 콘솔 출력, 약점 배율(임시 1.5f) 적용 예시 포함.

### EditorTools 동반 — `CombatWeaponWiringFactory`
- 메뉴 `Tools > Make Assets > Combat — Wire Melee Weapons`: `Assets/Prefabs/Weapons/Sword.prefab`·`Spear.prefab`에 `MeleeWeapon` 부착 + `Assets/Data/Combat/` 데이터 참조 주입 + 타격부 기준점 자식 Transform 생성. **멱등**(재실행 시 기존 배선 갱신) — `WeaponPrefabFactory`의 Force Recreate가 배선을 날려도 이 메뉴 재실행으로 복구.
- 더미 몬스터 테스트 프리팹(`Assets/Prefabs/Combat/DummyMonster.prefab` — 캡슐+머리 약점 볼륨)도 이 팩토리로 생성.
- RULE-02: `PrefabUtility.LoadPrefabContents`/`SaveAsPrefabAsset`만 사용 (기존 팩토리 패턴).

### 선행 산출물 의존성
- `IDamageable`, `HitInfo`, `MeleeWeaponData` — step-01.

### 제약
- RULE-03: 판정 쿼리는 읽기 전용이라 Update 허용. Rigidbody 조작이 필요하면 FixedUpdate.
- 그랩(잡기)은 기존 Meta `Grabbable`/`GrabInteractable`을 그대로 사용 — 재구현 금지.
- VR 90Hz: 매 틱 쿼리는 무기당 1~2회 캐스트 수준으로 경량 유지.

### 완료 판정
- [ ] `Grep "class MeleeWeapon" Assets/Scripts/Combat/Weapons/` 정의 확인
- [ ] 컴파일 통과 + 판정 기법 선택 근거 주석 존재
- [ ] (가능 시 ClaudeBridge) 더미에 휘두르면 속도 비례 데미지 로그, 살살 대면 무효(로그에 무효 표시), 머리 적중 시 ×1.5 로그 — 씬이 없으면 step-04 후 소급 검증
- [ ] 자동화 못한 수동 작업 체크리스트 보고 (그립 위치 조정, 약점 볼륨 위치 등)

---

## 금지 사항
- `OnTriggerEnter`/`OnCollisionEnter`/`OnTriggerStay` 등 물리 콜백 사용 금지 (공통 판정 규약).
- 몬스터 FSM·어그로 로직 선작업 금지 — 이 단계는 무기 쪽 판정까지만.
- `Rigidbody.velocity`로 스윙 속도 측정 금지 (지시서 명시).
