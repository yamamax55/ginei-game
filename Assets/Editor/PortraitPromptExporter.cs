using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 全 <see cref="AdmiralData"/> から肖像生成プロンプト＋決定論シードを CSV に書き出すエディタツール（③アート一貫性）。
    /// 「100人の提督を一貫した画風で生成する」を手作業から作業リスト駆動へ＝<see cref="PortraitPromptRules"/> を一括適用。
    /// メニュー：Ginei/Export Portrait Prompts (CSV)。出力＝プロジェクト直下 portrait-prompts.csv。
    /// 詳細運用は docs/portrait-pipeline.md。
    /// </summary>
    public static class PortraitPromptExporter
    {
        [MenuItem("Ginei/Export Portrait Prompts (CSV)")]
        public static void Export()
        {
            string[] guids = AssetDatabase.FindAssets("t:AdmiralData");
            var sb = new StringBuilder();
            sb.AppendLine("admiralName,fullName,faction,rankTier,seed,hasPortrait,prompt");

            int n = 0, missing = 0;
            foreach (string g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                var a = AssetDatabase.LoadAssetAtPath<AdmiralData>(path);
                if (a == null) continue;

                string prompt = PortraitPromptRules.BuildPrompt(a);
                int seed = PortraitPromptRules.DeriveSeed(a);
                bool hasPortrait = a.portrait != null;
                if (!hasPortrait) missing++;

                sb.Append(Csv(a.admiralName)).Append(',')
                  .Append(Csv(a.FullName)).Append(',')
                  .Append(Csv(a.faction.ToString())).Append(',')
                  .Append(a.rankTier).Append(',')
                  .Append(seed).Append(',')
                  .Append(hasPortrait ? "1" : "0").Append(',')
                  .Append(Csv(prompt)).Append('\n');
                n++;
            }

            string outPath = Path.Combine(Application.dataPath, "../portrait-prompts.csv");
            File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(true));
            Debug.Log($"[PortraitPromptExporter] {n} 件書き出し（肖像未割当 {missing} 件）: {Path.GetFullPath(outPath)}");
            AssetDatabase.Refresh();
        }

        /// <summary>CSV セルのエスケープ（カンマ/引用符/改行を含むときだけ二重引用符で囲む）。</summary>
        private static string Csv(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.IndexOf(',') >= 0 || s.IndexOf('"') >= 0 || s.IndexOf('\n') >= 0)
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }
    }
}
