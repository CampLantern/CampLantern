using CampLantern.Combat;
using CampLantern.Combat.Data;
using CampLantern.Combat.Player;
using CampLantern.Hunting;
using Fusion;
using UnityEngine;

namespace CampLantern.Networking
{
    /// <summary>
    /// 플레이어의 네트워크 전투 존재 (combat-detailed-network step-03). 플레이어당 1개 스폰(CombatPlayerSpawner).
    ///
    /// - 타겟 등록: 모든 클라에서 CombatPlayers에 등록 — 권한자(마스터)의 몬스터가 원격 플레이어도
    ///   감지/추격/타격한다. 다운 시 레지스트리에서 해제(§2-2 타겟 제외 — 원격에는 PlayerHealth가 없어
    ///   IsDowned 복제 상태로 토글하는 방식 채택. 프록시 PlayerHealth 부착안은 이중 진실이라 배제).
    /// - 신원 공급: NetworkedPlayerBodies에 PlayerRef→이 GO 등록 — NetworkedHuntMonster의 기여/어그로
    ///   신원 해석(step-01 계약 이행). 어그로가 이 트랜스폼을 실타겟으로 추적한다.
    /// - 피격: 권한자의 SkillHitCheck가 이 GO를 때리면(IDamageable) 소유 클라로 RPC → 로컬 PlayerHealth 적용.
    ///   PlayerHealth는 **소유 클라에만** 런타임 부착 — PlayerHealth.All(전멸 감시·로컬 UI)이 원격 복사본으로
    ///   오염되지 않는다.
    /// - 다운/소생: 소유 클라의 PlayerHealth.Downed/Revived → [Networked] IsDowned 동기화.
    ///   소생 요청은 아무 클라에서나 RequestRevive() → 소유 클라가 적용 (§2-3 — 홀드/접촉 판정은
    ///   요청 측(하네스/VR 어댑터/봇) 책임, 완료 시점에만 호출).
    /// - 전멸: RPC 브로드캐스트 대신 **복제 상태 유도** — IsDowned가 [Networked]라 각 클라가 동일 상태에서
    ///   독립적으로 "전원 다운"에 도달한다(마스터 확정과 등가, 왕복 없음). 관찰은 CombatPlayerSpawner.
    /// Spec: 지시서 8단계 (§5) + design/combat-detailed-network/step-03
    /// </summary>
    public class NetworkedCombatPlayer : NetworkBehaviour, IDamageable
    {
        [Tooltip("소생 비율 등 — 팩토리 배선")]
        [SerializeField] private CombatBalanceData m_balance;

        [Networked] public NetworkBool IsDowned { get; set; }

        private PlayerHealth m_health;   // 소유 클라 전용
        private ChangeDetector m_changes;
        private Transform m_followCamera;
        private bool m_registered;

        public bool IsDamageable => !IsDowned;

        public override void Spawned()
        {
            name = $"CombatPlayer_P{Object.StateAuthority.PlayerId}";
            m_changes = GetChangeDetector(ChangeDetector.Source.SimulationState);

            NetworkedPlayerBodies.Register(Object.StateAuthority, gameObject);
            SetTargetable(!IsDowned);

            if (Object.HasStateAuthority)
            {
                // 로컬 체력 실체 — 소유 클라에만 (README 결정)
                m_health = gameObject.AddComponent<PlayerHealth>();
                m_health.Configure(100); // TODO(TUNING): §2 수치 미정 — PlayerHealth 기본과 동일
                m_health.Downed -= OnLocalDowned;
                m_health.Downed += OnLocalDowned;
                m_health.Revived -= OnLocalRevived;
                m_health.Revived += OnLocalRevived;

                var cam = Camera.main;
                m_followCamera = cam != null ? cam.transform : null;
            }

            Debug.Log($"[NetworkedCombatPlayer] spawned {name} (authority={Object.HasStateAuthority})");
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            NetworkedPlayerBodies.Unregister(Object.StateAuthority);
            SetTargetable(false);
            if (m_health != null)
            {
                m_health.Downed -= OnLocalDowned;
                m_health.Revived -= OnLocalRevived;
            }
        }

        private void Update()
        {
            // 소유 클라 — 리그(카메라) 수평 추종. 몬스터 감지/추격 판정용 존재 위치.
            if (Object == null || !Object.HasStateAuthority || m_followCamera == null) return;
            Vector3 p = m_followCamera.position;
            transform.position = new Vector3(p.x, 0f, p.z);
        }

        public override void Render()
        {
            // 다운 상태 복제 감지 → 전 클라에서 타겟 등록 토글
            foreach (string change in m_changes.DetectChanges(this))
                if (change == nameof(IsDowned))
                    SetTargetable(!IsDowned);
        }

        // ── 피격 (몬스터 스킬 — 권한자 클라에서 판정됨) ──

        public void ApplyDamage(in HitInfo hit)
        {
            if (!IsDamageable) return;

            if (Object.HasStateAuthority)
                ApplyLocal(hit.BaseDamage);
            else
                RPC_TakeDamage(hit.BaseDamage);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_TakeDamage(int damage)
        {
            ApplyLocal(damage);
        }

        private void ApplyLocal(int damage)
        {
            m_health?.ApplyDamage(new HitInfo { BaseDamage = damage, Point = transform.position });
        }

        // ── 소생 (§2-3) — 홀드/접촉 판정 완료 시점에 요청 측이 호출 ──

        public void RequestRevive()
        {
            if (Object.HasStateAuthority)
                ReviveLocal();
            else
                RPC_Revive();
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_Revive()
        {
            ReviveLocal();
        }

        private void ReviveLocal()
        {
            if (m_health == null || !m_health.IsDowned) return;
            float ratio = m_balance != null ? m_balance.reviveHpRatio : 0.5f;
            m_health.ReviveTo(ratio);
        }

        // ── 소유 클라 상태 → 복제 ──

        private void OnLocalDowned()
        {
            IsDowned = true;
            SetTargetable(false);
        }

        private void OnLocalRevived()
        {
            IsDowned = false;
            SetTargetable(true);
        }

        private void SetTargetable(bool targetable)
        {
            if (targetable && !m_registered)
            {
                CombatPlayers.Register(transform);
                m_registered = true;
            }
            else if (!targetable && m_registered)
            {
                CombatPlayers.Unregister(transform);
                m_registered = false;
            }
        }
    }
}
