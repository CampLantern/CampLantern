using System.Collections;
using UnityEngine;

namespace CampLantern.Combat.Player
{
    /// <summary>
    /// 디버프 연출 훅 (§2-2) — 화면 테두리 색 오버레이: 기절=보라, 독=초록.
    /// 기계 효과: 기절 = 입력 잠금 플래그(InputLocked — **머리 회전은 자유: VR 멀미 방지**, 실제 입력
    /// 차단 배선은 VR 어댑터 몫) / 독 = 틱 데미지 코루틴. 지속·틱 수치는 SO 자리(CombatBalanceData
    /// 디버프 필드 — TODO(SPEC), 부여 주체 미정이라 파라미터로도 받는다).
    ///
    /// 렌더 방식 — 카메라 자식 반투명 쿼드: VR에서 UGUI 스크린스페이스가 없으므로 카메라 근평면 앞
    /// 쿼드가 가장 단순·리그 무관. 비주얼 완성도는 임시(단색 알파) — 연출 폴리시는 후속. 근거 주석.
    /// GameObject 활성화는 이 컴포넌트(소유자)가 관리 (rules/scripts.md — 자기 Awake에서 SetActive(false) 금지 원칙 준수).
    /// Spec: 지시서 7단계 (§2-2)
    /// </summary>
    public class ScreenTintOverlay : MonoBehaviour
    {
        private static readonly Color k_stunColor = new Color(0.5f, 0.2f, 0.8f, 0.35f);   // 보라
        private static readonly Color k_poisonColor = new Color(0.2f, 0.8f, 0.3f, 0.35f); // 초록

        [Tooltip("오버레이를 붙일 카메라 — null이면 Camera.main")]
        [SerializeField] private Camera m_camera;

        private Renderer m_quadRenderer;
        private Coroutine m_tintRoutine;
        private Coroutine m_poisonRoutine;

        /// <summary>기절 입력 잠금 플래그 — VR 입력 어댑터가 읽는다. 머리 회전은 잠그지 않는다(멀미 방지).</summary>
        public bool InputLocked { get; private set; }

        private void Awake()
        {
            if (m_camera == null) m_camera = Camera.main;
            EnsureQuad();
            if (m_quadRenderer != null) m_quadRenderer.gameObject.SetActive(false); // 소유자가 초기 비활성 관리
        }

        /// <summary>기절 연출 — 보라 오버레이 + 입력 잠금 (§2-2).</summary>
        public void ShowStun(float durationSeconds)
        {
            InputLocked = true;
            StartTint(k_stunColor, durationSeconds, unlockInputAfter: true);
        }

        /// <summary>독 연출 — 초록 오버레이 + 틱 데미지 (§2-2). 수치는 부여 주체 확정 전 파라미터. TODO(SPEC)</summary>
        public void ShowPoison(PlayerHealth target, int damagePerTick, float tickInterval, float durationSeconds)
        {
            StartTint(k_poisonColor, durationSeconds, unlockInputAfter: false);

            if (m_poisonRoutine != null) StopCoroutine(m_poisonRoutine);
            m_poisonRoutine = StartCoroutine(PoisonTicks(target, damagePerTick, tickInterval, durationSeconds));
        }

        private void StartTint(Color color, float duration, bool unlockInputAfter)
        {
            EnsureQuad();
            if (m_quadRenderer == null) return;

            m_quadRenderer.material.color = color;
            m_quadRenderer.gameObject.SetActive(true);

            if (m_tintRoutine != null) StopCoroutine(m_tintRoutine);
            m_tintRoutine = StartCoroutine(HideAfter(duration, unlockInputAfter));
        }

        private IEnumerator HideAfter(float duration, bool unlockInput)
        {
            yield return new WaitForSeconds(duration);
            if (m_quadRenderer != null) m_quadRenderer.gameObject.SetActive(false);
            if (unlockInput) InputLocked = false;
        }

        private IEnumerator PoisonTicks(PlayerHealth target, int damagePerTick, float tickInterval, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration && target != null && target.IsDamageable)
            {
                yield return new WaitForSeconds(tickInterval);
                elapsed += tickInterval;
                target.ApplyDamage(new HitInfo { BaseDamage = damagePerTick, Point = target.transform.position });
            }
        }

        // 카메라 근평면 앞 반투명 쿼드 — 콜라이더 제거(판정 오염 방지)
        private void EnsureQuad()
        {
            if (m_quadRenderer != null || m_camera == null) return;

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "ScreenTintQuad";
            Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(m_camera.transform, false);
            quad.transform.localPosition = new Vector3(0f, 0f, m_camera.nearClipPlane + 0.05f);
            quad.transform.localScale = Vector3.one * 2f;

            m_quadRenderer = quad.GetComponent<Renderer>();
            var shader = Shader.Find("Sprites/Default"); // 알파 지원 — URP 호환 내장
            if (shader != null) m_quadRenderer.material = new Material(shader);
        }
    }
}
