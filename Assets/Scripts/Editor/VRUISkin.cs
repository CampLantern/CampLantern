#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CampLantern.EditorTools
{
    /// <summary>
    /// VR UI 스킨 (Colorful UI Kit — Assets/Resources/UIKit) — 프리팹 팩토리들이 Image에
    /// 스프라이트를 입힐 때 쓰는 공용 헬퍼. 스프라이트가 없으면 false를 돌려 단색 폴백 유지.
    ///
    /// 디자인 언어(킷 스토어 키 이미지 기준): 흰 카드 패널(card_bg) + 상단 걸침 컬러 헤더 필
    /// (purpleButton) + 통통한 2.4:1 필 버튼(blue/green/pink) + 시안 목록 행(square_bg_2).
    /// 흰 패널 위 본문은 진보라(BodyText/RowText), 컬러 필 위 라벨은 흰색.
    /// **필 스프라이트(210x90)를 2.4:1보다 심하게 늘리면 모양이 죽는다** — 버튼 크기는 비율 유지.
    ///
    /// 파생 패널(Social/Action/Inventory/FishResult)은 전부 VRUIPanel/VRUIButton 베이스에서
    /// 재조립되므로, 스킨 변경 후엔 **Apply UI Skin (Reskin All)** 하나로 베이스→파생→씬(포탈 패널)
    /// 순서 전체를 재생성한다.
    /// </summary>
    public static class VRUISkin
    {
        private const string k_root = "Assets/Resources/UIKit";

        private const string k_panel       = "card_bg";      // 흰 카드 (256x207, 하단 연보라 립)
        private const string k_header      = "purpleButton"; // 헤더 필 (210x90)
        private const string k_buttonMain  = "blueButton";
        private const string k_buttonGreen = "greenButton";
        private const string k_buttonPink  = "pinkButton";
        private const string k_rowBar      = "square_bg_2";  // 시안 라운드 사각 (74x74) — 목록 행
        private const string k_coin        = "coin_img";

        // 9-슬라이스 보더 (px, L/B/R/T) — 코너 반경 실측 기반. 하단은 그림자 립 포함.
        private static readonly Dictionary<string, Vector4> k_borders = new()
        {
            { k_panel,       new Vector4(24f, 36f, 24f, 24f) },
            { k_header,      new Vector4(44f, 42f, 44f, 42f) },
            { k_buttonMain,  new Vector4(44f, 42f, 44f, 42f) },
            { k_buttonGreen, new Vector4(44f, 42f, 44f, 42f) },
            { k_buttonPink,  new Vector4(44f, 42f, 44f, 42f) },
            { "yellowButton", new Vector4(44f, 42f, 44f, 42f) },
            { k_rowBar,      new Vector4(24f, 26f, 24f, 24f) },
        };

        public enum ButtonStyle { Main, Positive, Negative }

        /// <summary>필 색 — UI 종류별 차별화용 (킷 레퍼런스: 리스트=보라, 카드=파랑, 결과=핑크, 확인=초록).</summary>
        public enum PillColor { Blue, Green, Pink, Purple, Yellow }

        private static string PillSprite(PillColor color) => color switch
        {
            PillColor.Blue   => k_buttonMain,
            PillColor.Green  => k_buttonGreen,
            PillColor.Pink   => k_buttonPink,
            PillColor.Yellow => "yellowButton",
            _                => k_header, // Purple
        };

        /// <summary>임의 Image에 지정 색 필 적용 — 헤더·버튼 색 차별화용.</summary>
        public static bool TryApplyPill(Image img, PillColor color) => Apply(img, PillSprite(color));

        /// <summary>필 위 라벨 색 — 밝은 필(파랑·초록·노랑)은 진남색, 진한 필(보라·핑크)은 흰색.</summary>
        public static Color PillText(PillColor color) =>
            color is PillColor.Purple or PillColor.Pink ? Color.white : new Color(0.10f, 0.26f, 0.37f, 1f);

        public static bool HasSkin => LoadSprite(k_panel) != null;

        /// <summary>흰 패널 위 본문 텍스트 — 진보라, 스킨 없으면 흰색.</summary>
        public static Color BodyText => HasSkin ? new Color(0.29f, 0.26f, 0.42f, 1f) : Color.white;

        /// <summary>시안 목록 행 위 텍스트 — 진남색, 스킨 없으면 흰색.</summary>
        public static Color RowText => HasSkin ? new Color(0.09f, 0.27f, 0.39f, 1f) : Color.white;

        private static Sprite LoadSprite(string name) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"{k_root}/{name}.png");

        public static bool TryApplyPanel(Image img)  => Apply(img, k_panel);
        public static bool TryApplyHeader(Image img) => Apply(img, k_header);

        public static bool TryApplyButton(Image img, ButtonStyle style = ButtonStyle.Main)
        {
            string name = style switch
            {
                ButtonStyle.Positive => k_buttonGreen,
                ButtonStyle.Negative => k_buttonPink,
                _                    => k_buttonMain,
            };
            return Apply(img, name);
        }

        /// <summary>목록 행 루트에 시안 라운드 바 배경 추가 — 스킨 없으면 아무것도 안 붙인다.</summary>
        public static void TryAddRowBackground(GameObject rowRoot)
        {
            var sprite = LoadSprite(k_rowBar);
            if (sprite == null) return;

            var bg = rowRoot.AddComponent<Image>();
            bg.sprite = sprite;
            bg.type = Image.Type.Sliced;
            bg.color = Color.white;
            bg.raycastTarget = false;
        }

        private static bool Apply(Image img, string spriteName)
        {
            var sprite = LoadSprite(spriteName);
            if (sprite == null) return false;
            img.sprite = sprite;
            img.type   = Image.Type.Sliced;
            img.color  = Color.white; // 스프라이트 원색 노출 (틴트가 필요하면 호출부가 재설정)
            return true;
        }

        // ── 임포트 세팅 (9-슬라이스 보더) ────────────────────────────
        // Colorful_UI 원본은 spriteBorder가 전부 0 — Sliced로 쓰려면 보더 필수.

        [MenuItem("Tools/Make Assets/UI Skin - Ensure Import Settings")]
        public static void EnsureNineSlice()
        {
            int patched = 0;
            foreach (var (name, border) in k_borders)
            {
                string path = $"{k_root}/{name}.png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) { Debug.LogWarning($"[MakeAssets] 스킨 스프라이트 없음: {path}"); continue; }
                if (importer.spriteBorder == border) continue;

                importer.spriteBorder = border;
                importer.SaveAndReimport();
                patched++;
            }
            Debug.Log($"[MakeAssets] UI 스킨 9-슬라이스 보더 세팅: {patched}개 갱신");
        }

        // ── 일괄 재생성 ──────────────────────────────────────────────

        [MenuItem("Tools/Make Assets/Apply UI Skin (Reskin All)")]
        public static void ReskinAll()
        {
            EnsureNineSlice();
            VRUIFactory.ForceRecreate();          // 베이스 VRUIPanel/VRUIButton (스킨 반영 지점)
            SocialPanelFactory.CreateAll();       // 파생 패널 — 베이스에서 재조립
            ActionPanelFactory.CreateAll();
            InventoryUIFactory.CreateAll();
            FishResultPanelFactory.Create();
            LobbyMapFactory.Build();              // 씬에 구워진 포탈 패널 재생성
            RoomMapsFactory.BuildAll();
            Debug.Log("[MakeAssets] UI 스킨 적용 + 전체 재생성 완료");
        }

        /// <summary>배치모드용: 스킨 재생성 + 스크린샷.</summary>
        public static void ReskinAndCapture()
        {
            ReskinAll();
            EnvBuildRunner.CaptureAll();
            CaptureUiCloseups();
        }

        /// <summary>UI 스킨 검증용 클로즈업 — 로비 포탈 패널 + SocialPanel 프리팹 근접 캡처.</summary>
        [MenuItem("Tools/Make Assets/Capture UI Closeups")]
        public static void CaptureUiCloseups()
        {
            string dir = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Library", "MapShots");
            System.IO.Directory.CreateDirectory(dir);

            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                "Assets/Scenes/Lobby.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);

            // ① 사냥터 관문 패널 정면 (패널은 -Z면이 스폰을 향함)
            var gate = GameObject.Find("Gate_HuntZone_A");
            if (gate != null)
            {
                Vector3 panelPos = gate.transform.position + new Vector3(0f, 1.35f, 0f);
                Vector3 camPos = panelPos - gate.transform.forward * 0.8f;
                MapAuditRunner.CaptureView(System.IO.Path.Combine(dir, "UI_portal_closeup.png"),
                    camPos, Quaternion.LookRotation(gate.transform.forward));
            }

            // ② SocialPanel 프리팹 임시 스폰 (파생 패널 스킨 확인) — 캡처 후 즉시 파괴
            var socialPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/SocialPanel.prefab");
            if (socialPrefab != null)
            {
                var inst = (GameObject)Object.Instantiate(socialPrefab, new Vector3(0f, 51.4f, 0f), Quaternion.identity);
                MapAuditRunner.CaptureView(System.IO.Path.Combine(dir, "UI_social_closeup.png"),
                    new Vector3(0f, 51.4f, -0.8f), Quaternion.identity);
                Object.DestroyImmediate(inst);
            }
            Debug.Log($"[MakeAssets] UI 클로즈업 저장: {dir}");
        }
    }
}
#endif
