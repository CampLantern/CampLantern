using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace CampLantern.Networking
{
    /// <summary>
    /// 세션 시작 시 로컬 플레이어의 전투 존재(NetworkedCombatPlayer)를 스폰한다 —
    /// VoiceController/AvatarController와 동일한 SessionStarted 패턴 (네트워크 소셜 스택의 전투판).
    /// 전멸 관찰도 여기서: 모든 CombatPlayer의 [Networked] IsDowned가 참이면 각 클라가 독립적으로
    /// 동일 결론에 도달한다 (복제 상태 유도 — RPC 불필요, step-03 README 결정).
    /// 씬 로드(사냥 실패 복귀)는 하네스/후속 배선 몫 — 여기선 이벤트·로그까지.
    /// </summary>
    public class CombatPlayerSpawner : MonoBehaviour
    {
        [SerializeField] private SessionLauncher m_launcher;
        [SerializeField] private NetworkObject m_playerPrefab;
        [Tooltip("전멸 관찰 주기(초). TODO(TUNING)")]
        [SerializeField] private float m_wipeCheckInterval = 0.5f;

        private readonly List<NetworkedCombatPlayer> m_buffer = new List<NetworkedCombatPlayer>();
        private NetworkRunner m_runner;
        private float m_wipeTimer;
        private bool m_wipedFired;

        /// <summary>전멸 (전원 다운) — 각 클라에서 1회. 사냥 실패 처리(씬 복귀 등)는 구독 측.</summary>
        public event Action PartyWiped;

        private void Awake()
        {
            if (m_launcher == null) m_launcher = GetComponent<SessionLauncher>();
            if (m_launcher != null)
            {
                m_launcher.SessionStarted -= OnSessionStarted;
                m_launcher.SessionStarted += OnSessionStarted;
            }
        }

        private void OnDestroy()
        {
            if (m_launcher != null) m_launcher.SessionStarted -= OnSessionStarted;
        }

        private void OnSessionStarted(NetworkRunner runner)
        {
            m_runner = runner;
            if (m_playerPrefab == null)
            {
                Debug.LogError("[CombatPlayerSpawner] 플레이어 프리팹 미배선");
                return;
            }

            // 동기 Spawn — 멀티 피어에서는 EnqueueIncompleteSynchronousSpawns 설정으로 큐잉된다
            // (기존 HuntZoneHarness 스폰과 동일 경로, tech-stack-decisions 3종 세트)
            try
            {
                runner.Spawn(m_playerPrefab, Vector3.zero, Quaternion.identity, runner.LocalPlayer);
                Debug.Log($"[CombatPlayerSpawner] combat player spawn requested (P{runner.LocalPlayer.PlayerId})");
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
        }

        private void Update()
        {
            if (m_runner == null || !m_runner.IsRunning || m_wipedFired) return;

            m_wipeTimer -= Time.deltaTime;
            if (m_wipeTimer > 0f) return;
            m_wipeTimer = m_wipeCheckInterval;

            m_buffer.Clear();
            m_runner.GetAllBehaviours(m_buffer);
            if (m_buffer.Count == 0) return;

            for (int i = 0; i < m_buffer.Count; i++)
                if (m_buffer[i] != null && !m_buffer[i].IsDowned) return; // 생존자 있음

            m_wipedFired = true;
            Debug.Log("[CombatPlayerSpawner] party wiped — hunt failed (전원 다운)");
            PartyWiped?.Invoke();
        }
    }
}
