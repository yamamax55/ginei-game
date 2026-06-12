namespace Ginei
{
    /// <summary>
    /// ネームド人物の職分（人物システムの「職業」・POP の <see cref="Occupation"/> とは<b>別管理</b>）。
    /// POP の労働力職業（日本標準職業分類#110）は<b>大衆の就労区分</b>だが、ネームド人物は <see cref="君主"/>（王・皇帝・元首）のように
    /// 標準職業分類に載らない地位を持つため、別系統で扱う。解決は <see cref="PersonVocationRules"/> が唯一の窓口。純データ（enum）。
    /// </summary>
    /// <remarks>
    /// 君主は POP 職業分類の apex ではなく <b>別格</b>（継承#152/易姓革命で替わる地位）＝POP からの昇格（<see cref="PersonVocationRules.PromotionVocation"/>）では到達しない。
    /// 政治家/文官/武官/技術者は POP の対応プール（官吏=事務／軍属=保安 等）から昇格しうる。
    /// </remarks>
    public enum PersonVocation
    {
        君主,     // 王・皇帝・国家元首（POP分類外の別格・継承#152/革命で替わる）
        政治家,   // 議員・党人（GOV-6 #159・政治任用）
        文官,     // 官僚・行政（文民の主流）
        武官,     // 将官・提督・軍人
        技術者,   // テクノクラート・研究者（LIFE-7）
        その他    // 在野・上記に当てはまらない人物
    }
}
