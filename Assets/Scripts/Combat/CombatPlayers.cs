using System.Collections.Generic;
using UnityEngine;

namespace CampLantern.Combat
{
    /// <summary>
    /// 전투가 인지하는 플레이어 목록 — 임시 공급자 (step-05 어그로가 주 소비처, step-12에서 네트워크 목록으로 교체).
    /// 등록 주체: 하네스/검증 봇 (실 리그 등록 배선은 step-05·step-11에서).
    ///
    /// 레지스트리 방식 근거: FindObjectsOfType류 매 틱 호출은 성능 금지(90Hz), 태그 방식은
    /// ProjectSettings(TagManager) 수정이 필요해 RULE-04에 걸린다.
    ///
    /// RULE-01 주의: 이 프로젝트는 Domain Reload 비활성 — static 상태는 플레이 세션 간 잔존하므로
    /// SubsystemRegistration 초기화로 클리어한다 (knowledge/unity-editor-automation.md 관례).
    /// </summary>
    public static class CombatPlayers
    {
        private static readonly List<Transform> s_players = new List<Transform>();

        public static IReadOnlyList<Transform> All => s_players;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_players.Clear();
        }

        public static void Register(Transform player)
        {
            if (player != null && !s_players.Contains(player))
                s_players.Add(player);
        }

        public static void Unregister(Transform player)
        {
            s_players.Remove(player);
        }

        /// <summary>가장 가까운 플레이어 (평면 거리). 파괴된 항목은 지나가며 정리한다.</summary>
        public static Transform FindNearest(Vector3 from)
        {
            Transform nearest = null;
            float bestSqr = float.MaxValue;

            for (int i = s_players.Count - 1; i >= 0; i--)
            {
                Transform t = s_players[i];
                if (t == null) { s_players.RemoveAt(i); continue; }

                Vector3 to = t.position - from;
                to.y = 0f;
                float sqr = to.sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    nearest = t;
                }
            }
            return nearest;
        }
    }
}
