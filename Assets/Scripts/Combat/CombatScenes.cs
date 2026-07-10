using UnityEngine.SceneManagement;

namespace CampLantern.Combat
{
    /// <summary>
    /// 무기 활성화 씬 제한 (§1-6) — **Scene 이름 기반 임시 판정** (지시서 명시: 임시임을 주석으로 명시).
    /// 사냥터(HuntZone_A)와 로컬 검증 샌드박스에서만 무기 사용 허용. 존 확장/매칭 백엔드 도입 시
    /// 씬 메타데이터(존 타입) 기반으로 대체 예정.
    /// </summary>
    public static class CombatScenes
    {
        private static readonly string[] k_combatSceneNames = { "HuntZone_A", "CombatSandbox" };

        // 테스트 심 — 검증 봇이 활성 씬과 무관하게 게이트 분기를 검증할 때만 사용
        private static bool? s_testOverride;

        public static bool IsCombatScene()
        {
            if (s_testOverride.HasValue) return s_testOverride.Value;

            string active = SceneManager.GetActiveScene().name;
            for (int i = 0; i < k_combatSceneNames.Length; i++)
                if (k_combatSceneNames[i] == active) return true;
            return false;
        }

        /// <summary>검증 봇 전용 — 게이트 강제 (null = 해제). 게임 코드에서 호출 금지.</summary>
        public static void SetTestOverride(bool? allowed)
        {
            s_testOverride = allowed;
        }
    }
}
