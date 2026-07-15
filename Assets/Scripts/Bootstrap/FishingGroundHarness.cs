using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CampLantern.Core;
using CampLantern.Core.Persistence;
using CampLantern.Fishing;
using CampLantern.Networking;
using CampLantern.Networking.Voice;
using CampLantern.UI;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CampLantern.Bootstrap
{
    /// <summary>
    /// 낚시터 — 싱글/멀티 선택, 상시 열림, 파티 구성 없이 드롭인 (room-architecture.md).
    /// P0는 실제 매칭/샤딩 없이 고정 샤드 이름("fishing_{m_shardId}")으로만 합류한다 —
    /// "정원 도달 시 새 인스턴스로 샤딩"은 매칭 백엔드가 필요해 P0 범위 밖.
    /// 인벤토리는 PlayerState를 통해 로컬 JSON에 저장/복원된다 — 다른 공간(영지 등)과 이어짐
    /// (오프라인 방문 같은 멀티유저 공유는 여전히 서버 필요, tech-stack-decisions.md).
    /// </summary>
    public class FishingGroundHarness : MonoBehaviour
    {
        [SerializeField] private FishingRod m_rod;
        [SerializeField] private FishingSpot m_spot;
        [SerializeField] private SessionLauncher m_launcher;
        [SerializeField] private string m_lobbySceneName = "Lobby";
        [SerializeField] private string m_shardId = "shard0";

        private PlayerState m_state;
        private ContentRegistry m_registry;
        private bool m_joining;
        private string m_lastLog = "-";

        private VoiceController m_voice;
        private PlayerMute m_mute;
        private InventoryPanel m_inventoryPanel;
        private FishResultPanel m_resultPanel;
        private WristHud m_wristHud; // 리그(DontDestroyOnLoad)에 붙어서 씬 이탈 시 직접 파괴해야 함
        private ActionListPanel m_shopPanel; // 낚시 상점 (미끼 구매·수리)
        private ToastHud m_toast;            // 시야 하단 알림 — 리그 부착이라 하네스가 파괴 책임
        private int m_xp; // TODO(DATA): XP 용처·저장 미정(§9) — 세션 로컬 누적 표시만

        private void Awake()
        {
            m_registry = Resources.Load<ContentRegistry>("ContentRegistry");
            if (m_registry == null)
                Debug.LogError("[FishingGroundHarness] ContentRegistry 없음 — Tools > Make Assets > Content Registry 실행 필요");

            m_state = new PlayerState();
            if (m_registry != null) m_state.Load(m_registry);

            // 근접 음성 — Network 오브젝트(SessionLauncher와 같은 GO)에 배선됨. HuntZone과 동일 패턴.
            m_voice = m_launcher.GetComponent<VoiceController>();
            m_mute  = m_launcher.GetComponent<PlayerMute>();

            // 월드스페이스 인벤토리 UI — 잡은 물고기가 아이콘으로 보인다. Resources 로드라 씬 배선 불필요.
            var panelPrefab = Resources.Load<InventoryPanel>("InventoryPanel");
            if (panelPrefab != null)
            {
                m_inventoryPanel = Instantiate(panelPrefab);
                m_inventoryPanel.transform.SetPositionAndRotation(new Vector3(1.6f, 1.4f, 1.2f), Quaternion.Euler(0f, 210f, 0f));
                m_inventoryPanel.Bind(m_state.Inventory);
            }

            // 획득 결과 패널 — 초기 표시는 소유자(하네스)가 관리 (rules/scripts.md)
            var resultPrefab = Resources.Load<FishResultPanel>("FishResultPanel");
            if (resultPrefab != null)
            {
                m_resultPanel = Instantiate(resultPrefab);
                m_resultPanel.transform.SetPositionAndRotation(new Vector3(-1.4f, 1.4f, 1.2f), Quaternion.Euler(0f, 150f, 0f));
                m_resultPanel.Hide();
                m_resultPanel.Confirmed -= OnResultConfirmed;
                m_resultPanel.Confirmed += OnResultConfirmed;
            }

            // 세션·음소거 소셜 패널 (P0 판정: 음소거 토글) — Resources 로드라 씬 배선 불필요
            var socialPrefab = Resources.Load<SocialPanel>("SocialPanel");
            if (socialPrefab != null)
            {
                var social = Instantiate(socialPrefab);
                social.transform.position = new Vector3(-1.8f, 1.4f, -0.6f); // 스폰 왼편 (빌보드라 회전 불필요)
                social.Bind(m_launcher, m_voice, m_mute);
            }

            // 손목 HUD — 코인 + 낚시 소모품(미끼·내구도). 리그(DontDestroyOnLoad)에 붙으므로 파괴는 하네스 책임
            m_wristHud = WristHud.Spawn(m_state.Wallet,
                () => $"미끼 {m_rod.BaitCount} · 내구도 {m_rod.Rod.durability}/{m_rod.Rod.maxDurability}");

            m_toast = ToastHud.Spawn(); // 구매/수리 결과 알림

            // 낚시 상점 패널 — 미끼 구매·수리 (IMGUI 버튼의 VR 대체)
            var listPrefab = Resources.Load<ActionListPanel>("ActionListPanel");
            if (listPrefab != null)
            {
                m_shopPanel = Instantiate(listPrefab);
                m_shopPanel.transform.position = new Vector3(2.4f, 1.5f, -2.4f); // 부두 입구 랜턴 옆
                m_shopPanel.SetTitle("낚시 상점");
                RefreshShopPanel();
            }

            // VR 입력 어댑터 — 씬에 없으면 낚싯대에 부착 (Awake에서 rod/spot 자동 해석)
            if (FindFirstObjectByType<FishingRodInput>() == null)
                m_rod.gameObject.AddComponent<FishingRodInput>();

            // VR 원거리 그랩 — 손 그랩 라인으로 낚싯대를 집는다. 반드시 FishingRodInput 뒤에 부착
            // (RodGrabber.Start가 입력 어댑터를 찾아 "든 동안에만 제스처" 게이트를 건다)
            if (FindFirstObjectByType<RodGrabber>() == null)
                m_rod.gameObject.AddComponent<RodGrabber>();

            m_rod.FishCaught -= OnFishCaught;
            m_rod.FishCaught += OnFishCaught;
        }

        private void OnDestroy()
        {
            m_rod.FishCaught -= OnFishCaught;
            if (m_resultPanel != null) m_resultPanel.Confirmed -= OnResultConfirmed;
            if (m_wristHud != null) Destroy(m_wristHud.gameObject); // 리그에 붙어 있어 씬 언로드로 안 죽는다
            if (m_toast != null) Destroy(m_toast.gameObject);
        }

        // IMGUI 로그와 VR 토스트 동시 알림 — 결과 피드백은 항상 이 헬퍼를 거친다
        private void Notify(string message)
        {
            m_lastLog = message;
            if (m_toast != null) m_toast.Show(message);
        }

        private void OnResultConfirmed() => m_resultPanel.Hide();

        private void OnApplicationQuit()
        {
            m_state.Save(); // 낚시터는 EstateManager가 없으므로 배치 목록은 디스크 값 그대로 보존됨
        }

        // 획득 적용 — 인벤토리(fishId→기존 FishDef)/코인/XP. 낚시 코어는 이벤트만 발화, 적용은 하네스 책임.
        private void OnFishCaught(FishInstance fish, FishReward reward)
        {
            string displayName = fish.Species.fishId;
            if (m_registry != null && m_registry.TryGetItem(fish.Species.fishId, out ItemDef def))
            {
                m_state.Inventory.Add(def); // 기존 FishDef 재사용 — 아이콘·판매가 그대로
                displayName = def.DisplayName;
            }

            m_state.Wallet.Add(reward.coin);
            m_xp += reward.xp; // TODO(DATA): XP 시스템 미정 — 로컬 누적

            m_lastLog = $"낚음: {displayName} ({fish.Length:F1}cm)";
            if (m_resultPanel != null) m_resultPanel.Show(fish, reward, displayName);
            m_state.Save();
            RefreshShopPanel(); // 포획으로 내구도가 깎였을 수 있음 — 수리 가격 갱신
        }

        private void ReturnToLobby()
        {
            m_state.Save();
            SceneManager.LoadScene(m_lobbySceneName);
        }

        // 낚시 상점 VR 패널 행 재구성 — 구매/수리/포획 후 호출 (미끼는 캐스팅 소모라 손목 HUD가 실시간 담당)
        private void RefreshShopPanel()
        {
            if (m_shopPanel == null) return;
            int repairPrice = FishingFormulas.RepairPrice(m_rod.Rod.durability);
            m_shopPanel.SetRows(new List<ActionListPanel.RowSpec>
            {
                new ActionListPanel.RowSpec
                {
                    label = $"미끼 {m_rod.BaitCount}개 보유",
                    button1 = $"5개 {FishingFormulas.BaitPrice() * 5}c",
                    onButton1 = () => { BuyBait(5); RefreshShopPanel(); },
                },
                new ActionListPanel.RowSpec
                {
                    label = $"내구도 {m_rod.Rod.durability}/{m_rod.Rod.maxDurability}",
                    button1 = m_rod.Rod.durability < m_rod.Rod.maxDurability ? $"수리 {repairPrice}c" : null,
                    onButton1 = () => { RepairRod(); RefreshShopPanel(); },
                },
            });
        }

        // 미끼 구매 — 코인 싱크 배선 (economy.md 일반 싱크, 가격은 §9 FishingFormulas 경유)
        private void BuyBait(int quantity)
        {
            int price = FishingFormulas.BaitPrice() * quantity;
            if (!m_state.Wallet.TrySpend(price))
            {
                Notify($"미끼 구매 실패 — 코인 부족 ({price}c 필요)");
                return;
            }
            m_rod.AddBait(quantity);
            Notify($"미끼 {quantity}개 구매 (-{price}c)");
            m_state.Save();
        }

        // 낚싯대 수리 — 코인 싱크 배선 (§5-2 수리대, 가격은 §9 FishingFormulas 경유)
        private void RepairRod()
        {
            int price = FishingFormulas.RepairPrice(m_rod.Rod.durability);
            if (!m_state.Wallet.TrySpend(price))
            {
                Notify($"수리 실패 — 코인 부족 ({price}c 필요)");
                return;
            }
            m_rod.Repair();
            Notify($"낚싯대 수리 완료 (-{price}c)");
            m_state.Save();
        }

        // 정교화 낚시 디버그 조작 (개발용 IMGUI — VR 입력은 FishingRodInput 담당, Quest 빌드 전 제거 대상)
        private void DrawFishing()
        {
            GUILayout.Label($"── 낚시 ── 미끼 {m_rod.BaitCount} · 내구도 {m_rod.Rod.durability}/{m_rod.Rod.maxDurability} · 코인 {m_state.Wallet.Coins} · XP {m_xp}");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button($"미끼 구매 x5 ({FishingFormulas.BaitPrice() * 5}c)"))
                BuyBait(5);
            if (m_rod.Rod.durability < m_rod.Rod.maxDurability &&
                GUILayout.Button($"수리 ({FishingFormulas.RepairPrice(m_rod.Rod.durability)}c)"))
                RepairRod();
            GUILayout.EndHorizontal();

            if (m_resultPanel != null && m_resultPanel.gameObject.activeSelf && GUILayout.Button("결과 닫기"))
                m_resultPanel.Hide();

            Fish fish = m_rod.CurrentFish;
            if (fish == null)
            {
                if (GUILayout.Button("캐스팅 (최근접 실루엣)"))
                {
                    if (m_spot.TryGetNearestFish(m_rod.transform.position, m_rod.Rod.length, out Fish target))
                        m_rod.Cast(target);
                    else
                        m_lastLog = "사거리 안에 물고기 없음";
                }
                return;
            }

            GUILayout.Label($"상태: {fish.State} · 줄: {fish.Line} · HP {fish.Health:F1}/{fish.Instance.MaxHealth:F1} · 텐션 {fish.Tension:F1}");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("챔질")) m_rod.Chamjil();
            if (GUILayout.Button(m_rod.Reeling ? "릴링 중지" : "릴링 (홀드)")) m_rod.SetReeling(!m_rod.Reeling);
            if (GUILayout.Button("스윙")) fish.OnSwing();
            if (GUILayout.Button("낚아올림")) fish.Hook();
            GUILayout.EndHorizontal();
        }

        private async Task JoinAsync()
        {
            m_joining = true;
            try
            {
                await m_launcher.StartSession($"fishing_{m_shardId}", destroyCancellationToken);
                m_lastLog = "낚시터 접속 완료";
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
            GUILayout.Label($"[낚시터] {m_lastLog}");
            if (GUILayout.Button("로비로 복귀")) ReturnToLobby();
            GUILayout.Space(8);

            var runner = m_launcher.Runner;
            if (runner == null)
            {
                GUI.enabled = !m_joining;
                if (GUILayout.Button(m_joining ? "접속 중..." : "낚시터 접속 (드롭인)"))
                    _ = JoinAsync();
                GUI.enabled = true;
            }
            else
            {
                GUILayout.Label($"접속: {runner.SessionInfo.Name} ({runner.SessionInfo.PlayerCount}명) — 고정 샤드, 실제 매칭/샤딩 TBD");

                if (m_voice != null && GUILayout.Button($"마이크 {(m_voice.MicEnabled ? "끄기" : "켜기")}"))
                    m_voice.SetMicEnabled(!m_voice.MicEnabled);
                if (m_mute != null)
                {
                    foreach (PlayerRef player in runner.ActivePlayers)
                    {
                        if (player == runner.LocalPlayer) continue;
                        bool muted = m_mute.IsMuted(player);
                        if (GUILayout.Button($"P{player.PlayerId} 음소거 {(muted ? "해제" : "")}"))
                            m_mute.SetMuted(player, !muted);
                    }
                }
            }

            GUILayout.Space(8);
            DrawFishing();

            GUILayout.Space(8);
            GUILayout.Label("── 인벤토리 ──");
            foreach (KeyValuePair<ItemDef, int> entry in new List<KeyValuePair<ItemDef, int>>(m_state.Inventory.Items))
                GUILayout.Label($"{entry.Key.DisplayName} x{entry.Value}");
        }
    }
}
