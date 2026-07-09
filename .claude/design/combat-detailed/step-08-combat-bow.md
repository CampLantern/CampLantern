# Step 08: Combat — 활 / 화살 (당김 기반 초속·경로 판정·회수)

- **영역:** `Combat` (`Weapons/`) + EditorTools 동반(프리팹 배선)
- **선행 단계:** step-01만 필수 — **step-02~07과 병렬 가능** (파일 독립, 판정 대상은 `IDamageable`이면 충분)
- **후행 단계:** step-10(홀스터 등 슬롯: 활 허용), step-11(하네스 화살 수량 표시)
- **스펙:** 지시서 6단계 (§1-4)

---

## 목적
당김 비율 기반 발사 속도, 중력 궤적, 경로 기반 명중 판정, 몬스터에 박힘/회수, 소지 수량 관리를 구현한다. 코어 로직은 입력 무관(`SetDrawRatio`) — VR 양손 인터랙션 연결은 배선/체크리스트로 분리한다.

---

## 에이전트 실행 지침

`/task-start` 호출 후, [`전투_시스템_구현_지시서.md`](../../domain/gdd/전투_시스템_구현_지시서.md) **6단계** 본문 규칙을 그대로 구현한다.

### 생성/수정 파일
- `Assets/Scripts/Combat/Weapons/Bow.cs` — 생성 (`CampLantern.Combat.Weapons`)
- `Assets/Scripts/Combat/Weapons/Arrow.cs` — 생성
- `Assets/Scripts/Combat/Weapons/ArrowQuiver.cs` — 생성 (소지 수량 — 파일 구성은 에이전트 재량이되 수량 관리는 한 곳으로)
- `Assets/Scripts/Editor/CombatWeaponWiringFactory.cs` — 수정 (Bow/Arrow 프리팹 배선 메뉴 추가)

### 핵심 심볼
```csharp
public class Bow : MonoBehaviour
{
    public BowData Data { get; }
    public int CurrentDurability { get; }
    /// <summary>당김 비율 0~1 — 입력 무관 진입점. VR에서는 시위 손-그립 거리로 산출해 주입.</summary>
    public void SetDrawRatio(float ratio);
    public bool TryFire();   // 화살 소지 0이면 실패. 발사 1회당 활 내구도 -1.
    public event Action<Bow> WeaponBroken;   // step-10 홀스터 귀환 구독
}

public class Arrow : MonoBehaviour
{
    public ArrowData Data { get; }
    public void Launch(Vector3 origin, Vector3 direction, float v0);
    // 명중 시: 몬스터에 박힘(부모 연결) + 회수 대기. 빗나가 지면 등 접촉 시 소멸.
}

public class ArrowQuiver : MonoBehaviour
{
    public int Count { get; }                 // 최대 CombatBalanceData.maxArrows(30)
    public bool TryConsume();                 // 발사 시
    public bool TryRetrieve(Arrow stuck);     // 박힌 화살 회수 — 만땅이면 false(무반응)
    public event Action<int> CountChanged;    // 화살집 비주얼 갱신 훅
}
```

### 구현 규칙 (지시서 그대로)
- **발사 속도**: `v0 = E(탄력성) × d(당김 비율 0~1) − c × W(화살 무게)` — c는 `CombatBalanceData.bowDrawConstC`.
- **비행/판정**: 명중 감지는 물리 콜백이 아닌 **이동 경로 기반 쿼리**(기법 선택+근거 — 빠른 화살 판정 누락 금지). 중력 궤적은 유지 — 물리 시뮬레이션(Rigidbody) 이용 여부는 에이전트 선택(판정만 규약 준수). **Rigidbody 채택 시 힘/속도 조작은 FixedUpdate만(RULE-03).**
- **접촉 시점 실측 속도 v**로 데미지: `clamp(v, arrowVMin, arrowVMax) × K × (1 + b × W)` — K/b/vMin/vMax는 CombatBalanceData. `HitInfo.BaseDamage`로 전달(약점 배율은 수신 측 — 근접과 동일 규칙).
- **박힘**: 명중 시 몬스터에 부모 연결로 고정. 빗나가 지면 등 접촉 시 소멸. **몬스터 사망 후에도 박힌 화살 유지·회수 가능** (step-03 Death가 화살을 파괴하지 않는지 교차 확인).
- **회수**: 박힌 화살-플레이어 손 거리 판정(`arrowRetrieveRadius`) → 즉시 회수: 오브젝트 제거 + 수량 +1 + `CountChanged`. 만땅(30)이면 무반응.
- **소지**: 최대 `maxArrows`(30), 발사 시 차감.

### EditorTools 동반 — CombatWeaponWiringFactory 확장
- 메뉴 `Tools > Make Assets > Combat — Wire Bow & Arrow`: `Bow.prefab`에 `Bow`+`BowData`, `Arrow.prefab`에 `Arrow`+`ArrowData` 배선(멱등). 이후 `CombatSandboxFactory` 재실행으로 씬에 반영.
- VR 양손 당김(시위 손·활 그립 거리) 배선은 수동 작업 체크리스트로 정리 (활 프리팹 시위 비주얼, 화살집 부착 위치 포함).

### 선행 산출물 의존성
- `BowData`/`ArrowData`/`CombatBalanceData`/`IDamageable`/`HitInfo` — step-01. (몬스터 없이도 `IDamageable` 더미로 검증 가능 — step-02~07과 병렬일 때.)

### 제약
- 판정 규약(README) — 물리 콜백 금지. VR 90Hz — 비행 중 화살당 쿼리 1회/틱 수준.
- 화살 오브젝트 풀링은 이번 단계 범위 밖(`// TODO(TUNING)` 주석만 — knowledge/unity-mobile-performance.md 오브젝트 풀 항목 참조).

### 완료 판정
- [ ] 당김 정도에 따라 초속 변화 + 중력 낙차 확인 (CombatSandbox)
- [ ] 명중 시 무게 보너스 데미지 로그, 약점(머리) 적중 시 배율 로그
- [ ] 박힌 화살 회수(+1)/만땅 무반응/빗나간 화살 소멸 동작
- [ ] 몬스터 사망 후 박힌 화살 회수 가능
- [ ] 컴파일 통과 + 판정·비행 기법 근거 주석 존재 + 수동 체크리스트 보고

---

## 금지 사항
- `OnCollisionEnter` 등 물리 콜백으로 명중 감지 금지.
- 근접 무기·몬스터 파일 수정 금지 (병렬 실행 안전성 — Death의 화살 보존 확인은 읽기만, 문제 발견 시 아키텍트 보고).
