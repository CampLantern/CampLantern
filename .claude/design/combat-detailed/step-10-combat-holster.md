# Step 10: Combat — 홀스터 + 내구도 0 귀환 + Blacksmith 스텁

- **영역:** `Combat` (`Player/`, `Weapons/` 소폭) + EditorTools 동반(리그 앵커 배선)
- **선행 단계:** step-02 (`MeleeWeapon.WeaponBroken`), step-08 (`Bow.WeaponBroken`, `WeaponKind.Bow`)
- **후행 단계:** step-11(하네스 검증). step-09와 서로 병렬 가능(파일 독립)
- **스펙:** 지시서 7단계 후반 (§1-5 ~ §1-6)

---

## 목적
무기 슬롯 3개 홀스터(타입 검사), 내구도 0 무기의 강제 귀환+사용 불가, 수리 스텁(Blacksmith), 사냥터 씬 제한을 구현한다.

---

## 에이전트 실행 지침

`/task-start` 호출 후, [`전투_시스템_구현_지시서.md`](../../domain/gdd/전투_시스템_구현_지시서.md) **7단계** 본문 중 §1-5·§1-6 부분을 그대로 구현한다.

### 생성/수정 파일
- `Assets/Scripts/Combat/Player/WeaponHolster.cs` — 생성 (`CampLantern.Combat.Player`)
- `Assets/Scripts/Combat/Player/BrokenWeaponHandler.cs` — 생성 (WeaponBroken 구독 → 귀환+사용 불가. 파일 구성은 에이전트 재량 — Holster에 흡수 가능, 근거 설명)
- `Assets/Scripts/Combat/Player/Blacksmith.cs` — 생성 (스텁)
- `Assets/Scripts/Editor/` — 리그/씬에 홀스터 앵커 배선 팩토리 (기존 팩토리 확장 or 신규 — 에이전트 판단)

### 핵심 심볼
```csharp
public class WeaponHolster : MonoBehaviour
{
    // 슬롯 3개: 등(활/검/창 허용), 허리 좌/우(검 전용) — §1-6
    // 좌우 배치는 설정값(기본 오른손잡이 기준) — [SerializeField]
    public bool TryStow(GameObject weapon, WeaponKind kind);   // 슬롯별 허용 타입 검사
    public void ForceReturn(GameObject weapon);                // 내구도 0 강제 귀환 (§1-5)
}

public class Blacksmith : MonoBehaviour
{
    // 스텁 — 코인 차감 + 내구도 복구 메서드만. UI/공간은 추후 (지시서 명시).
    // 수리 단가는 MeleeWeaponData.repairCost(임시 0) — TODO(SPEC)
    public bool TryRepair(MeleeWeapon weapon, CampLantern.Core.Wallet wallet);
}
```

### 구현 규칙 (지시서 그대로)
- **홀스터 (§1-6)**: 슬롯 3개 — 등: `Sword`/`Spear`/`Bow` 허용, 허리 좌/우: `Sword` 전용. 슬롯별 허용 `WeaponKind` 검사. 좌우 배치 설정값(기본 오른손잡이). 수납/인출 판정도 공통 판정 규약(거리 판정 — 물리 콜백 금지).
- **내구도 0 (§1-5)**: step-02/08의 `WeaponBroken` 구독 → 홀스터로 강제 귀환 + 사용 불가 상태(집기 시도 시 거부 — Meta `Grabbable` 비활성 등 기법 선택+근거).
- **수리**: `Blacksmith.TryRepair` — `Wallet`(Core) 차감 + 내구도 복구만. UI/공간 없음.
- **무기 활성화는 사냥터 Scene에서만**: Scene 이름 기반 임시 판정 — **임시임을 주석으로 명시** (지시서 명시). 로컬 검증용으로 CombatSandbox 씬도 허용 목록에 포함.
- 홀스터 앵커(등/허리)는 `PersistentPlayer` 리그를 따라가야 한다 — 리그 자식 배선(팩토리) 또는 추적 컴포넌트 중 에이전트 선택+근거. **PersistentPlayer.prefab 직접 편집 금지** — 팩토리(`AvatarSetupFactory` 패턴)로만.

### 선행 산출물 의존성
- `WeaponKind`/`MeleeWeaponData.repairCost` — step-01. `MeleeWeapon.WeaponBroken`/`IsBroken` — step-02. `Bow.WeaponBroken` — step-08. `Core.Wallet` — 기존 (참조만, 수정 금지).

### 제약
- `Wallet` API는 기존 시그니처 그대로 사용 — Core 수정 금지.
- VR에서 등 뒤 슬롯 인출 UX 세부(각도/거리)는 SerializeField 튜닝(`// TODO(TUNING)`).

### 완료 판정
- [ ] 슬롯 타입 검사: 허리에 활 수납 시도 → 거부, 등에 활 → 성공
- [ ] 내구도 0 → 홀스터 강제 귀환 + 집기 거부, `TryRepair` 후 다시 사용 가능
- [ ] 사냥터 외 씬(Lobby)에서 무기 비활성 (씬 이름 판정 임시 주석 확인)
- [ ] 컴파일 통과 + 앵커 배선 수동 체크리스트 보고

---

## 금지 사항
- 수리 UI/대장간 공간 제작 금지 — 메서드 스텁까지만.
- `PersistentPlayer.prefab`·리그 프리팹 직접 편집 금지 (팩토리 경유).
- step-09 파일(PlayerHealth 등) 수정 금지 — 병렬 실행 안전성.
