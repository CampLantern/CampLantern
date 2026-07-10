using System;
using System.Collections.Generic;
using CampLantern.Combat;
using CampLantern.Combat.Monsters;
using CampLantern.Core;
using Fusion;
using UnityEngine;

namespace CampLantern.Hunting
{
    /// <summary>
    /// 전투 몬스터의 네트워크 두뇌 (combat-detailed-network step-01, 지시서 8단계 §5).
    /// **감지는 클라, 적용은 State Authority(마스터)** — 비권한 클라의 무기/화살 판정 코드는 그대로 두고,
    /// ApplyDamage만 RPC로 권한자에 전달한다. 권한자는 로컬 MonsterHealth를 그대로 실행해
    /// 약점 배율·HP 스케일링·기여 로직을 재사용한다 (Combat 코어는 Fusion 무참조 유지).
    ///
    /// - 권한 게이팅: FSM/어그로/스킬/판정 컴포넌트는 권한자에서만 enabled. 마스터 이전은
    ///   HasStateAuthority 변화 폴링으로 재게이팅 (IStateAuthorityChanged API 의존 회피 — 비용은 bool 비교 1회).
    /// - 공격자 신원: 감지한 클라가 자기 Runner.LocalPlayer를 RPC에 스탬핑. 권한자는 PlayerRef를
    ///   NetworkedPlayerBodies(step-03 공급)로 몸체 GameObject에 해석 — 미등록이면 PlayerRef당 1:1
    ///   자식 프록시로 폴백해 MonsterHealth의 GameObject 기여 Set·HP 스케일링이 그대로 동작한다.
    /// - 계약 계승: HuntTarget의 TryStartHunt 2인 게이트·HuntActive 타격 게이트·Defeated 1회 발화
    ///   (ChangeDetector) + HuntLedger 기여(Hit)·참여자 보상. **HuntTarget/HuntLedger는 무수정** —
    ///   보상 발화는 이 클래스가 대행(RewardGranted: 처치 시 로컬 참여자에게만, HuntLedger.RewardGranted와 동일 계약).
    /// - 기존 HuntTarget 프리팹 3종(사슴/멧돼지/곰 단순 HP)은 무손상 — 이 클래스는 새 프리팹(step-02) 전용.
    /// Spec: 지시서 8단계 (§5) + design/combat-detailed-network/README.md
    /// </summary>
    [RequireComponent(typeof(MonsterHealth))]
    public class NetworkedHuntMonster : NetworkBehaviour, IDamageable
    {
        [Networked] public int NetCurrentHp { get; set; }
        [Networked] public int NetMaxHp { get; set; }

        /// <summary>사냥 진행 게이트 — TryStartHunt로만 켜진다. 시작 전/처치 후 타격 무효 (HuntTarget 규칙).</summary>
        [Networked] public NetworkBool HuntActive { get; set; }

        /// <summary>권한자 FSM 상태명 미러 — 비권한 표현(step-04)이 소비.</summary>
        [Networked, Capacity(24)] public string NetStateName { get; set; }

        private MonsterHealth m_health;
        private MonsterController m_controller;
        private AggroController m_aggro;
        private SkillRunner m_skills;
        private SkillHitCheck m_hitCheck;
        private MonsterAnimationDriver m_animDriver;
        private HuntLedger m_ledger;
        private ChangeDetector m_changes;
        private int m_lastRenderedHp;
        private string m_lastRenderedState;
        private readonly Dictionary<PlayerRef, GameObject> m_attackerProxies = new Dictionary<PlayerRef, GameObject>();
        private bool m_defeatedFired;
        private bool m_lastAuthority;

        public MonsterHealth Health => m_health;

        /// <summary>처치 시 각 클라이언트에서 1회 발화 (HuntTarget.Defeated와 동일 계약). 구독 해제 필수.</summary>
        public event Action<NetworkedHuntMonster> Defeated;

        /// <summary>처치 시 로컬 플레이어가 참여자일 때만 발화 — HuntLedger.RewardGranted 대행 (참여자 전원 동일 보상).</summary>
        public event Action<HuntTargetDef> RewardGranted;

        // ── IDamageable — HitVolume.OverrideOwner로 피격 수신자가 된다 ──

        /// <summary>
        /// 비권한 클라 근사 판정: HuntActive + HP>0 (Return 무적 등 권한자 상태는 모름 —
        /// 통과한 히트도 권한자 MonsterHealth.IsDamageable에서 최종 걸러진다).
        /// </summary>
        public bool IsDamageable => HuntActive && NetCurrentHp > 0;

        public void ApplyDamage(in HitInfo hit)
        {
            if (!IsDamageable) return;

            if (Object.HasStateAuthority)
                ApplyAuthoritative(Runner.LocalPlayer, hit.BaseDamage, hit.IsWeakpoint, hit.Speed);
            else
                RPC_ApplyHit(Runner.LocalPlayer, hit.BaseDamage, hit.IsWeakpoint, hit.Speed);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_ApplyHit(PlayerRef attacker, int baseDamage, NetworkBool isWeakpoint, float speed)
        {
            ApplyAuthoritative(attacker, baseDamage, isWeakpoint, speed);
        }

        // 권한자 일괄 적용 (§5): 데미지 최종 확정(약점 배율·스케일링 — MonsterHealth 재사용),
        // HP 차감, 사망, 기여자 등록(HuntLedger). 어그로 재계산은 MonsterHealth.Damaged 구독(권한자 전용)이 처리.
        private void ApplyAuthoritative(PlayerRef attacker, int baseDamage, bool isWeakpoint, float speed)
        {
            if (!HuntActive || m_health.CurrentHp <= 0) return;

            var hit = new HitInfo
            {
                BaseDamage = baseDamage,
                IsWeakpoint = isWeakpoint,
                Speed = speed,
                Point = transform.position,
                Attacker = ResolveAttackerObject(attacker),
            };
            m_health.ApplyDamage(in hit);
            m_ledger?.RecordContribution(attacker, HuntLedger.ContributionKind.Hit);

            if (m_health.CurrentHp <= 0)
                HuntActive = false;

            MirrorHealth();
        }

        // PlayerRef → 몸체 해석. 미등록(step-03 전/이탈)이면 PlayerRef당 1:1 자식 프록시 —
        // MonsterHealth 기여 Set(§4-2 스케일링)의 신원 단위가 "플레이어당 1개"로 유지되는 것이 목적.
        // 프록시는 몬스터 위치라 어그로 타겟으로는 부적합 — 실몸체 등록(step-03) 전까지의 폴백임을 명시.
        private GameObject ResolveAttackerObject(PlayerRef attacker)
        {
            GameObject body = NetworkedPlayerBodies.Resolve(attacker);
            if (body != null) return body;

            if (!m_attackerProxies.TryGetValue(attacker, out GameObject proxy) || proxy == null)
            {
                proxy = new GameObject($"AttackerProxy_P{attacker.PlayerId}");
                proxy.transform.SetParent(transform, false);
                m_attackerProxies[attacker] = proxy;
            }
            return proxy;
        }

        // ── 사냥 게이트 (HuntTarget.TryStartHunt 계승) ──

        /// <summary>
        /// 협동 게이트 — huntDef.RequiredParticipants 미만이면 시작 불가 (social-cooperation ②).
        /// State Authority에서만 호출 가능. 재사냥 시 HP·기여 초기화.
        /// </summary>
        public bool TryStartHunt()
        {
            if (!Object.HasStateAuthority || HuntActive) return false;

            HuntTargetDef huntDef = m_health.Data != null ? m_health.Data.huntDef : null;
            if (huntDef == null)
            {
                Debug.LogWarning("[NetworkedHuntMonster] MonsterData.huntDef 없음 — 사냥 시작 불가");
                return false;
            }

            if (Runner.SessionInfo.PlayerCount < huntDef.RequiredParticipants)
            {
                Debug.Log($"[NetworkedHuntMonster] 참여 인원 부족 ({Runner.SessionInfo.PlayerCount}/{huntDef.RequiredParticipants}) — 사냥 시작 불가");
                return false;
            }

            m_health.ResetToFull();            // 기여·스케일 초기화 포함 (새 교전)
            m_ledger?.ResetForNewHunt();
            m_defeatedFired = false;
            HuntActive = true;
            MirrorHealth();
            return true;
        }

        // ── 수명 주기 ──

        public override void Spawned()
        {
            m_health = GetComponent<MonsterHealth>();
            m_controller = GetComponent<MonsterController>();
            m_aggro = GetComponent<AggroController>();
            m_skills = GetComponent<SkillRunner>();
            m_hitCheck = GetComponent<SkillHitCheck>();
            m_animDriver = GetComponent<MonsterAnimationDriver>();
            m_ledger = GetComponent<HuntLedger>();
            m_changes = GetChangeDetector(ChangeDetector.Source.SimulationState);
            m_lastRenderedHp = NetCurrentHp;

            // 피격 수신자를 이 라우터로 — 같은 GO의 MonsterHealth 직접 수신 차단 (권한 경계 유지)
            foreach (HitVolume volume in GetComponentsInChildren<HitVolume>(true))
                volume.OverrideOwner(this);

            if (Object.HasStateAuthority)
                MirrorHealth();

            ApplyAuthorityGating();
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority) return;
            // 상태명 미러 — step-04 비권한 표현이 소비. 변화 시에만 쓰기(대역폭).
            string current = m_controller != null ? m_controller.CurrentStateName : string.Empty;
            if (NetStateName != current) NetStateName = current;
        }

        public override void Render()
        {
            // 마스터 이전 재게이팅 (폴링)
            if (Object.HasStateAuthority != m_lastAuthority)
                ApplyAuthorityGating();

            bool isAuthority = Object.HasStateAuthority;

            foreach (string change in m_changes.DetectChanges(this))
            {
                // 처치 감지 — 각 클라 1회 (HuntTarget.Render 패턴)
                if (change == nameof(NetCurrentHp))
                {
                    if (NetCurrentHp <= 0 && !m_defeatedFired)
                    {
                        m_defeatedFired = true;
                        Defeated?.Invoke(this);

                        HuntTargetDef huntDef = m_health != null && m_health.Data != null ? m_health.Data.huntDef : null;
                        if (huntDef != null && m_ledger != null && m_ledger.IsParticipant(Runner.LocalPlayer))
                            RewardGranted?.Invoke(huntDef); // 참여자 전원 동일 보상 — 각자 클라에서 발화
                    }

                    // 비권한 표현: HP 감소 = 피격 연출, 0 = 사망 연출 (step-04 — 권한자는 로컬 이벤트 경로)
                    if (!isAuthority && m_animDriver != null)
                    {
                        if (NetCurrentHp <= 0) m_animDriver.ApplyStateByName("Dead");
                        else if (NetCurrentHp < m_lastRenderedHp) m_animDriver.ApplyHitReaction();
                    }
                    m_lastRenderedHp = NetCurrentHp;
                }

                // 비권한 표현: 권한자 FSM 상태명 → 애니 (step-04)
                if (change == nameof(NetStateName) && !isAuthority && m_animDriver != null)
                {
                    string state = NetStateName;
                    if (state != m_lastRenderedState && NetCurrentHp > 0)
                    {
                        m_lastRenderedState = state;
                        m_animDriver.ApplyStateByName(state);
                    }
                }
            }
        }

        // FSM/어그로/스킬/판정은 권한자 전용 실행 (§5) — 비권한 클라는 NetworkTransform 수신 + 표현(step-04)만
        private void ApplyAuthorityGating()
        {
            bool authority = Object != null && Object.IsValid && Object.HasStateAuthority;
            if (m_controller != null) m_controller.enabled = authority;
            if (m_aggro != null) m_aggro.enabled = authority;
            if (m_skills != null) m_skills.enabled = authority;
            if (m_hitCheck != null) m_hitCheck.enabled = authority;
            m_lastAuthority = authority;
        }

        private void MirrorHealth()
        {
            NetCurrentHp = m_health.CurrentHp;
            NetMaxHp = m_health.MaxHp;
        }
    }
}
