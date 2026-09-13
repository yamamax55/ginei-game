using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using TMPro;

namespace Ginei
{
    /// <summary>
    /// UI で使っている記号が日本語フォントに収録されているかを実際に調べる（#記号欠落）。
    /// 「豆腐（□）になる字」を<b>推測ではなく実データで</b>特定するための道具＝警告を消して隠すのではなく、
    /// どの字が入っていないかを名指しできるようにする。
    ///
    /// 収録されていない字が見つかったら対処は2択：①その字を使うのをやめて日本語や数字に置き換える
    /// （盤面の艦隊/星系まわりは既にこの方針）②フォントアセットを動的（Dynamic）にして
    /// 元フォントから字を足す。どちらを採るかは見え方の判断が要るので、ここでは検出だけを行う。
    /// </summary>
    public static class FontCoverageChecker
    {
        /// <summary>UI に出てくる記号の候補（見つけ次第ここへ足す）。</summary>
        private const string Symbols = "★☆◆◇■□●○▲△▼▽×✕✓✔≡＝＋－±→←↑↓►◀━─│┃※⛨⚑⚔☗♦♣▾▴‥…「」『』〜／＼";

        [MenuItem("Ginei/QA: フォント未収録の記号を検出", false, 310)]
        public static void Check()
        {
            var font = Resources.Load<TMP_FontAsset>("JapaneseFont_TMP");
            if (font == null)
            {
                EditorUtility.DisplayDialog("記号の収録チェック",
                    "Resources/JapaneseFont_TMP が見つかりません。", "OK");
                return;
            }

            // 判定は「実際に出せるか」で行う。動的（Dynamic）フォントは未追加でも元フォントに字があれば
            // 実行時に足されるので、単純な HasCharacter(c) では**まだ追加していないだけ**の字を
            // 未収録と誤判定する（実機QA：33件はほぼこれ）。フォールバック連鎖と動的追加まで見る版を使う。
            bool dynamic = font.atlasPopulationMode == AtlasPopulationMode.Dynamic;

            var missing = new List<char>();
            var present = new List<char>();
            for (int i = 0; i < Symbols.Length; i++)
            {
                char c = Symbols[i];
                // searchFallbacks:true＝フォールバックまで辿る／tryAddCharacter:dynamic＝動的なら追加を試す。
                bool ok = font.HasCharacter(c, searchFallbacks: true, tryAddCharacter: dynamic);
                if (ok) present.Add(c); else missing.Add(c);
            }

            var sb = new StringBuilder();
            sb.AppendLine($"フォント: {font.name}（AtlasPopulationMode = {font.atlasPopulationMode}）");
            sb.AppendLine(dynamic
                ? "動的アトラス＝未追加の字も実行時に足されるため、追加可否まで含めて判定しています。"
                : "静的アトラス＝アトラスに無い字は出せません（フォールバックも含めて判定しています）。");
            sb.AppendLine($"出せる {present.Count} 件 / 出せない {missing.Count} 件");
            if (missing.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("収録なし（豆腐になる字）:");
                for (int i = 0; i < missing.Count; i++)
                    sb.AppendLine($"  {missing[i]}  U+{(int)missing[i]:X4}");
                sb.AppendLine();
                sb.AppendLine("対処：この字を使う表示を日本語/数字へ置き換えるか、フォントアセットを Dynamic にして補う。");
            }

            if (missing.Count == 0)
            {
                Debug.Log($"[FontCoverage] {sb}");
                EditorUtility.DisplayDialog("記号の収録チェック", "調べた記号はすべて収録されています。", "OK");
            }
            else
            {
                Debug.LogWarning($"[FontCoverage] {sb}");
                EditorUtility.DisplayDialog("記号の収録チェック",
                    $"収録なし {missing.Count} 件。詳細は Console を確認してください。", "OK");
            }
        }
    }
}
