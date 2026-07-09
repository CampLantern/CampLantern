# Step 07: Fishing — VR 입력·햅틱 어댑터

- **영역:** `Fishing` (`Assets/Scripts/Fishing/`)
- **선행 단계:** step-06 완료 (코어 진입점 `Cast/Chamjil/SetReeling/OnSwing/Hook` 확정)
- **후행 단계:** step-08에서 타겟 획득 스텁을 `FishingSpot` 질의로 교체

---

## 목적
스펙 §8 입력표를 코어 API에 바인딩하는 **얇은 어댑터**를 만든다: 캐스팅(스윙+릴리즈), 챔질(들어올리기), 릴링(**트리거 홀드**), 비늘털이 스윙(좌/우 각속도+방향 전환), 낚아올림(들어올리기). 줄 색↔햅틱 동기화(흰=약한 릴링 진동, 빨강=강한 경고, 초록=성공 패턴)로 화면을 응시하지 않아도 되는 대화 병행 원칙(§8)을 구현한다. **게임 로직은 넣지 않는다** — 감지→코어 호출만.

---

## 에이전트 실행 지침

`/task-start` 후 §8 입력표·§7-5 스윙 조건·구현 지침 [3](스윙 임계값 TODO(TUNING)) 기준.

### 생성 파일
- `Assets/Scripts/Fishing/FishingRodInput.cs` — 생성 (VR 입력 어댑터 + 햅틱)

### 핵심 심볼
```csharp
public class FishingRodInput : MonoBehaviour
{
    [SerializeField] private FishingRod m_rod;
    [SerializeField] private FishingTuning m_tuning;                       // 스윙 임계값 등 (step-01)
    [SerializeField] private OVRInput.Controller m_controller = OVRInput.Controller.RTouch;

    // ── 감지 (Update, 90Hz 경량) ──
    // 릴링: OVRInput.Get(Axis1D.PrimaryIndexTrigger, m_controller) > 임계 → rod.SetReeling(true/false)
    // 스윙: OVRInput.GetLocalControllerAngularVelocity(m_controller) — 좌/우(수직축) 각속도가
    //        m_tuning.swingMinAngularSpeed 이상 + 부호 반전 m_tuning.swingMinDirChanges 회 → fish.OnSwing()
    //        // TODO(TUNING): VR 실기 테스트로 임계값 확정 (오탐/미탐 균형)
    // 챔질/낚아올림: 위로 들어 올리기 — 상향 피치 각속도(or 상향 속도)가 임계 이상. 챔질(Bite 중)과
    //        낚아올림(Hooked 중)은 같은 제스처, 물고기 상태로 구분 → rod.Chamjil() / fish.Hook()
    // 캐스팅: 스윙 중 릴리즈(트리거/그립 해제) — 타겟은 아래 타겟 획득으로

    // ── 타겟 획득 (임시 스텁) ──
    // TODO: step-08에서 FishingSpot.TryGetNearestFish(...)로 교체.
    //       이 단계에서는 FindFirstObjectByType<Fish>() 또는 [SerializeField] Fish 참조로 대체.

    // ── 햅틱 ──
    // Fish.LineColorChanged 구독: White=약(저진폭 지속), Red=강(고진폭 경고), Green=성공 패턴(짧은 펄스 반복)
    // OVRInput.SetControllerVibration(freq, amp, m_controller)는 ~2초 후 자동 정지 — Update에서 주기 갱신.
    // 구독은 += 전 -= / OnDestroy·OnDisable 해제 (rules/scripts.md).

    // ── 데스크톱 폴백 (개발용) ──
    // HMD/컨트롤러 부재 시(OVRInput 값 0) 키보드로 동작 검증: 예) C=캐스팅, 스페이스=챔질/낚아올림,
    // 마우스 좌클릭 홀드=릴링, A/D=스윙. IMGUI 하네스와 충돌하지 않게 단순 키만. Quest 빌드 전 제거 대상 주석.
}
```

### 선행 산출물 의존성
- `FishingRod.Cast/Chamjil/SetReeling` — step-03/04
- `Fish.OnSwing/Hook`, `Fish.LineColorChanged` — step-05/06/02
- `FishingTuning.swingMinAngularSpeed/swingMinDirChanges` — step-01

### 제약
- **어댑터는 얇게**: 감지→호출만. 상태 판정(챔질 유효 여부 등)은 전부 코어(`Fish`/`FishingRod`)가 한다 — 어댑터는 무조건 호출하고 코어가 무시.
- 스윙 임계값·제스처 임계 전부 `FishingTuning` 경유 + `TODO(TUNING)` (구현 지침 [3] — VR 실기 확인 전 필수 태그).
- 컨트롤러 부재(에디터·시뮬레이터 없이)에도 **NRE 없이** 동작해야 함 — OVRInput은 값 0을 반환하므로 안전하지만, null 참조(m_rod 등)는 가드.
- 햅틱 호출은 상태 변화 시+주기 갱신만 — 매 프레임 새 패턴 계산 금지(90Hz).
- RULE-03: 물리 API 미사용(각속도는 OVRInput 조회) — FixedUpdate 불필요.

### 완료 판정
- [ ] `Grep "FishingRodInput" Assets/Scripts/Fishing/` 정의 확인
- [ ] `Grep "TODO(TUNING)" Assets/Scripts/Fishing/FishingRodInput.cs` — 스윙 임계값 태그 확인
- [ ] 릴링이 **트리거 홀드**(크랭크 아님, §8)로 구현됐는지 확인
- [ ] 컴파일 + ClaudeBridge 플레이 스모크: 컨트롤러 없는 에디터 플레이에서 예외 0, 데스크톱 폴백 키로 Cast→Chamjil 경로 도달(로그)

---

## 금지 사항
- 게임 로직(판정·수식·상태 전이) 구현 금지 — 코어 소관.
- 타겟 획득을 FishingSpot에 구현 금지 — step-08. 여기서는 임시 스텁 + 교체 주석만.
- 기존 IMGUI 하네스 수정 금지 — step-09.
