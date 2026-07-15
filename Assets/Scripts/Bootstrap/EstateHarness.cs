using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CampLantern.Cooking;
using CampLantern.Core;
using CampLantern.Core.Persistence;
using CampLantern.Estate;
using CampLantern.Networking;
using CampLantern.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CampLantern.Bootstrap
{
    /// <summary>
    /// 영지 — 개인 소유 공간, roomName = estate_{ownerId} (room-architecture.md).
    /// 주인 오프라인이어도 방문 가능해야 하는데, 유저 인증·영구 저장 백엔드가 아직 미정이라
    /// (tech-stack-decisions.md) P0에서는 기기별 임시 식별자로 자기 영지에만 접속하는 템플릿이다.
    /// 코인/인벤토리/배치는 PlayerState로 로컬 JSON에 저장·복원된다 — 다른 유저가 오프라인 주인의
    /// 영지를 읽는 것(진짜 오프라인 방문)은 여전히 서버가 필요해 이걸로 해결되지 않는다.
    /// </summary>
    public class EstateHarness : MonoBehaviour
    {
        [SerializeField] private EstateManager m_estateManager;
        [SerializeField] private CookingPot m_pot;
        [SerializeField] private SessionLauncher m_launcher;
        [SerializeField] private EstateObjectDef[] m_estateCatalog;
        [SerializeField] private string m_lobbySceneName = "Lobby";
        [SerializeField] private int m_startingCoins = 100;
        [SerializeField] private Vector3 m_placeOrigin = new Vector3(3f, 0f, 3f);

        private PlayerState m_state;
        private ContentRegistry m_registry;

        private bool m_joining;
        private string m_lastLog = "-";
        private WristHud m_wristHud; // 리그(DontDestroyOnLoad)에 붙어서 씬 이탈 시 직접 파괴해야 함

        /// <summary>테스트/디버그 조회용 — 저장 라운드트립 자동 검증에 사용.</summary>
        public PlayerState State => m_state;

        private void Awake()
        {
            m_registry = Resources.Load<ContentRegistry>("ContentRegistry");
            if (m_registry == null)
                Debug.LogError("[EstateHarness] ContentRegistry 없음 — Tools > Make Assets > Content Registry 실행 필요");

            bool isNewSave = !SaveService.Exists(); // 최초 실행에만 시작 코인 지급 — 이후엔 저장값이 우선

            m_state = new PlayerState();
            if (m_registry != null) m_state.Load(m_registry);

            if (isNewSave && m_startingCoins > 0) m_state.Wallet.Add(m_startingCoins);
        }

        private void Start()
        {
            // 손목 HUD — 코인 (구매/판매 피드백). 리그(DontDestroyOnLoad)에 붙으므로 파괴는 하네스 책임
            m_wristHud = WristHud.Spawn(m_state.Wallet);

            m_pot.Initialize(m_state.Inventory);
            m_pot.Cooked -= OnCooked;
            m_pot.Cooked += OnCooked;

            m_estateManager.Bind(m_state.Shop);

            // 저장된 배치를 복원 — 보유 목록(Shop.OwnedDefs)과 별개로 이미 배치된 것만 Place로 재현
            if (m_registry != null)
            {
                foreach (PlacedObjectSave saved in m_state.PendingPlacements)
                {
                    if (!m_registry.TryGetEstateObject(saved.DefId, out EstateObjectDef def))
                    {
                        Debug.LogWarning($"[EstateHarness] 저장된 배치 오브젝트 Id를 찾을 수 없음: {saved.DefId}");
                        continue;
                    }

                    if (m_estateManager.Place(def, saved.Position, saved.Rotation) == null)
                    {
                        // 수용량 초과 등으로 복원 실패 — 데이터 유실 방지를 위해 보유 목록으로 되돌린다
                        m_state.Shop.ReturnOwned(def);
                        Debug.LogWarning($"[EstateHarness] 저장된 배치 복원 실패(수용량 초과 등) — 보유 목록으로 반환: {saved.DefId}");
                    }
                }
            }
        }

        private void OnDestroy()
        {
            m_pot.Cooked -= OnCooked;
            if (m_wristHud != null) Destroy(m_wristHud.gameObject); // 리그에 붙어 있어 씬 언로드로 안 죽는다
        }

        private void OnApplicationQuit()
        {
            m_state.Save(m_estateManager);
        }

        private void ReturnToLobby()
        {
            m_state.Save(m_estateManager);
            SceneManager.LoadScene(m_lobbySceneName);
        }

        private void OnCooked(ItemDef result)
        {
            m_lastLog = $"조리 결과: {result.DisplayName}";
            m_state.Save(m_estateManager); // OS 강제종료 대비 — 재료 소모·결과물 반영 즉시 저장
        }

        // 영지 소유자 식별 — 인증 백엔드 확정 전 임시값 (SystemInfo.deviceUniqueIdentifier).
        // 실제 유저 계정 도입 시 이 부분을 로그인 결과로 교체할 것.
        private static string OwnerId => SystemInfo.deviceUniqueIdentifier;

        private async Task JoinAsync()
        {
            m_joining = true;
            try
            {
                await m_launcher.StartSession($"estate_{OwnerId}", destroyCancellationToken);
                m_lastLog = "영지 접속 완료";
            }
            catch (OperationCanceledException) { /* 파괴로 인한 취소 — 정상 */ }
            catch (Exception e)
            {
                m_lastLog = $"접속 실패: {e.Message}";
                Debug.LogException(e, this);
            }
            finally
            {
                m_joining = false;
            }
        }

        private void OnGUI()
        {
            GUILayout.Label($"[영지] 코인: {m_state.Wallet.Coins}  |  {m_lastLog}");
            if (GUILayout.Button("로비로 복귀")) ReturnToLobby();
            GUILayout.Space(8);

            var runner = m_launcher.Runner;
            if (runner == null)
            {
                GUI.enabled = !m_joining;
                if (GUILayout.Button(m_joining ? "접속 중..." : "내 영지 접속"))
                    _ = JoinAsync();
                GUI.enabled = true;
            }
            else
            {
                GUILayout.Label($"접속: {runner.SessionInfo.Name} ({runner.SessionInfo.PlayerCount}명) — 오프라인 방문은 백엔드 TBD");
            }

            GUILayout.Space(8);
            GUILayout.Label("── 인벤토리 ── (투입=냄비, 판매=코인)");
            foreach (KeyValuePair<ItemDef, int> entry in new List<KeyValuePair<ItemDef, int>>(m_state.Inventory.Items))
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{entry.Key.DisplayName} x{entry.Value}", GUILayout.Width(140));
                if (GUILayout.Button("투입", GUILayout.Width(60))) m_pot.TryAddIngredient(entry.Key);
                if (GUILayout.Button($"판매 {entry.Key.SellPrice}c", GUILayout.Width(90)) &&
                    m_state.Inventory.TryRemove(entry.Key))
                {
                    m_state.Wallet.Add(entry.Key.SellPrice);
                    m_state.Save(m_estateManager); // 판매 즉시 저장
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(8);
            var names = new List<string>();
            foreach (ItemDef ingredient in m_pot.Ingredients) names.Add(ingredient.DisplayName);
            GUILayout.Label($"── 요리 ── 냄비: [{string.Join(", ", names)}]");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("조리")) m_pot.Cook();
            if (GUILayout.Button("비우기")) m_pot.Clear();
            GUILayout.EndHorizontal();

            GUILayout.Space(8);
            GUILayout.Label($"── 영지 배치 ── 수용량: {m_estateManager.CapacityUsed}/{m_estateManager.CapacityMax}");
            foreach (EstateObjectDef def in m_estateCatalog)
            {
                if (def == null) continue;
                GUILayout.BeginHorizontal();
                string material = def.RequiredMaterial != null
                    ? $" + {def.RequiredMaterial.DisplayName} x{def.RequiredMaterialCount}" : "";
                GUILayout.Label($"{def.DisplayName} ({def.CoinCost}c{material})", GUILayout.Width(180));
                if (def.Rarity == Rarity.Epic)
                {
                    GUILayout.Label("이벤트 전용");
                }
                else
                {
                    if (GUILayout.Button("구매", GUILayout.Width(50)))
                    {
                        bool purchased = m_state.Shop.TryPurchase(def);
                        m_lastLog = purchased ? $"구매: {def.DisplayName}" : "구매 실패 (재화 부족)";
                        if (purchased) m_state.Save(m_estateManager); // 구매 즉시 저장
                    }
                    int owned = m_state.Shop.CountOwned(def);
                    if (owned > 0 && GUILayout.Button($"배치({owned})", GUILayout.Width(70)))
                        TryPlace(def);
                }
                GUILayout.EndHorizontal();
            }

            if (m_estateManager.PlacedObjects.Count > 0 && GUILayout.Button("마지막 배치물 회수"))
            {
                m_estateManager.Remove(m_estateManager.PlacedObjects[m_estateManager.PlacedObjects.Count - 1]);
                m_state.Save(m_estateManager); // 회수(보유 반환) 즉시 저장
            }
        }

        private void TryPlace(EstateObjectDef def)
        {
            if (!m_estateManager.CanPlace(def))
            {
                m_lastLog = "배치 실패 — 수용량 초과";
                return;
            }
            if (!m_state.Shop.TryConsumeOwned(def)) return;

            int index = m_estateManager.PlacedObjects.Count;
            Vector3 pos = m_placeOrigin + new Vector3((index % 4) * 2f, 0f, (index / 4) * 2f);
            PlacedObject placed = m_estateManager.Place(def, pos, Quaternion.identity);
            if (placed == null)
            {
                m_state.Shop.ReturnOwned(def);
            }
            else
            {
                m_lastLog = $"배치: {def.DisplayName}";
                m_state.Save(m_estateManager); // 배치 즉시 저장
            }
        }
    }
}
