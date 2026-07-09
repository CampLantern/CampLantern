using CampLantern.Combat.Data;
using CampLantern.Combat.Monsters;
using UnityEngine;

namespace CampLantern.Combat.Weapons
{
    /// <summary>
    /// 근접 유효타 무인 검증 봇 — FishingLoopSelfTest 전례 (knowledge/unity-editor-automation.md 봇 주입 패턴).
    /// 플레이 중 브릿지로 AddComponent하면 스스로 더미 몬스터·무기를 코드로 조립해 4개 페이즈를 주행하고
    /// [MeleeTest] 태그로 결과를 남긴다. 씬 의존 없음 (프리팹 미사용 — 실제 쿼리 경로를 그대로 검증).
    ///
    /// 페이즈: 1) 유효타(빠른 스윙, 몸통) 2) 무효타(느린 접촉 — 데미지 없어야)
    ///         3) 약점(빠른 스윙, 머리 — ×1.5) 4) 재접촉 규칙(접촉 유지 중 재판정 없음 → 이탈 후 재타격 가능)
    /// </summary>
    public class MeleeSwingSelfTest : MonoBehaviour
    {
        private const string k_tag = "[MeleeTest]";

        // 테스트 전용 데이터 (에셋 미사용 — 기대값 계산이 코드 안에서 닫히도록)
        private const float k_vMin = 1.5f;
        private const float k_vMax = 6f;
        private const float k_coeff = 10f;
        private const float k_const = 0.25f;

        private MeleeWeapon m_weapon;
        private MonsterHealth m_dummy; // step-03에서 DummyMonster → MonsterHealth로 대체
        private Transform m_weaponRoot;
        private Vector3 m_bodyCenter;
        private Vector3 m_headCenter;

        private int m_phase;
        private float m_phaseTime;
        private int m_hpBeforePhase;
        private int m_validHits;
        private bool m_failed;
        private int m_dwellStage;
        private float m_dwellTimer;
        private float m_warmup = 3f;

        private void Start()
        {
            BuildDummy();
            BuildWeapon();
            m_hpBeforePhase = m_dummy.CurrentHp;
            Debug.Log($"{k_tag} start — 4 phases");
        }

        private void OnDestroy()
        {
            if (m_weapon != null) m_weapon.ValidHit -= OnValidHit;
        }

        private void BuildDummy()
        {
            var monsterData = ScriptableObject.CreateInstance<MonsterData>();
            monsterData.baseHp = 100;
            monsterData.weakpointMultiplier = 1.5f;

            var root = new GameObject("MeleeTest_Dummy");
            root.transform.position = new Vector3(1000f, 0f, 1000f); // 씬 내용과 격리된 좌표
            m_dummy = root.AddComponent<MonsterHealth>();
            m_dummy.Configure(monsterData);

            m_bodyCenter = root.transform.position + new Vector3(0f, 1f, 0f);
            m_headCenter = root.transform.position + new Vector3(0f, 2.2f, 0f);

            var body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);
            body.transform.position = m_bodyCenter;
            var bodyCol = body.AddComponent<CapsuleCollider>();
            bodyCol.height = 1.6f;
            bodyCol.radius = 0.4f;
            body.AddComponent<HitVolume>().Configure(isWeakpoint: false);

            var head = new GameObject("Head");
            head.transform.SetParent(root.transform, false);
            head.transform.position = m_headCenter;
            var headCol = head.AddComponent<SphereCollider>();
            headCol.radius = 0.3f;
            head.AddComponent<HitVolume>().Configure(isWeakpoint: true);
        }

        private void BuildWeapon()
        {
            var data = ScriptableObject.CreateInstance<MeleeWeaponData>();
            data.kind = WeaponKind.Sword;
            data.damageCoeff = k_coeff;
            data.damageConst = k_const;
            data.vMin = k_vMin;
            data.vMax = k_vMax;
            data.maxDurability = 30;

            var root = new GameObject("MeleeTest_Sword");
            m_weaponRoot = root.transform;

            var hitBase = new GameObject("HitRef_Base").transform;
            hitBase.SetParent(root.transform, false);
            hitBase.localPosition = new Vector3(0f, 0f, 0.3f);
            var hitTip = new GameObject("HitRef_Tip").transform;
            hitTip.SetParent(root.transform, false);
            hitTip.localPosition = new Vector3(0f, 0f, 1f);

            m_weapon = root.AddComponent<MeleeWeapon>();
            m_weapon.Configure(data, hitBase, hitTip);
            m_weapon.ValidHit -= OnValidHit;
            m_weapon.ValidHit += OnValidHit;

            ResetWeaponPosition();
        }

        private void ResetWeaponPosition()
        {
            // 몸통 좌측 2m 밖, 칼끝이 +X로 이동하며 몸통을 통과하는 배치.
            // 순간이동은 반드시 NotifyTeleported로 통지 — 텔레포트 경로 유령 스윕 차단 (홀스터 step-10과 동일 규약).
            // 자동 가드(변위+속도)는 히칭 프레임과 겹친 텔레포트(속도가 스윙 수준으로 떨어짐)를 원리적으로 못 잡는다.
            m_weaponRoot.position = m_bodyCenter + new Vector3(-2f, 0f, -0.65f);
            m_weaponRoot.rotation = Quaternion.identity;
            if (m_weapon != null) m_weapon.NotifyTeleported();
        }

        private void OnValidHit(MeleeWeapon weapon, HitInfo hit, IDamageable target)
        {
            m_validHits++;
        }

        private void Update()
        {
            if (m_phase > 4) return; // 실패해도 남은 페이즈 주행 — 최종 RESULT 요약까지 출력

            // 워밍업 — 플레이 진입 직후 아바타 로딩 히칭 구간을 피해 측정 안정화
            if (m_warmup > 0f) { m_warmup -= Time.deltaTime; return; }

            m_phaseTime += Time.deltaTime;

            switch (m_phase)
            {
                case 0: RunSweepPhase(speed: 4f, targetY: m_bodyCenter.y, next: () =>
                    VerifyDamagePhase("PHASE1_VALID_BODY", expectWeakpoint: false)); break;
                case 1: RunSweepPhase(speed: 0.5f, targetY: m_bodyCenter.y, next: () =>
                    VerifyNoDamagePhase("PHASE2_INVALID_SLOW")); break;
                case 2: RunSweepPhase(speed: 4f, targetY: m_headCenter.y, next: () =>
                    VerifyDamagePhase("PHASE3_WEAKPOINT_HEAD", expectWeakpoint: true)); break;
                case 3: RunDwellPhase(); break;
                case 4: Finish(); break;
            }

            if (m_phaseTime > 30f) Fail($"phase {m_phase} timeout");
        }

        /// <summary>무기를 +X로 등속 이동시켜 표적을 통과시킨다. 4m 이동 후 판정 콜백 실행.</summary>
        private void RunSweepPhase(float speed, float targetY, System.Action next)
        {
            var pos = m_weaponRoot.position;
            pos.y = Mathf.Lerp(pos.y, targetY, 0.5f); // 목표 높이로 정렬
            pos.x += speed * Time.deltaTime;
            m_weaponRoot.position = pos;

            if (pos.x > m_bodyCenter.x + 2f)
                next();
        }

        private void VerifyDamagePhase(string label, bool expectWeakpoint)
        {
            int dealt = m_hpBeforePhase - m_dummy.CurrentHp;
            // 기대값: clamp(4, 1.5, 6) × 10 × 0.25 = 10 (약점이면 ×1.5 = 15). 프레임 차분 오차 ±2 허용.
            int expected = expectWeakpoint ? 15 : 10;
            bool ok = Mathf.Abs(dealt - expected) <= 2 && m_validHits >= 1;
            Report(label, ok, $"dealt={dealt} expected~{expected} validHits={m_validHits}");
            AdvancePhase();
        }

        private void VerifyNoDamagePhase(string label)
        {
            int dealt = m_hpBeforePhase - m_dummy.CurrentHp;
            bool ok = dealt == 0 && m_validHits == 0;
            Report(label, ok, $"dealt={dealt} validHits={m_validHits} (expected 0/0)");
            AdvancePhase();
        }

        /// <summary>
        /// 페이즈 4: 몸통 안에서 정지(접촉 유지 — 재판정 없어야) 후 이탈 완료까지 유효타가 진입 1회뿐인지 확정.
        /// 이탈 후 재타격 가능(재공격 목록 복귀)은 페이즈 1~3이 매번 이탈→재접촉으로 이미 증명한다.
        /// </summary>
        private void RunDwellPhase()
        {
            switch (m_dwellStage)
            {
                case 0: // 진입 — 몸통 중심 x 도달까지 이동 (유효타 1회 발생 지점)
                    MoveX(4f);
                    if (m_weaponRoot.position.x >= m_bodyCenter.x) { m_dwellStage = 1; m_dwellTimer = 0f; }
                    break;
                case 1: // 몸통 내부 정지 2초 — 추가 판정 없어야 (에피소드 유지)
                    m_dwellTimer += Time.deltaTime;
                    if (m_dwellTimer > 2f) m_dwellStage = 2;
                    break;
                case 2: // 이탈
                    MoveX(4f);
                    if (m_weaponRoot.position.x > m_bodyCenter.x + 2f)
                    {
                        Report("PHASE4_SINGLE_JUDGMENT", m_validHits == 1,
                               $"validHits={m_validHits} (expected 1 — dwell 중 재판정 금지)");
                        m_dwellStage = 0;
                        AdvancePhase();
                    }
                    break;
            }

            void MoveX(float speed)
            {
                var pos = m_weaponRoot.position;
                pos.y = m_bodyCenter.y;
                pos.x += speed * Time.deltaTime;
                m_weaponRoot.position = pos;
            }
        }

        private void AdvancePhase()
        {
            m_phase++;
            m_phaseTime = 0f;
            m_validHits = 0;
            m_dummy.ResetToFull();
            m_hpBeforePhase = m_dummy.CurrentHp;
            ResetWeaponPosition();
        }

        private void Report(string label, bool ok, string detail)
        {
            if (!ok) m_failed = true;
            Debug.Log($"{k_tag} {label} {(ok ? "PASS" : "FAIL")} — {detail}");
        }

        private void Fail(string reason)
        {
            m_failed = true;
            Debug.Log($"{k_tag} FAIL — {reason}");
            enabled = false;
        }

        private void Finish()
        {
            Debug.Log($"{k_tag} {(m_failed ? "RESULT FAIL" : "RESULT ALL PASS")}");
            enabled = false;
        }
    }
}
