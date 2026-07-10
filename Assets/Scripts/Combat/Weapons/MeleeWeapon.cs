using System;
using System.Collections.Generic;
using CampLantern.Combat.Data;
using UnityEngine;

namespace CampLantern.Combat.Weapons
{
    /// <summary>
    /// 근접 무기 유효타 판정 — 무기 상태머신 Idle → 공격(Attack) → 마무리(Recover) → Idle.
    ///
    /// [판정 기법 선택 근거 — 판정 규약: 물리 콜백 금지, 능동 쿼리]
    /// ① 스윕 캐스트: 타격부 세그먼트(Base→Tip)를 N개 점으로 샘플링하고, 각 점의 "이전 프레임 위치 →
    ///    현재 위치" 경로를 SphereCast로 검사한다. 프레임 사이 이동 경로 전체를 커버하므로
    ///    고속 스윙에서도 판정 누락이 없다(터널링 금지 요구).
    /// ② 오버랩 보강: 무기가 정지해 있고 몬스터가 걸어 들어오는 경우 스윕(길이 0)이 놓치므로,
    ///    현재 세그먼트의 OverlapCapsule을 병용해 접촉 유지/이탈 추적을 정확히 한다.
    ///
    /// [속도 측정 근거] 물리 엔진 velocity는 그랩(kinematic 추적) 상태에서 신뢰 불가(지시서 명시) —
    /// Tip 트랜스폼의 프레임 간 위치 차분 / deltaTime으로 실측한다.
    ///
    /// [§1-2 판정 규칙] 몬스터(IDamageable)별 접촉 에피소드 1회 판정:
    /// 첫 접촉 순간의 속도·부위로 유효/무효를 확정하고, 접촉이 유지되는 동안 재판정하지 않는다
    /// (몸통→머리 훑기는 첫 접촉 부위만). 모든 볼륨에서 완전히 벗어나면 재공격 가능 목록으로 복귀.
    /// 무효타(vMin 미달)도 에피소드를 소모한다 — 살살 댄 채 가속해도 무효 유지, 떼었다 다시 쳐야 한다.
    ///
    /// Spec: 지시서 2단계 (§1-2, §1-3)
    /// </summary>
    public class MeleeWeapon : MonoBehaviour
    {
        private enum StrikeState { Idle, Attack, Recover }

        [SerializeField] private MeleeWeaponData m_data;
        [Tooltip("타격부 시작 기준점 (자루 쪽)")]
        [SerializeField] private Transform m_hitRefBase;
        [Tooltip("타격부 끝 기준점 (칼끝/창끝)")]
        [SerializeField] private Transform m_hitRefTip;
        [Tooltip("스윕 반경(m) — 타격부 두께. TODO(TUNING)")]
        [SerializeField] private float m_bladeRadius = 0.03f;
        [Tooltip("세그먼트 샘플 점 개수 (Base→Tip). TODO(TUNING)")]
        [SerializeField] private int m_sweepSamples = 4;
        [Tooltip("순간이동 판정 최소 변위(m/프레임). 변위와 속도 조건을 모두 넘어야 순간이동으로 간주 — " +
                 "로딩 히칭(긴 프레임)의 정상 이동은 변위가 커도 속도가 낮아 오탐되지 않는다. TODO(TUNING)")]
        [SerializeField] private float m_teleportThreshold = 1f;
        [Tooltip("순간이동 판정 속도 하한(m/s). 손 스윙 칼끝 실측 상한(~15-20m/s)보다 높게. TODO(TUNING)")]
        [SerializeField] private float m_teleportSpeedThreshold = 25f;
        [Tooltip("현재 상태 (인스펙터 확인용)")]
        [SerializeField] private string m_stateName = nameof(StrikeState.Idle);
        [Tooltip("사냥터 씬에서만 판정 활성 (§1-6, 씬 이름 기반 임시 — CombatScenes 주석 참조). " +
                 "프리팹은 true, 런타임 조립(봇)은 기본 false")]
        [SerializeField] private bool m_requireCombatScene;

        public MeleeWeaponData Data => m_data;
        public int CurrentDurability { get; private set; }
        public bool IsBroken => m_data != null && m_data.maxDurability > 0 && CurrentDurability <= 0;

        /// <summary>공격 주체 루트 — 기본은 무기 자신. step-10(홀스터)/step-12(네트워크)에서 플레이어로 설정.</summary>
        public GameObject Attacker { get; set; }

        /// <summary>유효타 발생 — (무기, 판정 정보, 피격 대상). 사운드/이펙트/햅틱 훅 지점. 무효타에는 발화하지 않는다.</summary>
        public event Action<MeleeWeapon, HitInfo, IDamageable> ValidHit;

        /// <summary>내구도 0 도달 — 홀스터 강제 귀환은 step-10에서 구독. 지금은 이벤트만.</summary>
        public event Action<MeleeWeapon> WeaponBroken;

        // 쿼리 무할당 버퍼 (90Hz — GC 회피, knowledge/unity-mobile-performance.md)
        private static readonly Collider[] s_overlapBuffer = new Collider[16];
        private static readonly RaycastHit[] s_castBuffer = new RaycastHit[16];

        // 몬스터별 접촉 추적 (§1-2). 값 = 이번 에피소드에서 판정 완료 여부(항상 true — 첫 접촉 즉시 판정).
        private readonly HashSet<IDamageable> m_activeContacts = new HashSet<IDamageable>();
        private readonly HashSet<IDamageable> m_contactsThisTick = new HashSet<IDamageable>();

        private Vector3[] m_prevSamplePositions;
        private float m_tipSpeed;
        private Vector3 m_prevTipPosition;
        private bool m_hasPrevFrame;
        private StrikeState m_state = StrikeState.Idle;

        /// <summary>
        /// Editor 팩토리·런타임 조립용 — 직렬화 필드 주입 (SerializedObject.FindProperty 회피).
        /// 런타임 조립(봇/하네스)은 AddComponent 시점에 Awake가 먼저 돌아 data가 없으므로,
        /// 여기서 내구도를 함께 초기화한다 — 안 하면 CurrentDurability 0 → IsBroken 오판으로 판정이 전부 스킵된다.
        /// </summary>
        public void Configure(MeleeWeaponData data, Transform hitRefBase, Transform hitRefTip)
        {
            m_data       = data;
            m_hitRefBase = hitRefBase;
            m_hitRefTip  = hitRefTip;
            if (m_data != null) CurrentDurability = m_data.maxDurability;
        }

        private void Awake()
        {
            if (Attacker == null) Attacker = gameObject;
            if (m_data != null) CurrentDurability = m_data.maxDurability;
            if (m_sweepSamples < 2) m_sweepSamples = 2;
            m_prevSamplePositions = new Vector3[m_sweepSamples];
        }

        /// <summary>
        /// 외부에서 무기를 명시적으로 순간이동시킬 때 호출 (홀스터 수납/귀환 step-10, 스폰 배치 등) —
        /// 이전 프레임 기준점을 무효화해 텔레포트 경로 스윕(유령 타격)을 원천 차단한다.
        /// 임계값 자동 가드(m_teleportThreshold)의 명시적 보완.
        /// </summary>
        public void NotifyTeleported()
        {
            m_hasPrevFrame = false;
            m_tipSpeed = 0f;
        }

        /// <summary>씬 제한 토글 — 팩토리(프리팹 true)/하네스 설정용.</summary>
        public bool RequireCombatScene
        {
            get => m_requireCombatScene;
            set => m_requireCombatScene = value;
        }

        /// <summary>내구도 전량 복구 — Blacksmith 수리 (§1-5).</summary>
        public void RepairFull()
        {
            if (m_data != null) CurrentDurability = m_data.maxDurability;
        }

        private void Update()
        {
            if (m_data == null || m_hitRefBase == null || m_hitRefTip == null) return;
            if (m_requireCombatScene && !CombatScenes.IsCombatScene()) return; // 무기 활성화는 사냥터 씬에서만 (§1-6)

            Vector3 basePos = m_hitRefBase.position;
            Vector3 tipPos  = m_hitRefTip.position;

            // 속도 실측 — Tip 프레임 차분 (물리 velocity 미사용)
            float dt = Time.deltaTime;
            float displacement = m_hasPrevFrame ? (tipPos - m_prevTipPosition).magnitude : 0f;
            if (m_hasPrevFrame && dt > 0f)
                m_tipSpeed = displacement / dt;

            // 순간이동 가드 — 텔레포트 경로가 스윕에 잡히면 유령 타격이 되므로 이 프레임 판정을 버린다.
            // 변위·속도 복합 조건: 히칭 프레임의 정상 스윙(변위 큼·속도 정상)은 판정을 유지한다.
            bool teleported = m_hasPrevFrame
                              && displacement > m_teleportThreshold
                              && m_tipSpeed > m_teleportSpeedThreshold;
            if (teleported) m_tipSpeed = 0f;

            if (m_prevSamplePositions.Length != m_sweepSamples)
                m_prevSamplePositions = new Vector3[m_sweepSamples];

            m_contactsThisTick.Clear();

            if (m_hasPrevFrame && !IsBroken && !teleported)
            {
                CollectSweepContacts(basePos, tipPos);
                CollectOverlapContacts(basePos, tipPos);
                ResolveContacts();
            }

            // 다음 프레임 기준점 갱신
            for (int i = 0; i < m_sweepSamples; i++)
                m_prevSamplePositions[i] = Vector3.Lerp(basePos, tipPos, i / (float)(m_sweepSamples - 1));
            m_prevTipPosition = tipPos;
            m_hasPrevFrame = true;

            UpdateStrikeState();
        }

        /// <summary>샘플 점별 "이전 위치 → 현재 위치" 스윕 — 고속 이동 경로 커버 (터널링 방지).</summary>
        private void CollectSweepContacts(Vector3 basePos, Vector3 tipPos)
        {
            for (int i = 0; i < m_sweepSamples; i++)
            {
                float t = i / (float)(m_sweepSamples - 1);
                Vector3 from = m_prevSamplePositions[i];
                Vector3 to   = Vector3.Lerp(basePos, tipPos, t);
                Vector3 delta = to - from;
                float dist = delta.magnitude;
                if (dist < 1e-5f) continue; // 정지 — 오버랩이 커버

                int count = Physics.SphereCastNonAlloc(from, m_bladeRadius, delta / dist,
                    s_castBuffer, dist, Physics.AllLayers, QueryTriggerInteraction.Collide);
                for (int c = 0; c < count; c++)
                    RegisterContact(s_castBuffer[c].collider);
            }
        }

        /// <summary>현재 세그먼트 오버랩 — 정지 접촉 유지/이탈 추적 보강.</summary>
        private void CollectOverlapContacts(Vector3 basePos, Vector3 tipPos)
        {
            int count = Physics.OverlapCapsuleNonAlloc(basePos, tipPos, m_bladeRadius,
                s_overlapBuffer, Physics.AllLayers, QueryTriggerInteraction.Collide);
            for (int c = 0; c < count; c++)
                RegisterContact(s_overlapBuffer[c]);
        }

        private void RegisterContact(Collider col)
        {
            if (col == null) return;
            var volume = col.GetComponent<HitVolume>();
            if (volume == null || volume.Owner == null) return;          // 쿼리 대상은 HitVolume만
            if (col.GetComponentInParent<MeleeWeapon>() == this) return; // 자기 자신 제외

            if (m_contactsThisTick.Add(volume.Owner) && !m_activeContacts.Contains(volume.Owner))
                JudgeFirstContact(volume); // 첫 접촉 부위만 인정 (§1-2) — 이 틱에 처음 만난 볼륨으로 확정
        }

        /// <summary>접촉 에피소드의 첫 접촉 1회 판정 — 유효/무효 확정 (§1-2, §1-3).</summary>
        private void JudgeFirstContact(HitVolume volume)
        {
            m_activeContacts.Add(volume.Owner);

            float speed = m_tipSpeed;
            if (speed < m_data.vMin)
            {
                // 무효타 — 데미지 0, 피드백 훅(ValidHit) 미호출. 로그만 무효 표시.
                Debug.Log($"[MeleeWeapon] invalid hit (speed {speed:F2} < vMin {m_data.vMin:F2}) — {name}");
                return;
            }

            if (!volume.Owner.IsDamageable) return; // Return 무적 등 — 판정 스킵 (step-03)

            var hit = new HitInfo
            {
                BaseDamage  = Mathf.RoundToInt(Mathf.Clamp(speed, m_data.vMin, m_data.vMax)
                                               * m_data.damageCoeff * m_data.damageConst),
                IsWeakpoint = volume.IsWeakpoint,
                Speed       = speed,
                Point       = volume.transform.position,
                Attacker    = Attacker,
            };

            volume.Owner.ApplyDamage(in hit);
            ValidHit?.Invoke(this, hit, volume.Owner);

            ConsumeDurability();
        }

        /// <summary>이번 틱에 안 보인 대상은 접촉에서 완전히 벗어남 — 재공격 가능 목록 복귀 (§1-2).</summary>
        private void ResolveContacts()
        {
            m_activeContacts.RemoveWhere(target => !m_contactsThisTick.Contains(target));
        }

        /// <summary>유효타 1회당 내구도 1 차감 (§1-5). 0 도달 시 WeaponBroken 발행.</summary>
        private void ConsumeDurability()
        {
            if (m_data.maxDurability <= 0) return;

            CurrentDurability--;
            if (CurrentDurability <= 0)
            {
                CurrentDurability = 0;
                Debug.Log($"[MeleeWeapon] broken — {name}");
                WeaponBroken?.Invoke(this);
            }
        }

        // Idle(비접촉) → Attack(이번 틱 신규 판정 발생) → Recover(접촉 유지) → Idle(전부 이탈)
        private void UpdateStrikeState()
        {
            StrikeState next;
            if (m_activeContacts.Count == 0)                 next = StrikeState.Idle;
            else if (m_state == StrikeState.Idle)            next = StrikeState.Attack;
            else                                             next = StrikeState.Recover;

            m_state = next;
            m_stateName = m_state.ToString();
        }
    }
}
