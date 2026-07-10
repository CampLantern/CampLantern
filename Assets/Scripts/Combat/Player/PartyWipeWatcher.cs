using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CampLantern.Combat.Player
{
    /// <summary>
    /// 전멸 감시 (§2) — 씬 내 플레이어(PlayerHealth.All) 전원 다운 → 사냥 실패 → 지정 Scene 로드.
    /// Scene 이름은 설정값 (지시서 명시 — 기본 "Lobby", 기존 하네스 m_lobbySceneName 관례).
    /// 검증 봇은 m_loadSceneOnWipe=false로 이벤트만 검사한다.
    /// Spec: 지시서 7단계 (§2 전멸)
    /// </summary>
    public class PartyWipeWatcher : MonoBehaviour
    {
        [Tooltip("전멸 시 로드할 씬 (설정값 — 지시서 명시)")]
        [SerializeField] private string m_failSceneName = "Lobby";
        [Tooltip("전멸 시 실제 씬 로드 수행 여부 — 검증 봇/샌드박스는 끔")]
        [SerializeField] private bool m_loadSceneOnWipe = true;
        [Tooltip("감시 주기(초). TODO(TUNING)")]
        [SerializeField] private float m_checkInterval = 0.5f;

        private float m_timer;
        private bool m_wiped;

        /// <summary>전멸 발생 (1회). 구독 해제 필수.</summary>
        public event Action PartyWiped;

        public void Configure(string failSceneName, bool loadSceneOnWipe)
        {
            m_failSceneName = failSceneName;
            m_loadSceneOnWipe = loadSceneOnWipe;
        }

        private void Update()
        {
            if (m_wiped) return;

            m_timer -= Time.deltaTime;
            if (m_timer > 0f) return;
            m_timer = m_checkInterval;

            var players = PlayerHealth.All;
            if (players.Count == 0) return;

            for (int i = 0; i < players.Count; i++)
                if (players[i] != null && !players[i].IsDowned) return; // 한 명이라도 생존 — 전멸 아님

            m_wiped = true;
            Debug.Log($"[PartyWipe] all players downed — hunt failed (scene={m_failSceneName}, load={m_loadSceneOnWipe})");
            PartyWiped?.Invoke();

            if (m_loadSceneOnWipe)
                SceneManager.LoadScene(m_failSceneName);
        }
    }
}
