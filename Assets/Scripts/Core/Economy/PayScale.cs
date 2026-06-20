namespace Ginei
{
    /// <summary>
    /// 俸給表（#1969 WAGE・純データ）。勢力の給与政策＝基本俸・階級ステップ・能力加給率。人物（提督/文官 #14）の俸給を
    /// 階級と能力から算定する基準（<see cref="WageRules.PersonSalary"/>）。勢力ごとに違える（格差の大きい/平等寄り等）。
    /// 実効値パターン（基準非破壊＝俸給は計算で出し、人物データを書き換えない）。
    /// </summary>
    [System.Serializable]
    public class PayScale
    {
        public string name = "俸給表";
        public Faction faction;

        /// <summary>基本俸（階級tier1の俸給＝俸給カーブの起点）。</summary>
        public float baseSalary = WageRules.DefaultBaseSalary;

        /// <summary>階級ステップ（1階級上がるごとに基本俸の何倍を上乗せするか＝俸給の傾き）。</summary>
        public float tierStep = WageRules.DefaultTierStep;

        /// <summary>能力加給率（能力が基準=50からどれだけ離れたかで俸給を増減する割合）。</summary>
        public float abilityBonusRatio = WageRules.DefaultAbilityBonus;

        public PayScale() { }

        public PayScale(float baseSalary, float tierStep, float abilityBonusRatio, Faction faction = default)
        {
            this.baseSalary = baseSalary;
            this.tierStep = tierStep;
            this.abilityBonusRatio = abilityBonusRatio;
            this.faction = faction;
        }

        /// <summary>既定の俸給表。</summary>
        public static PayScale Default => new PayScale();
    }
}
