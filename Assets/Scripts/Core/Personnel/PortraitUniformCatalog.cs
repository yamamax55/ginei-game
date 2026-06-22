using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 勢力ごとの「制服クローズ」（肖像プロンプトに差し込む英語句）の単一窓口（③アート一貫性）。
    /// 同一勢力の全人物で同じ制服文を使うことで、勢力内の制服を揃える。
    /// 多勢力名（<see cref="AdmiralData.factionName"/>）で引く。未登録/空なら null を返し、
    /// 呼び出し側（<see cref="PortraitPromptRules"/>）が enum 既定（帝国/同盟）へフォールバックする＝後方互換。
    /// 純ロジック・test-first・読むだけ（基準値非破壊）。詳細運用は docs/ops/portrait-pipeline.md。
    /// </summary>
    public static class PortraitUniformCatalog
    {
        // 勢力名 → 制服クローズ（判別キー＝色／徽章。黎明=白×紺×翼徽章／碧晶=白×翡翠緑×金刺繍／
        // ファロス=白×紺だが金の袖線で区別／自由港=白×赤／鉄環=灰緑×赤×鉄十字）。
        private static readonly Dictionary<string, string> Uniforms = new Dictionary<string, string>
        {
            ["黎明評議会"] =
                "the Dawn Council republican dress uniform: a clean white high-collar double-breasted tunic " +
                "with a navy-blue standing collar and front placket, gold trim lines, gold shoulder epaulettes, " +
                "and a small golden winged emblem at the collar",
            ["碧晶公国"] =
                "the Jade Principality aristocratic dress uniform: an ornate white high-collar tunic richly " +
                "embroidered with gold braid and aiguillettes, jade-green facings and sash, gold epaulettes, " +
                "regal and formal",
            ["自由港同位体"] =
                "the Free Port guild outfit: a practical off-white jacket with bold red and navy-blue accent panels, " +
                "functional straps, and a small golden free-port guild crest, informal merchant-privateer style",
            ["灯心自由市ファロス"] =
                "the Pharos Free City naval dress uniform: a white coat with navy-blue lapels, gold sleeve rank " +
                "stripes and gold buttons, and a small golden lighthouse crest, calm civic-navy style",
            ["鉄環兵団"] =
                "the Iron Ring Corps field military uniform: an austere dark field-grey (grey-green) military tunic " +
                "with blood-red collar tabs and shoulder boards, and an iron-cross insignia, militaristic",
        };

        /// <summary>勢力名に対応する制服クローズ。未登録/空なら null。</summary>
        public static string For(string factionName)
        {
            if (string.IsNullOrEmpty(factionName)) return null;
            return Uniforms.TryGetValue(factionName, out var s) ? s : null;
        }

        /// <summary>登録があるか。</summary>
        public static bool Has(string factionName) => For(factionName) != null;
    }
}
