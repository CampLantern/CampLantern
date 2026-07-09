# Step 09: Combat — 플레이어 전투 규칙 (다운·소생·전멸) + HP 스케일링·기여

- **영역:** `Combat` (`Player/` 중심 + `Monsters/MonsterHealth` 확장)
- **선행 단계:** step-06 (PlayerHealth 최소판), step-05 (다운 시 어그로 재계산 훅)
- **후행 단계:** step-11(하네스 검증), step-12(다운/소생/전멸·HP 스케일링을 호스트 권한으로 이동)
- **스펙:** 지시서 7단계 전반 (§2, §4-2, §3)

---

## 목적
플레이어 생존 규칙(다운/소생/전멸)과 디버프 연출 훅, 몬스터 HP 스케일링, 기여 판정 이벤트를 구현한다. 홀스터/내구도 귀환은 step-10으로 분리.

---

## 에이전트 실행 지침

`/task-start` 호출 후, [`전투_시스템_구현_지시서.md`](../../domain/gdd/전투_시스템_구현_지시서.md) **7단계** 본문 중 §2·§4-2·§3 부분을 그대로 구현한다.

### 생성/수정 파일
- `Assets/Scripts/Combat/Player/PlayerHealth.cs` — 수정 (다운/소생/전멸 확장)
- `Assets/Scripts/Combat/Player/PlayerReviver.cs` — 생성 (소생 상호작용 — 파일 구성은 에이전트 재량)
- `Assets/Scripts/Combat/Player/ScreenTintOverlay.cs` — 생성 (디버프 연출 훅)
- `Assets/Scripts/Combat/Player/PartyWipeWatcher.cs` — 생성 (전멸 감지 → Scene 로드)
- `Assets/Scripts/Combat/Monsters/MonsterHealth.cs` — 수정 (HP 스케일링 + 기여자 Set)

### 구현 규칙 (지시서 그대로)
- **다운 (§2-2)**: HP 0 → 행동 불능 + 피격 면제. **제한시간 없음.** 다운 시 `AggroController.NotifyTargetDowned` 호출(step-05 훅 연결).
- **소생 (§2-3)**: 다른 플레이어 **손 접촉 + 버튼 홀드 `reviveHoldSeconds`(3s)** → HP `reviveHpRatio`(절반) 부활. **소생 중 소생자가 피격돼도 취소 없음.** 손 접촉 판정도 공통 판정 규약(거리 판정 — 물리 콜백 금지). VR 버튼 홀드 입력은 입력 무관 진입점(`BeginRevive`/`CancelRevive` 등) + 어댑터.
- **전멸**: 전원 다운 → 사냥 실패 → 지정 Scene 로드. **Scene 이름은 SerializeField 설정값** (기본 `"Lobby"` — 기존 `m_lobbySceneName` 관례).
- **디버프 연출 훅 (§2-2)**: 화면 테두리 색 오버레이 — 기절=보라, 독=초록. 기계 효과: 기절=입력 잠금(**머리 회전은 자유 — VR 멀미 방지 주석 명시**), 독=틱 데미지 코루틴. 지속/틱 수치는 SO 필드 자리만(`// TODO(SPEC)` — CombatBalanceData에 Header 추가). VR 오버레이 렌더 방식(카메라 앞 쿼드 등)은 에이전트 선택+근거 — 훅과 로그까지 필수, 비주얼 완성도는 임시 가능.
- **HP 스케일링 (§4-2)**: `MonsterHealth`에 기여자 Set. **신규 플레이어의 첫 유효타 시점**에 `MaxHp = baseHp × (1 + hpScalePerExtraPlayer × (인원−1))` 갱신, **증가분을 잔여 HP에 플랫 가산. 감소 없음.**
- **기여 판정 (§3)**: 유효타 1회 이상 = 기여. 사냥 성공(몬스터 사망) 시 기여자 목록 이벤트 발행 — `event Action<IReadOnlyCollection<GameObject>> HuntSucceeded` 등. **보상 지급은 구현하지 않는다**(이벤트만 — step-12에서 HuntLedger·RewardMaterials와 통합, 로컬 하네스는 로그만).

### 선행 산출물 의존성
- `PlayerHealth` 최소판·`ApplyDamage` 경로 — step-06. `NotifyTargetDowned` 훅 — step-05. `CombatBalanceData`(revive/hpScale 필드) — step-01.

### 제약
- 다운/소생/전멸 상태 변화도 이벤트로 노출(`Downed`/`Revived`/`PartyWiped`) — step-11 하네스와 step-12 권한 이동이 구독한다.
- `PersistentPlayer`(DontDestroyOnLoad 리그)와의 연결: PlayerHealth를 리그에 붙일지 씬 오브젝트로 둘지 에이전트 판단+근거 (전멸 Scene 로드 시 리그는 살아남는다는 점 고려).
- rules/scripts.md: 오버레이 GameObject 활성화는 소유 Manager가 관리(자기 Awake에서 SetActive(false) 금지).

### 완료 판정
- [ ] 2인 테스트(더미 플레이어): 다운 → 소생(홀드 3s, HP 절반) 동작, 소생 중 소생자 피격에도 취소 없음
- [ ] 전원 다운 → 지정 Scene 로드
- [ ] 2인째 첫 유효타 시 몬스터 MaxHp ×1.5 갱신 + 증가분 잔여 가산 로그 (감소 없음 확인)
- [ ] 몬스터 사망 시 기여자 목록 이벤트 로그 (유효타 없는 참관자는 미포함)
- [ ] 컴파일 통과 + 수동 체크리스트 보고

---

## 금지 사항
- 보상 지급(인벤토리/코인) 구현 금지 — 이벤트 발행까지만 (economy·HuntLedger 소관).
- 홀스터/내구도 귀환 선작업 금지 — step-10 몫.
- `Hunting/` 코드 수정 금지.
