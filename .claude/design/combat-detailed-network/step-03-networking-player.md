# Step 03: Networking — NetworkedCombatPlayer (원격 타겟 등록·다운/소생/전멸 동기화)

- **영역:** `Networking` (`Assets/Scripts/Networking/`)
- **선행 단계:** step-01 (권한자 판정 주체 존재). step-02와 병렬 가능(파일 독립)
- **후행 단계:** step-05 검증
- **스펙:** 지시서 8단계 "소생/다운/전멸 판정도 호스트 권한으로 이동" + §4 몬스터→플레이어 판정

---

## 목적
플레이어를 네트워크 전투에 등장시킨다 — ① 원격 플레이어 몸체를 `CombatPlayers`에 등록해 권한자의 몬스터가 **모든** 플레이어를 감지/추격/타격하게 하고 ② 다운 상태를 동기화하며 ③ 몬스터 스킬 피격(권한자 판정)을 소유 클라의 `PlayerHealth`로 전달하고 ④ 전멸을 권한자(마스터)가 확정해 브로드캐스트한다.

---

## 에이전트 실행 지침

`/task-start` 호출 후 수행. 스폰 패턴은 `AvatarController`/`VoiceController`(SessionStarted에서 플레이어당 스폰, tech-stack-decisions "런타임 AddComponent 러너는 자동 콜백 수집 제외" 함정 포함)를 참조한다.

### 생성/수정 파일
- `Assets/Scripts/Networking/NetworkedCombatPlayer.cs` — 생성 (`CampLantern.Networking`, NetworkBehaviour)
- `Assets/Scripts/Networking/CombatPlayerSpawner.cs` — 생성 (SessionStarted → 플레이어당 스폰. 파일 구성 재량)
- `Assets/Scripts/Editor/` — 스포너를 Network GO에 배선(기존 `EnsureVoiceOnNetworkObject` 패턴의 전투판, 사냥터만)

### 핵심 계약
```csharp
public class NetworkedCombatPlayer : NetworkBehaviour, IDamageable
{
    [Networked] public NetworkBool IsDowned { get; set; }
    // 리그 추종: 로컬 소유자는 자기 리그(카메라) 위치를 따라감 + NetworkTransform 동기화
    // CombatPlayers 등록: Spawned에서 transform 등록, Despawned에서 해제 (원격 포함 전원)
    // IDamageable: 권한자의 SkillHitCheck가 이 오브젝트를 때린다 →
    //   StateAuthority(소유자) 대상 RPC로 전달 → 소유 클라의 로컬 PlayerHealth.ApplyDamage
    // 다운: 로컬 PlayerHealth.Downed 구독 → IsDowned 동기화 (원격 클라의 CombatPlayers 제외 판정이 동작)
    // 소생: 소생자 클라가 홀드 판정(PlayerReviver 로컬) → 완료 시 RPC_ReviveRequest(대상) → 대상 소유 클라 적용
    // 전멸: 마스터가 전원 IsDowned 관찰 → RPC 브로드캐스트 → 각 클라 PartyWipeWatcher 경로/씬 로드
}
```

### 구현 규칙
- **CombatPlayers 제외 판정 호환**: 원격 플레이어 GO에는 로컬 `PlayerHealth`가 없다 — `CombatPlayers.FindNearest`의 다운 제외가 원격에도 동작하도록, 이 컴포넌트가 다운 시 레지스트리에서 스스로 해제(또는 PlayerHealth 프록시 부착 중 선택, 근거 설명).
- **몬스터 스킬 판정**: 권한자의 `SkillHitCheck.TickMeleeArc`는 `CombatPlayers` 목록을 훑으므로 원격 등록만으로 판정 대상이 된다. 피격 전달은 IDamageable 라우팅(위) — SkillHitCheck 무수정.
- **소생/다운/전멸의 "호스트 권한"**: Shared Mode에서 플레이어 상태 권한은 소유자에게 있으므로, 지시서 의도(권한 일원화)는 **전멸 확정만 마스터**로 두고 다운/소생은 소유 클라 적용+상태 동기화로 해석한다 — 진행도(전멸=사냥 실패)가 §12 정합성 대상이기 때문. 이 해석은 아키텍트 확인 항목으로 보고에 명시.
- PersistentPlayer/리그 프리팹 직접 편집 금지 — 리그 추종은 런타임 탐색.

### 완료 판정
- [ ] 컴파일 + 위빙 OK. 멀티 피어 더미 2인: 더미 쪽 몸체가 권한자 몬스터의 타겟이 됨(추격 로그/기즈모)
- [ ] 더미 다운 시 몬스터 타겟이 로컬로 재계산 (AnyPlayerDowned 경로 or 레지스트리 해제)
- [ ] 로컬 폴백 회귀: 봇 7종 ALL PASS 유지
- [ ] "다운/소생 소유 클라 적용 + 전멸만 마스터 확정" 해석의 아키텍트 승인 여부 보고

---

## 금지 사항
- VoiceController/AvatarController/SessionLauncher 수정 금지 (패턴 참조만).
- 보상/경제 로직 추가 금지.
