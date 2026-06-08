using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.TextCore.Text;

namespace Ginei
{
    /// <summary>
    /// UI Toolkit（段階移行）の共通基盤。実行時に UIDocument＋PanelSettings を配線し、
    /// 共通テーマ（Resources/GineiTheme.uss）と日本語フォントを適用したルート VisualElement を返す。
    /// ★ランタイムUITKには PanelSettings が必須。Resources に "GineiPanelSettings" を1つ用意する
    ///   （Project で右クリック → Create → UI Toolkit → Panel Settings Asset、Assets/Resources/ に置き名前を
    ///   GineiPanelSettings にする。テーマは既定のままで可）。未用意なら警告ログを出す。
    /// </summary>
    public static class GineiUITK
    {
        /// <summary>
        /// host に UIDocument を付け、テーマ/フォントを適用したルートを返す。失敗時 root は null。
        /// </summary>
        public static UIDocument Attach(GameObject host, int sortingOrder, out VisualElement root)
        {
            root = null;
            var doc = host.AddComponent<UIDocument>();

            var ps = Resources.Load<PanelSettings>("GineiPanelSettings");
            if (ps == null)
            {
                Debug.LogWarning("GineiUITK: Resources/GineiPanelSettings (PanelSettings) が見つかりません。" +
                    "Project で Create → UI Toolkit → Panel Settings Asset を作り Assets/Resources/GineiPanelSettings に置いてください。");
                return doc;
            }
            doc.panelSettings = ps;
            doc.sortingOrder = sortingOrder;

            root = doc.rootVisualElement;
            if (root == null) return doc;

            root.style.flexGrow = 1f;

            var uss = Resources.Load<StyleSheet>("GineiTheme");
            if (uss != null) root.styleSheets.Add(uss);

            ApplyJapaneseFont(root);
            return doc;
        }

        /// <summary>ルート以下に日本語フォントを適用（継承で子へ波及）。</summary>
        public static void ApplyJapaneseFont(VisualElement root)
        {
            if (root == null) return;
            // TMP 用 FontAsset が TextCore FontAsset として使えればそれを優先（CJK アトラス）。
            FontAsset fa = Resources.Load<FontAsset>("JapaneseFont_TMP");
            if (fa != null) { root.style.unityFontDefinition = new StyleFontDefinition(fa); return; }

            // フォールバック：legacy Font（エディタでは msgothic を解決）。
            Font f = FontProvider.JapaneseFont;
            if (f != null) root.style.unityFontDefinition = new StyleFontDefinition(f);
        }
    }
}
