# 낚시 정교화 (정밀 파이팅)

## 한 줄 요약
동물의숲 수준의 단순 캐스팅·챔질에, 물고기 FSM(8상태)·줄 색(흰/빨강/초록)·텐션·비늘털이 QTE를 이식해 "빨간색일 때 릴을 참는 절제" 하나로 수렴하는 정밀 파이팅 낚시를 구현한다.

## 원문 / 근거
- **스펙(구현 준비 완료)**: [`.claude/domain/gdd/낚시_시스템_구현_지시서.md`](../../domain/gdd/낚시_시스템_구현_지시서.md) — 구현 지침(for Claude) + 설계 스펙 §1~§9. **이 문서를 그대로 기준으로 삼는다(재설계 아님).** 상태전이표는 §3-1, 수식은 §7, 데이터 스키마는 §5, VR 입력은 §8, 미결항목은 §9.
- 라우팅: [`.claude/INDEX.md`](../../INDEX.md) Level 2 "낚시 시스템" 항목.
- 도메인: [`resource-loop.md`](../../domain/resource-loop.md) 낚시 섹션.

## 아키텍처 결정
- **FSM은 "물고기"에 둔다 (`Fish`), "낚싯대"는 입력·텐션·내구도 컨트롤러 (`FishingRod`).** 스펙 §3-1이 물고기 FSM이고, 파이팅은 물고기 개체 상태로 진행되므로. 기존 `FishingRod`의 자체 타이밍 FSM(Idle/Casting/Waiting/Biting/Caught/Missed)은 **대체**된다.
- **개체(instance) vs 어종(species) 분리.** 어종 정적 스탯 = `FishSpeciesData`(§5-1), 캐스팅마다 롤되는 개체 스탯(크기/길이/무게/체력) = `FishInstance`(§5-3 공유 품질 롤). 낚싯대 = `RodData`(§5-2).
- **수식은 전부 `FishingFormulas` static 클래스 경유.** §7 확정 수식(체력·텐션·도망간격·도망지속·비늘털이 확률)은 **구현**, "별도 작성 예정"(XP/코인/미끼가·수리가, §9)만 `TODO(FORMULA)` 스텁(`return 1`).
- **임시 데이터(TODO(DATA))**: `FishSpeciesData`/`RodData`는 아직 데이터 시스템이 없으므로 plain C# 클래스로 스크립트 내 하드코딩(어종 3종: 소형/중형/대형). 추후 ScriptableObject 이관 쉽게 설계.
- **튜닝 값(TODO(TUNING))**: 스윙 임계값·입질 윈도우·비늘털이 확률/디버프·텐션 계수·도망 지속 계수 등은 하드코딩 금지, `[SerializeField]`로 노출.
- **보상은 이벤트로 전달, 적용은 하네스/PlayerState.** `Fish`/`FishingRod`는 Core.Wallet/Inventory를 직접 참조하지 않고 `FishCaught(FishInstance)` 이벤트만 발화 — 기존 계약(FishCaught→Inventory) 유지. 코인/XP 적용은 하네스가 담당.
- **VR 입력은 얇은 어댑터로 분리.** 코어 로직(FSM/수식)은 입력 무관하게 테스트 가능하게, VR 컨트롤러 바인딩(스윙 각속도·트리거 홀드·햅틱)은 별도 컴포넌트. IMGUI/데스크톱 조작은 검증용으로 유지.

## 터치 영역
| 영역 | 경로 | 역할 |
|---|---|---|
| Fishing | `Assets/Scripts/Fishing/` | 데이터·수식·물고기 FSM·낚싯대·QTE·결과 UI·VR 입력 (대부분) |
| Bootstrap | `Assets/Scripts/Bootstrap/` | `FishingGroundHarness` 통합·마이그레이션 (마지막) |
| Core | `Assets/Scripts/Core/` | (참조만) `FishDef`, `Wallet`, `Inventory` — 수정 없음 |

## 의존성 그래프
```
Fishing/FishSpeciesData·RodData·FishInstance·FishingFormulas·FishingTuning  (step-01)
        │
        ▼
Fishing/Fish (FSM 스켈레톤)  (step-02)
        │  ├─ 캐스팅·입질 (step-03)
        │  ├─ 파이팅 텐션·체력 (step-04)
        │  ├─ 도망·비늘털이 QTE (step-05)
        │  └─ 후킹·획득·보상 UI (step-06)
        ▼
Fishing/FishingRodInput (VR 입력·햅틱)  (step-07)
        │
        ▼
Fishing/FishingSpot 개편 + 구 FishingRod 정리  (step-08)
        │
        ▼
Bootstrap/FishingGroundHarness 통합  (step-09)
```

## 단계
1. [step-01-fishing-data.md](step-01-fishing-data.md) — Fishing: 데이터·수식 골격 (FishSpeciesData/RodData/FishInstance/FishingFormulas/FishingTuning)
2. [step-02-fishing-fsm.md](step-02-fishing-fsm.md) — Fishing: 물고기 FSM 스켈레톤 (`Fish`, 8상태·줄색·이벤트)
3. [step-03-fishing-cast-bite.md](step-03-fishing-cast-bite.md) — Fishing: 캐스팅·접근·입질(챔질)
4. [step-04-fishing-fight-tension.md](step-04-fishing-fight-tension.md) — Fishing: 파이팅 텐션·체력 (Fight-평상)
5. [step-05-fishing-escape-qte.md](step-05-fishing-escape-qte.md) — Fishing: 도망 + 비늘털이 스윙 QTE
6. [step-06-fishing-hook-reward.md](step-06-fishing-hook-reward.md) — Fishing: 후킹·획득 UI·보상
7. [step-07-fishing-vr-input.md](step-07-fishing-vr-input.md) — Fishing: VR 입력·햅틱 어댑터
8. [step-08-fishing-spot-migrate.md](step-08-fishing-spot-migrate.md) — Fishing: FishingSpot 개편 + 구 FishingRod 정리
9. [step-09-bootstrap-harness.md](step-09-bootstrap-harness.md) — Bootstrap: FishingGroundHarness 통합

## 병렬 실행 가능성
- **step-01 → step-02 순차 필수** (이후 전부 step-02의 `Fish`/상태 enum을 참조).
- step-03·04·05·06은 모두 step-02 위에 얹히지만 **같은 `Fish` 클래스를 확장**하므로 순차 실행 권장(병렬 시 머지 충돌). 논리적 의존은 03→04→05→06.
- step-07(VR 입력)은 step-03~06의 public 진입점(Cast/Reel/Swing/Hook)이 정해지면 병렬 착수 가능하나, 안전하게 step-06 후 실행.
- step-08·09는 코어(01~07) 완료 후. step-08(Fishing) → step-09(Bootstrap) 순차.

## 공통 규약 (모든 단계)
- **재설계 금지**: 상태전이(§3-1)·수식(§7)을 스펙 그대로. 이견 있으면 아키텍트에 보고 후 계획 갱신.
- **임시 처리 3종 태그 필수**: `TODO(FORMULA)`(FishingFormulas 스텁 return 1) / `TODO(DATA)`(하드코딩) / `TODO(TUNING)`([SerializeField]).
- **컨벤션**: 네임스페이스 `CampLantern.Fishing`, `m_` 접두사, `[SerializeField]`+프로퍼티, 이벤트 `+=` 전 `-=` 후 `OnDestroy/OnDisable` 해제 (rules/scripts.md).
- **RULES**: RULE-01(Domain Reload 금지 — asmdef 신설·InitializeOnLoad 금지), RULE-02(.asset/.prefab 직접편집 금지 — 프리팹 필요 시 Editor 팩토리), RULE-03(물리 API는 FixedUpdate). VR 90Hz — Update/FixedUpdate 경량.
- **검증**: 각 단계 완료 시 (1) 컴파일 0에러, (2) 가능하면 ClaudeBridge 플레이 스모크(런타임 NRE 0). 브릿지 사용법은 [`knowledge/unity-editor-automation.md`](../../knowledge/unity-editor-automation.md) "컴파일·Play Mode 검증 런북".
- **완료 조건(전체)**: 마지막 단계(09) 후 `TODO(FORMULA)`/`TODO(DATA)`/`TODO(TUNING)` 항목 전체 목록을 정리해 보고. 문서 수치·수식과 코드가 다른 부분은 이유 명시.

## 첫 단계 착수
```
/task-start
```
그다음 [step-01-fishing-data.md](step-01-fishing-data.md)의 지시를 수행.
