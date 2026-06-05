using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// legacy TextMesh 用の日本語フォント解決を一本化する（FleetStrength / FleetMorale / DamagePopup が共有）。
    ///
    /// 注意：Unity 6 では組み込みの "Arial.ttf" は廃止され、取得しようとすると例外を投げる
    /// （過去に一瞬で艦隊が消えるバグの原因になった）。代わりに "LegacyRuntime.ttf" を使う。
    /// エディタ上では Assets/Fonts/msgothic.ttc があればそれを優先する。
    /// 解決結果はアプリ寿命でキャッシュする（毎回の AssetDatabase 参照を避ける）。
    /// </summary>
    public static class FontProvider
    {
        private static Font cachedJapaneseFont;

        /// <summary>頭上ラベル／ダメージ表示用の legacy 日本語フォント。</summary>
        public static Font JapaneseFont
        {
            get
            {
                if (cachedJapaneseFont == null) cachedJapaneseFont = ResolveJapaneseFont();
                return cachedJapaneseFont;
            }
        }

        private static Font ResolveJapaneseFont()
        {
            // Unity 6 で "Arial.ttf" は廃止＝例外。必ず "LegacyRuntime.ttf" を使う。
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#if UNITY_EDITOR
            // エディタ上では同梱の日本語フォントを優先（あれば）。
            Font custom = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/msgothic.ttc");
            if (custom != null) font = custom;
#endif
            return font;
        }
    }
}
