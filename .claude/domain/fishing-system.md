# 낚시 시스템 (정교화 구현)

## 한 줄 요약
"빨간색일 때 릴을 참는 절제" 하나로 수렴하는 정밀 파이팅 낚시 — 스펙(`gdd/낚시_시스템_구현_지시서.md` §1~9)을 `design/fishing-detailed` 9단계로 구현 완료(2026-07-09, 무인 주행 검증 통과). 이 문서는 **지시서에 없는 구현 결정·암묵지**만 담는다.

## 핵심 타입 / 진입점 (`Assets/Scripts/Fishing/`)
- `Fish` — **FSM 8상태의 소유자이자 파이팅 주체**(§3-1). 개체 스탯·체력·텐션을 소유. 낚싯대가 아니라 물고기가 진행을 주도한다.
- `FishingRod` — 얇은 컨트롤러: `Cast(Fish)`/`Chamjil()`/`SetReeling(bool)`/미끼·내구도(RodData). 획득은 `FishCaught(FishInstance, FishReward)` 이벤트로만 알림 — **Wallet/Inventory 적용은 하네스 책임**.
- `FishingSpot` — 스포너/관리자: 실루엣 스폰(크기=개체 롤 반영)·Idle 유영·재스폰·`TryGetNearestFish`(사거리 게이트).
- `FishingFormulas` — §7 수식 단일 경유점. §9 미정 4개(XP/코인/미끼가/수리가)만 `TODO(FORMULA)` 스텁.
- `FishingTuning` — 튜닝 값 전부 SerializeField(`TODO(TUNING)` 22건 — 스윙 임계값은 VR 실기 필수).
- `FishingRodInput` — VR 어댑터(감지→코어 호출만). `FishingLoopSelfTest` — 무인 검증 봇(아래).

## 숨은 규칙 / 암묵지
- **`FishSpeciesData.fishId`는 반드시 기존 `FishDef.Id`와 일치해야 한다** (`fish_crucian`/`fish_trout`/`fish_golden_carp`). 이게 ContentRegistry→아이콘·인벤토리·에디터 팩토리(`m_fishTable` 배선)를 잇는 연결 고리다. **새 어종 추가 = 4종 세트**: ① `FishSpeciesTable`에 스탯 ② `P0DataFactory`에 FishDef ③ `ItemIconFactory`에 아이콘 SVG ④ Content Registry Rebuild.
- 텐션은 **시도마다 rod.tension으로 덮어씀**(이월 없음, §7-2) — Fight 진입(StartFight)에서만 초기화하고 도망 복귀(EnterFightNormal)에선 건드리지 않는다. 도망 간격 기산점 = **이전 도망 종료 시점**(§7-3 소프트락 방지) — EnterFightNormal에서 재산정.
- 비늘털이 디버프는 `EffectivePower` 프로퍼티 하나로 일원화 — 텐션 감소·도망 간격·지속 세 수식이 전부 이걸 참조하므로 디버프를 따로 곱하면 이중 적용된다.
- `FishingRodInput`의 릴링은 **에지 트리거**(변화 시에만 SetReeling) — 매 프레임 덮어쓰면 IMGUI 토글 등 다른 SetReeling 경로가 무효화된다(실제 결함이었음, `0ee58ee`).
- 구 `FishDef.BiteWindowSeconds/Min·MaxWaitSeconds`는 신 시스템 미사용(입질 윈도우는 전 어종 고정 §2) — 필드는 에셋 호환용으로 잔존.

## 검증 (사람 없이 회귀 확인)
플레이 중 아무 GameObject에 `FishingLoopSelfTest`를 AddComponent(브릿지: GameObject.Create→Component.Add)하면 **포획(절제 플레이)·줄 끊김(빨간색 릴링) 양 경로를 무인 주행**하고 `[FishTest]` 태그로 전이·수치를 로그한다. 실측: 개체 롤(42.8cm=lerp 정확 일치)·텐션 감소(×0.25/s, 비늘털이 ×1.5)·§3-2 리셋 전부 스펙 일치. 절차 일반론은 `knowledge/unity-editor-automation.md` "봇 주입 패턴".

## 시스템 간 관계
- 획득 흐름: `Fish.Caught` → `FishingRod.FishCaught(개체+보상)` → 하네스가 fishId→`ContentRegistry.TryGetItem`→기존 FishDef를 `Inventory.Add`(아이콘 재사용) + `Wallet.Add`(코인) + `FishResultPanel.Show`. XP는 저장처 미정(§9) — 하네스 로컬 누적(`TODO(DATA)`).
- [[resource-loop]] 낚시 섹션(동물의숲 수준 단순화)은 P0 최소판 기준 — 이 정교화가 그 자리를 대체한다.

## 수정 시 주의
- 프로젝트는 **New Input System 전용**(`activeInputHandler=1`) — 입력 코드에 legacy `UnityEngine.Input` 금지(런타임 예외). `knowledge/unity-scripting-gotchas.md` §6.
- §7 수식 수치를 바꿀 땐 코드가 아니라 `FishingTuning`/`FishingFormulas`에서 — 호출부 직접 수정 금지(구현 지침 [1]).
- 팀원의 RealVRFishing 태클 에셋(`Assets/RealVRFishing/`, 2026-07-09 병합)이 낚싯대/미끼 비주얼 교체 후보 — 교체 시 "Visual" 자식 스왑 원칙(PlaceholderArtFactory 방식) 유지.
