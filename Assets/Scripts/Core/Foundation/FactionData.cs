using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 勢力（陣営）の定義データ（ScriptableObject）。将来的に enum Faction を置き換える土台。
    /// 名前・色・思想・敵対関係を持ち、3勢力以上を扱えるようにする。
    ///
    /// 後方互換：FactionData を割り当てない艦は従来どおり enum Faction で動作する
    /// （敵対判定・色は FactionData が無ければ enum にフォールバックする）。
    /// アセットは エディタメニュー `Ginei/Create Faction Data` で生成できる（`Resources/Factions/`）。
    /// </summary>
    [CreateAssetMenu(fileName = "NewFaction", menuName = "Ginei/Faction Data")]
    public class FactionData : ScriptableObject
    {
        [Header("基本情報")]
        [Tooltip("勢力名（表示・結果画面の勝者名に使用）")]
        public string factionName = "勢力名";

        [Tooltip("陣営色（艦体・ビーム・HUD・マーカーハロー等の唯一の出所）")]
        public Color color = Color.white;

        [Tooltip("思想・系統（王党派/民主派/共産/軍閥 等。表示・将来ロジック用）")]
        public string ideology = "";

        [Header("敵対関係")]
        [Tooltip("この勢力と敵対しない（味方・中立）勢力。ここに無い他勢力は既定で敵対する")]
        public List<FactionData> nonHostileFactions = new List<FactionData>();

        [Header("後方互換")]
        [Tooltip("旧 enum Faction との対応（既存UI・セーブ・プレイヤー陣営選択の橋渡し用）")]
        public Faction legacyFaction = Faction.帝国;

        [Header("階級")]
        [Tooltip("この勢力の階級表。tier(序列・大きいほど上位)と階級名。tier は欠番可＝その勢力に無い階級。" +
                 "勢力をまたいだ同列判定は tier の一致で行う（RankSystem 参照）。")]
        public List<RankEntry> ranks = new List<RankEntry>
        {
            new RankEntry(5, "准将"),
            new RankEntry(6, "少将"),
            new RankEntry(7, "中将"),
            new RankEntry(8, "大将"),
            new RankEntry(10, "元帥"),
            // 帝国はここに tier 9「上級大将」を足す（同盟は欠番のまま＝同列に該当無し）
        };

        /// <summary>
        /// other と敵対するか。自分自身・非敵対リスト内の勢力とは敵対しない。
        /// それ以外の異勢力は既定で敵対（従来の「陣営違い＝敵」を踏襲）。
        /// </summary>
        public bool IsHostileTo(FactionData other)
        {
            if (other == null || other == this) return false;
            if (nonHostileFactions != null && nonHostileFactions.Contains(other)) return false;
            return true;
        }

        // ───────────── 階級ヘルパ ─────────────

        /// <summary>指定 tier の階級名を返す（この勢力に無ければ空文字）。</summary>
        public string GetRankName(int tier)
        {
            if (ranks == null) return "";
            for (int i = 0; i < ranks.Count; i++)
            {
                if (ranks[i] != null && ranks[i].tier == tier) return ranks[i].rankName;
            }
            return "";
        }

        /// <summary>階級名から tier を返す（無ければ -1）。</summary>
        public int GetTier(string rankName)
        {
            if (ranks == null || string.IsNullOrEmpty(rankName)) return -1;
            for (int i = 0; i < ranks.Count; i++)
            {
                if (ranks[i] != null && ranks[i].rankName == rankName) return ranks[i].tier;
            }
            return -1;
        }

        /// <summary>この勢力の最高位 tier（階級が無ければ -1）。</summary>
        public int HighestTier()
        {
            int h = int.MinValue;
            if (ranks != null)
            {
                for (int i = 0; i < ranks.Count; i++)
                {
                    if (ranks[i] != null && ranks[i].tier > h) h = ranks[i].tier;
                }
            }
            return h == int.MinValue ? -1 : h;
        }

        /// <summary>tier 昇順に並べた階級表のコピーを返す（表示用）。</summary>
        public List<RankEntry> GetRanksSorted()
        {
            List<RankEntry> sorted = new List<RankEntry>();
            if (ranks != null)
            {
                foreach (var r in ranks) if (r != null) sorted.Add(r);
                sorted.Sort((a, b) => a.tier.CompareTo(b.tier));
            }
            return sorted;
        }

        /// <summary>1つの階級（序列 tier ＋ 名称）。tier が同列判定・比較の基準。</summary>
        [System.Serializable]
        public class RankEntry
        {
            [Tooltip("序列。大きいほど上位。勢力をまたいで同じ tier は同列とみなす")]
            public int tier;

            [Tooltip("この勢力での階級名（例：大将／上級大将／元帥）")]
            public string rankName;

            public RankEntry() { }
            public RankEntry(int tier, string rankName) { this.tier = tier; this.rankName = rankName; }
        }
    }
}
