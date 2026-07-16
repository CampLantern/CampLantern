using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CampLantern.UI
{
    /// <summary>
    /// 런타임 UI 스킨 로더 — Resources/UIKit(300Mind 아틀라스)의 서브 스프라이트를 코드 생성 HUD에 입힌다.
    /// 스프라이트가 없으면 조용히 실패(호출부의 단색 폴백 유지) — 스킨 미임포트 상태에서도 동작.
    /// 프리팹형 UI(패널·버튼)는 에디터 팩토리(VRUISkin)가 굽고, 여기는 코드 생성 HUD 전용.
    /// </summary>
    public static class UISkin
    {
        private const string k_stripName = "card_bg";      // 흰 라운드 카드 — 호출부의 어두운 틴트로 다크 HUD가 된다
        private const string k_pillName  = "purpleButton"; // 보라 필 — 토스트 알림용
        private const string k_coinName  = "coin_img";     // 금화 아이콘

        private static Dictionary<string, Sprite> s_cache;

        public static Sprite Rounded => Load(k_stripName);
        public static Sprite Coin    => Load(k_coinName);

        private static Sprite Load(string name)
        {
            if (s_cache == null)
            {
                s_cache = new Dictionary<string, Sprite>();
                foreach (var sprite in Resources.LoadAll<Sprite>("UIKit"))
                    s_cache[sprite.name] = sprite;
            }
            return s_cache.TryGetValue(name, out var found) ? found : null;
        }

        /// <summary>배경 Image에 라운드 카드 스킨 적용 — 호출부가 설정한 틴트(어두운 HUD 톤)를 유지한다.</summary>
        public static bool TryApplyRounded(Image img)
        {
            var sprite = Rounded;
            if (sprite == null) return false;
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            return true;
        }

        /// <summary>토스트용 보라 필 적용 — 성공 시 필 원색 노출(흰 틴트). 없으면 false(호출부 단색 유지).</summary>
        public static bool TryApplyPill(Image img)
        {
            var sprite = Load(k_pillName);
            if (sprite == null) return false;
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.color = Color.white;
            return true;
        }
    }
}
