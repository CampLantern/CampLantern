using System.Collections.Generic;
using CampLantern.Combat;
using CampLantern.Combat.Data;
using CampLantern.Combat.Monsters;
using CampLantern.Combat.Player;
using CampLantern.Combat.Weapons;
using UnityEngine;

namespace CampLantern.Bootstrap
{
    /// <summary>
    /// 전투 샌드박스 IMGUI 디버그 하네스 (design/combat-detailed step-11).
    /// **IMGUI는 개발용 임시** — 실사용 UI는 VR UI 토대(VRUIPanel/VRUIButton)로 후속, Quest 빌드 전 제거 대상 (CLAUDE.md 관례).
    /// 하네스는 이벤트 구독·표시·주입만 한다 — 전투 로직 없음.
    ///
    /// 로컬 플레이어 프록시: 카메라(리그 머리)를 따라가는 PlayerHealth GO를 만들어 CombatPlayers에 등록 —
    /// 곰이 실제 시점을 감지/추격하고, 다운/소생/전멸을 IMGUI로 재현할 수 있다.
    /// 더미 플레이어: 어그로 2인/소생/전멸 테스트용 (HuntZoneHarness 더미 패턴의 로컬판).
    /// </summary>
    public class CombatSandboxHarness : MonoBehaviour
    {
        [SerializeField] private MonsterController m_monster;   // 팩토리 배선
        [SerializeField] private CombatBalanceData m_balance;   // 팩토리 배선

        private PlayerHealth m_localPlayer;
        private Transform m_localProxy;
        private readonly List<PlayerHealth> m_dummies = new List<PlayerHealth>();
        private PlayerReviver m_dummyReviver;
        private PartyWipeWatcher m_wipe;
        private ArrowQuiver m_quiver;
        private bool m_wiped;
        private string m_lastLog = "-";

        private void Start()
        {
            // 로컬 플레이어 프록시 — 리그 카메라 추종
            var proxyGo = new GameObject("LocalPlayerProxy");
            m_localProxy = proxyGo.transform;
            m_localPlayer = proxyGo.AddComponent<PlayerHealth>();
            m_localPlayer.Configure(100);
            CombatPlayers.Register(m_localProxy);

            // 화살집 — 씬의 Bow 인스턴스에 주입 (회수 기준점 = 프록시)
            m_quiver = proxyGo.AddComponent<ArrowQuiver>();
            m_quiver.Configure(m_balance, m_localProxy);
            foreach (var bow in FindObjectsByType<Bow>(FindObjectsSortMode.None))
            {
                bow.Quiver = m_quiver;
                bow.Attacker = proxyGo;
            }

            // 전멸 감시 — 샌드박스는 씬 로드 대신 라벨 표시
            m_wipe = proxyGo.AddComponent<PartyWipeWatcher>();
            m_wipe.Configure("Lobby", loadSceneOnWipe: false);
            m_wipe.PartyWiped -= OnWiped;
            m_wipe.PartyWiped += OnWiped;
        }

        private void OnDestroy()
        {
            if (m_wipe != null) m_wipe.PartyWiped -= OnWiped;
            if (m_localProxy != null) CombatPlayers.Unregister(m_localProxy);
            foreach (var d in m_dummies)
                if (d != null) CombatPlayers.Unregister(d.transform);
        }

        private void OnWiped() => m_wiped = true;

        private void Update()
        {
            // 프록시가 리그 시점을 따라간다 (수평 위치만 — 몬스터 추격 판정용)
            var cam = Camera.main;
            if (cam != null && m_localProxy != null)
            {
                Vector3 p = cam.transform.position;
                m_localProxy.position = new Vector3(p.x, 0f, p.z);
            }
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 360, 640));
            GUILayout.Label($"[전투 샌드박스] {m_lastLog}");
            if (m_wiped) GUILayout.Label("!! 전멸 — 사냥 실패 (씬 로드는 샌드박스에서 비활성) !!");

            // 몬스터
            if (m_monster != null && m_monster.Health != null)
            {
                var h = m_monster.Health;
                string aggro = m_monster.Aggro != null && m_monster.Aggro.CurrentTarget != null
                    ? $"{m_monster.Aggro.CurrentTarget.name} (고정 {m_monster.Aggro.LockRemainingSeconds:F1}s)" : "-";
                GUILayout.Label($"곰 HP {h.CurrentHp}/{h.MaxHp}  상태 {m_monster.CurrentStateName}  타겟 {aggro}");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("타격 20")) Hit(h, 20, false);
                if (GUILayout.Button("약점 20")) Hit(h, 20, true);
                if (GUILayout.Button("스턴 2s")) m_monster.ApplyStun(2f);
                if (GUILayout.Button("리셋")) h.ResetToFull();
                GUILayout.EndHorizontal();
            }

            // 로컬 플레이어
            GUILayout.Space(6);
            GUILayout.Label($"나 HP {m_localPlayer.CurrentHp}/{m_localPlayer.MaxHp}  {(m_localPlayer.IsDowned ? "다운!" : "정상")}");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("강제 다운")) m_localPlayer.ApplyDamage(new HitInfo { BaseDamage = 9999, Point = m_localProxy.position });
            if (m_dummyReviver != null && GUILayout.Button(m_dummyReviver.CurrentTarget != null
                    ? $"소생 중 {m_dummyReviver.Progress:P0}" : "더미로 나를 소생"))
                BeginReviveByDummy();
            GUILayout.EndHorizontal();
            GUILayout.Label($"화살 {(m_quiver != null ? m_quiver.Count : 0)}/{(m_quiver != null ? m_quiver.MaxArrows : 0)}");

            // 무기 내구도
            foreach (var melee in FindObjectsByType<MeleeWeapon>(FindObjectsSortMode.None))
                if (melee.Data != null)
                    GUILayout.Label($"{melee.name}: 내구도 {melee.CurrentDurability}/{melee.Data.maxDurability}{(melee.IsBroken ? " (파손)" : "")}");
            foreach (var bow in FindObjectsByType<Bow>(FindObjectsSortMode.None))
                if (bow.Data != null)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"{bow.name}: 내구도 {bow.CurrentDurability}/{bow.Data.maxDurability}");
                    if (GUILayout.Button("발사(풀드로우)")) { bow.SetDrawRatio(1f); m_lastLog = bow.TryFire() ? "발사!" : "발사 불가"; }
                    GUILayout.EndHorizontal();
                }

            // 더미 플레이어
            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("더미 추가")) AddDummy();
            if (m_dummies.Count > 0 && GUILayout.Button("더미 제거")) RemoveDummy();
            GUILayout.EndHorizontal();
            for (int i = 0; i < m_dummies.Count; i++)
            {
                var d = m_dummies[i];
                if (d == null) continue;
                GUILayout.BeginHorizontal();
                GUILayout.Label($"더미{i + 1} HP {d.CurrentHp} {(d.IsDowned ? "다운" : "")}", GUILayout.Width(140));
                if (GUILayout.Button("다운")) d.ApplyDamage(new HitInfo { BaseDamage = 9999, Point = d.transform.position });
                if (GUILayout.Button("몬스터 약점타"))
                {
                    if (m_monster != null)
                        m_monster.Health.ApplyDamage(new HitInfo { BaseDamage = 10, IsWeakpoint = true, Attacker = d.gameObject, Point = m_monster.transform.position });
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndArea();
        }

        private void Hit(MonsterHealth health, int damage, bool weakpoint)
        {
            health.ApplyDamage(new HitInfo
            {
                BaseDamage = damage,
                IsWeakpoint = weakpoint,
                Attacker = m_localPlayer.gameObject,
                Point = health.transform.position,
                Speed = 4f,
            });
        }

        private void AddDummy()
        {
            var go = new GameObject($"DummyPlayer_{m_dummies.Count + 1}");
            go.transform.position = m_localProxy.position + new Vector3(2f + m_dummies.Count, 0f, 1f);
            var health = go.AddComponent<PlayerHealth>();
            health.Configure(100);
            CombatPlayers.Register(go.transform);
            m_dummies.Add(health);

            if (m_dummyReviver == null)
            {
                m_dummyReviver = go.AddComponent<PlayerReviver>();
                m_dummyReviver.Configure(m_balance, go.transform);
            }
        }

        private void RemoveDummy()
        {
            var last = m_dummies[m_dummies.Count - 1];
            m_dummies.RemoveAt(m_dummies.Count - 1);
            if (last != null)
            {
                CombatPlayers.Unregister(last.transform);
                if (m_dummyReviver != null && m_dummyReviver.gameObject == last.gameObject) m_dummyReviver = null;
                Destroy(last.gameObject);
            }
        }

        private void BeginReviveByDummy()
        {
            if (m_dummyReviver == null || !m_localPlayer.IsDowned) { m_lastLog = "소생 불가 (다운 아님/더미 없음)"; return; }
            m_dummyReviver.transform.position = m_localProxy.position; // 손 접촉
            m_lastLog = m_dummyReviver.TryBeginRevive(m_localPlayer) ? "소생 홀드 시작 (3s)" : "소생 시작 실패";
        }
    }
}
