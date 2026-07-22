using System;
using CampLantern.Core;
using Fusion;
using UnityEngine;

namespace CampLantern.Hunting
{
    /// <summary>
    /// 대형 사냥감 체력·상태 (P0: 제자리 + 체력, 이동 AI는 P1).
    /// 체력 차감은 State Authority에서만 수행한다 — 클라이언트 각자 계산 금지
    /// (진행도 불일치가 GDD 13장 최우선 리스크, domain/social-cooperation.md).
    /// </summary>
    public class HuntTarget : NetworkBehaviour
    {
        [Networked] public int CurrentHealth { get; set; }

        /// <summary>사냥 진행 중 여부. RequiredParticipants 충족 후 TryStartHunt로만 켜진다.</summary>
        [Networked] public NetworkBool HuntActive { get; set; }

        public HuntTargetDef Def; // step-03 에셋 참조 — 스포너가 주입

        /// <summary>체력 0 도달 시 각 클라이언트에서 1회 발화. 구독자는 OnDestroy/OnDisable에서 반드시 해제할 것.</summary>
        public event Action<HuntTarget> Defeated;

        private ChangeDetector m_changes;
        private HuntLedger m_ledger;
        private bool m_defeatedFired;

        public override void Spawned()
        {
            m_ledger  = GetComponent<HuntLedger>();
            m_changes = GetChangeDetector(ChangeDetector.Source.SimulationState);

            if (Object.HasStateAuthority && Def != null)
                CurrentHealth = Def.MaxHealth;
        }

        /// <summary>
        /// 2인 미만이면 사냥 시작 불가 (social-cooperation.md ② "2인 이상 협동 시에만 포획 가능").
        /// State Authority에서만 호출 가능.
        /// </summary>
        public bool TryStartHunt()
        {
            if (!Object.HasStateAuthority) return false;
            if (HuntActive) return false;
            if (Def == null) return false;

            if (Runner.SessionInfo.PlayerCount < Def.RequiredParticipants)
            {
                Debug.Log($"[HuntTarget] 참여 인원 부족 ({Runner.SessionInfo.PlayerCount}/{Def.RequiredParticipants}) — 사냥 시작 불가");
                return false;
            }

            CurrentHealth   = Def.MaxHealth;
            HuntActive      = true;
            m_defeatedFired = false; // 재사냥 시작 — 이전 처치의 1회성 가드를 새 사이클로 초기화
            m_ledger?.ResetForNewHunt(); // 참여 판정도 이번 사이클 기여만 반영하도록 초기화

            return true;
        }

        /// <summary>
        /// 타격 적용. 비권한 클라이언트에서 호출하면 State Authority로 RPC 전달된다.
        /// </summary>
        public void ApplyHit(PlayerRef contributor, int damage)
        {
            if (damage <= 0) return;

            if (Object.HasStateAuthority)
                ApplyHitAuthoritative(contributor, damage);
            else
                RPC_ApplyHit(contributor, damage);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_ApplyHit(PlayerRef contributor, int damage)
        {
            ApplyHitAuthoritative(contributor, damage);
        }

        private void ApplyHitAuthoritative(PlayerRef contributor, int damage)
        {
            if (!HuntActive || CurrentHealth <= 0) return; // 시작 전/처치 후 타격 무효

            CurrentHealth -= damage;
            m_ledger?.RecordContribution(contributor, HuntLedger.ContributionKind.Hit);

            if (CurrentHealth <= 0)
                HuntActive = false;
        }

        public override void Render()
        {
            // [Networked] 값 변화는 ChangeDetector로 감지 — 각 클라이언트가 자기 화면에서 처치를 인지
            foreach (var change in m_changes.DetectChanges(this))
            {
                if (change != nameof(CurrentHealth)) continue;

                if (CurrentHealth > 0)
                {
                    // 재사냥으로 체력이 회복됨 — 비권한 클라이언트도 여기서 가드를 리셋해야
                    // 2회차 처치에서 Defeated(→보상)가 발화한다. TryStartHunt는 권한자 전용이라
                    // 이 경로가 없으면 비권한 쪽만 보상이 누락된다.
                    m_defeatedFired = false;
                }
                else if (!m_defeatedFired)
                {
                    m_defeatedFired = true;
                    Defeated?.Invoke(this);
                }
            }
        }
    }
}
