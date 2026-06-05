using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 勢力(FactionData)アセットをワンクリック生成するエディタ拡張。
    /// メニュー「Ginei/Create Faction Data」から実行する。
    ///
    /// 世界観バイブル(docs/worldbuilding-bible.md §2)に沿って、三大勢力
    /// （王党派＝帝国／民主派＝同盟／共産主義）＋軍閥2勢力（自治領・軍閥）を、
    /// 色・思想・階級(ranks)・敵対関係(nonHostileFactions)つきで定義する（Issue #16）。
    ///
    /// アセットは `Assets/Resources/Factions/` に作成（既存は内容を更新、参照GUIDは維持）。
    /// 敵対関係の既定：三大勢力は相互に敵対（nonHostileFactions 空）。軍閥2勢力は連合＝相互に非敵対。
    /// 同盟・恒久ブロックを変えたい場合は各アセットの nonHostileFactions を編集する。
    /// </summary>
    public static class FactionDataCreator
    {
        private const string FactionDir = "Assets/Resources/Factions";

        [MenuItem("Ginei/Create Faction Data")]
        public static void CreateFactions()
        {
            EnsureFolder(FactionDir);

            // 階級は RankSystem の共通 tier（大きいほど上位）に揃える。欠番は ResolveTier が直近 tier に丸める。
            // 帝国のみ tier9「上級大将」を持つ。共産は党称号、軍閥は簡易ラダー。
            FactionData empire = GetOrCreate("Empire", "帝国", new Color(0.9f, 0.2f, 0.2f),
                "王党派（専制・帝政）", Faction.帝国,
                Ranks((5, "准将"), (6, "少将"), (7, "中将"), (8, "大将"), (9, "上級大将"), (10, "元帥")));

            FactionData alliance = GetOrCreate("Alliance", "同盟", new Color(0.2f, 0.5f, 0.9f),
                "民主派（共和制）", Faction.同盟,
                Ranks((5, "准将"), (6, "少将"), (7, "中将"), (8, "大将"), (10, "元帥")));

            FactionData communist = GetOrCreate("Communist", "共産主義", new Color(0.9f, 0.4f, 0.1f),
                "党による計画・集団主義（穏健派／強硬派）", Faction.帝国,
                Ranks((5, "准司令官"), (6, "司令官"), (7, "上級司令官"), (8, "軍司令官"), (10, "総司令官")));

            FactionData autonomous = GetOrCreate("Autonomous", "自治領", new Color(0.3f, 0.8f, 0.4f),
                "軍閥（自治領）", Faction.帝国,
                Ranks((6, "隊長"), (8, "将軍"), (10, "総帥")));

            FactionData warlord = GetOrCreate("Warlord", "軍閥", new Color(0.45f, 0.75f, 0.35f),
                "軍閥（地方領袖）", Faction.同盟,
                Ranks((6, "隊長"), (8, "将軍"), (10, "総帥")));

            // 敵対関係（Issue #16「関係を設定」）。
            // 三大勢力は全敵対（四つ巴）。軍閥2勢力は連合＝相互に非敵対だが三大勢力とは敵対。
            SetNonHostile(empire);                 // 全敵対
            SetNonHostile(alliance);               // 全敵対
            SetNonHostile(communist);              // 全敵対
            SetNonHostile(autonomous, warlord);    // 軍閥連合
            SetNonHostile(warlord, autonomous);    // 軍閥連合

            FactionData[] all = { empire, alliance, communist, autonomous, warlord };
            foreach (var f in all) EditorUtility.SetDirty(f);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Ginei: FactionData 5勢力（帝国/同盟/共産主義/自治領/軍閥）を Assets/Resources/Factions に生成。" +
                      "三大勢力は相互敵対、軍閥2勢力は連合（相互非敵対）。" +
                      "ScenarioData のエントリや FleetStrength.factionData に割り当てて使います。");
        }

        /// <summary>勢力アセットを生成/更新し、scalar・階級を設定する（nonHostileFactions は後段で設定）。</summary>
        private static FactionData GetOrCreate(string fileName, string factionName, Color color, string ideology,
            Faction legacy, List<FactionData.RankEntry> ranks)
        {
            string path = $"{FactionDir}/{fileName}.asset";
            FactionData existing = AssetDatabase.LoadAssetAtPath<FactionData>(path);
            FactionData f = existing != null ? existing : ScriptableObject.CreateInstance<FactionData>();

            f.factionName = factionName;
            f.color = color;
            f.ideology = ideology;
            f.legacyFaction = legacy;
            f.ranks = ranks;

            if (existing == null) AssetDatabase.CreateAsset(f, path);
            return f;
        }

        /// <summary>nonHostileFactions を上書き設定（引数なし＝全敵対）。null/自己参照は除外。</summary>
        private static void SetNonHostile(FactionData f, params FactionData[] allies)
        {
            f.nonHostileFactions = new List<FactionData>();
            if (allies == null) return;
            foreach (var a in allies)
            {
                if (a != null && a != f) f.nonHostileFactions.Add(a);
            }
        }

        /// <summary>(tier, 名称) の並びから階級表を作る。</summary>
        private static List<FactionData.RankEntry> Ranks(params (int tier, string name)[] entries)
        {
            var list = new List<FactionData.RankEntry>();
            if (entries != null)
            {
                foreach (var e in entries) list.Add(new FactionData.RankEntry(e.tier, e.name));
            }
            return list;
        }

        /// <summary>"Assets/A/B" を順に作成（既存はスキップ）。</summary>
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string[] parts = path.Split('/');
            string current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }
    }
}
