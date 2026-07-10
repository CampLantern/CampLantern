using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace CampLantern.Hunting
{
    /// <summary>
    /// PlayerRef → 플레이어 몸체(GameObject) 매핑 — 네트워크 전투의 공격자/타겟 신원 해석.
    /// 공급자: step-03 NetworkedCombatPlayer(Spawned/Despawned에서 등록/해제).
    /// 소비자: NetworkedHuntMonster(원격 공격자의 기여·어그로 신원을 GameObject로 해석 —
    /// Combat 코어의 GameObject 기반 기여 Set/어그로를 무수정 재사용하기 위한 브리지).
    /// 미등록 PlayerRef는 소비자가 프록시로 폴백한다 (step-01 단독으로도 동작).
    ///
    /// RULE-01: Domain Reload 비활성 — static 상태는 SubsystemRegistration에서 초기화.
    /// </summary>
    public static class NetworkedPlayerBodies
    {
        private static readonly Dictionary<PlayerRef, GameObject> s_bodies = new Dictionary<PlayerRef, GameObject>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_bodies.Clear();
        }

        public static void Register(PlayerRef player, GameObject body)
        {
            if (body != null) s_bodies[player] = body;
        }

        public static void Unregister(PlayerRef player)
        {
            s_bodies.Remove(player);
        }

        public static GameObject Resolve(PlayerRef player)
        {
            return s_bodies.TryGetValue(player, out var body) && body != null ? body : null;
        }
    }
}
