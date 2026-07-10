using System;
using System.Collections.Generic;
using UnityEngine;

namespace CampLantern.Combat.Player
{
    /// <summary>
    /// 플레이어 체력 + 다운 규칙 (§2-2, step-09 확장).
    /// 다운: HP 0 → 행동 불능 + 피격 면제, **제한시간 없음**. 소생(PlayerReviver)으로만 복귀.
    /// "행동 불능"의 실제 입력 잠금은 VR 입력 어댑터/이동 시스템 몫 — 여기선 상태·이벤트만 노출.
    /// 몬스터 스킬 데미지는 배율 없이 BaseDamage 그대로 (플레이어에겐 약점 개념 없음).
    ///
    /// RULE-01: Domain Reload 비활성 프로젝트 — static 목록/이벤트는 SubsystemRegistration에서 초기화.
    /// Spec: 지시서 5단계(최소판)·7단계(§2-2 다운/피격 면제)
    /// </summary>
    public class PlayerHealth : MonoBehaviour, IDamageable
    {
        [Tooltip("최대 HP — §2 스펙 수치 미정. TODO(TUNING)")]
        [SerializeField] private int m_maxHp = 100;

        // 씬 내 플레이어 체력 목록 — 전멸 감시(PartyWipeWatcher)·통계용
        private static readonly List<PlayerHealth> s_all = new List<PlayerHealth>();
        public static IReadOnlyList<PlayerHealth> All => s_all;

        /// <summary>플레이어 다운 발생 (전역) — AggroController가 "타겟 다운 시 즉시 재계산"(§4-3 규칙 6)에 구독.</summary>
        public static event Action<PlayerHealth> AnyPlayerDowned;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_all.Clear();
            AnyPlayerDowned = null;
        }

        public int MaxHp => m_maxHp;
        public int CurrentHp { get; private set; }

        /// <summary>다운 상태 — 행동 불능 + 피격 면제 (§2-2). 몬스터 타겟팅에서도 제외된다(CombatPlayers).</summary>
        public bool IsDowned { get; private set; }

        /// <summary>다운 중 피격 면제 포함 — 0 HP면 추가 피격 무시.</summary>
        public bool IsDamageable => CurrentHp > 0;

        /// <summary>피격 발생 — 디버프 연출/햅틱/UI 훅. 구독자는 OnDestroy/OnDisable에서 해제할 것.</summary>
        public event Action<HitInfo> Damaged;

        /// <summary>다운 진입 (HP 0 도달 시 1회).</summary>
        public event Action Downed;

        /// <summary>소생 완료 (PlayerReviver가 ReviveTo 호출 시).</summary>
        public event Action Revived;

        private void Awake()
        {
            CurrentHp = m_maxHp;
        }

        private void OnEnable()
        {
            if (!s_all.Contains(this)) s_all.Add(this);
        }

        private void OnDisable()
        {
            s_all.Remove(this);
        }

        /// <summary>런타임 조립(봇/하네스) 대응 — Awake 이후 주입 시 HP 초기화 (step-02 Configure 교훈).</summary>
        public void Configure(int maxHp)
        {
            m_maxHp = maxHp;
            CurrentHp = maxHp;
            IsDowned = false;
        }

        public void ApplyDamage(in HitInfo hit)
        {
            if (!IsDamageable) return;

            CurrentHp = Mathf.Max(0, CurrentHp - hit.BaseDamage);
            Debug.Log($"[PlayerHealth] hit damage={hit.BaseDamage} hp={CurrentHp}/{m_maxHp} " +
                      $"attacker={(hit.Attacker != null ? hit.Attacker.name : "(null)")} ({name})");
            Damaged?.Invoke(hit);

            if (CurrentHp <= 0 && !IsDowned)
            {
                IsDowned = true;
                Debug.Log($"[PlayerHealth] downed ({name})");
                Downed?.Invoke();
                AnyPlayerDowned?.Invoke(this);
            }
        }

        /// <summary>소생 — HP 비율만큼 회복하고 다운 해제 (§2-3). PlayerReviver가 호출.</summary>
        public void ReviveTo(float hpRatio)
        {
            if (!IsDowned) return;
            CurrentHp = Mathf.Max(1, Mathf.RoundToInt(m_maxHp * hpRatio));
            IsDowned = false;
            Debug.Log($"[PlayerHealth] revived hp={CurrentHp}/{m_maxHp} ({name})");
            Revived?.Invoke();
        }

        public void ResetToFull()
        {
            CurrentHp = m_maxHp;
            IsDowned = false;
        }
    }
}
