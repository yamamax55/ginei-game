namespace Ginei
{
    /// <summary>
    /// 勲章の種別（#2263・史実参考）。勲功章＝顕著な功績(order of merit)、武功章＝武勇(金鵄勲章型)、
    /// 戦功章＝会戦の戦功、従軍章＝従軍記章(広く授与・名誉低)。タイクン化回避＝少数種別。
    /// </summary>
    public enum MedalKind { 勲功章, 武功章, 戦功章, 従軍章 }

    /// <summary>勲章の等級（一級が最高〜五級が最低・金鵄勲章の功級型）。</summary>
    public enum MedalGrade { 一級, 二級, 三級, 四級, 五級 }

    /// <summary>
    /// 叙勲された1つの勲章（#2263）。種別×等級＋叙勲年＋功績文。人物が保有する（<see cref="MedalRegistry"/>）。
    /// 効果（恩給倍率・名誉点）は <see cref="MedalRules"/> が算定。
    /// </summary>
    public readonly struct Decoration
    {
        public readonly MedalKind kind;
        public readonly MedalGrade grade;
        public readonly int awardedYear;
        public readonly string citation;

        public Decoration(MedalKind kind, MedalGrade grade, int awardedYear = 0, string citation = "")
        {
            this.kind = kind; this.grade = grade; this.awardedYear = awardedYear; this.citation = citation ?? "";
        }
    }
}
