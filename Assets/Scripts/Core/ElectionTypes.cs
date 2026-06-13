namespace Ginei
{
    /// <summary>
    /// 指導者・代表の<b>選び方</b>（政体 #117 が決める・選挙システム基盤）。
    /// 寡頭制は<b>少数による合議</b>、民主政治は<b>選挙</b>、君主制は世襲、独裁は指名。解決は <see cref="ElectoralSystemRules"/> が窓口。
    /// </summary>
    public enum LeaderSelectionMode
    {
        世襲,   // 血統で継ぐ（君主制・継承#152）
        指名,   // 現指導者/前任が後継を指名（指導者独裁）
        合議,   // 少数の寡頭が合議で決める（共産の集団指導/首長制の長老会＝CouncilRules）
        選挙    // 有権者の投票で決める（民主政治＝ElectionRules・複数の層 ElectionTier）
    }

    /// <summary>
    /// 民主政治で存在する選挙の層（選挙システム基盤）。党内（総裁選＝<see cref="LeadershipElectionRules"/>）から
    /// 領域の三層（惑星→星系→勢力）まで。<see cref="ElectoralSystemRules.ActiveElectionTiers"/> が政体ごとに有効な層を返す。
    /// </summary>
    /// <remarks>
    /// 領域三層は下位から上位へ集計しうる（惑星選挙の結果を星系へ、星系を勢力へ＝<see cref="ElectionRules.Aggregate"/>）。
    /// 党内は組織（政党 <see cref="Party"/>）の選挙で領域に属さない＝別系統（既存の <see cref="LeadershipElectionRules"/> を流用）。
    /// </remarks>
    public enum ElectionTier
    {
        党内,   // 政党の党首選（総裁選 GOV-7 #165・LeadershipElectionRules）
        惑星,   // 惑星（Province）の選挙＝最小の選挙区
        星系,   // 星系（StarSystem）の選挙＝惑星の集約
        勢力    // 勢力（Faction）全体の選挙＝国政（元首/議会・星系の集約）
    }
}
