namespace Ginei
{
    /// <summary>
    /// 国全体の労働市場（#1957 LABM・純データ）。労働供給側（生産年齢人口×労働参加率）と自然失業率を持つ。
    /// 労働需要（企業 #1022 の雇用枠）と突き合わせて就業者・失業率を導く（解決は <see cref="LaborRules"/>）。
    /// 勢力ごとに1つのマクロ集約（タイクン化回避＝個人の求職は持たない）。生産年齢人口は星系の
    /// <see cref="OccupationRules.WorkingAge"/>（#110）/ <see cref="Population.working"/>（#153）から集計。
    /// </summary>
    [System.Serializable]
    public class LaborMarket
    {
        public string name = "労働市場";
        public Faction faction;

        /// <summary>生産年齢人口（15〜64歳＝働ける年齢の総数）。</summary>
        public float workingAgePopulation = 0f;

        /// <summary>労働参加率（0..1＝働く意思のある割合。保育整備 <see cref="NurseryRules.LaborParticipationFactor"/> で上昇）。</summary>
        public float participationRate = LaborRules.DefaultParticipationRate;

        /// <summary>自然失業率（摩擦的＋構造的＝好況でも消えない最低限）。</summary>
        public float naturalRate = LaborRules.NaturalUnemploymentRate;

        public LaborMarket() { }

        public LaborMarket(float workingAgePopulation, float participationRate = LaborRules.DefaultParticipationRate,
            float naturalRate = LaborRules.NaturalUnemploymentRate, Faction faction = default)
        {
            this.workingAgePopulation = workingAgePopulation;
            this.participationRate = participationRate;
            this.naturalRate = naturalRate;
            this.faction = faction;
        }
    }
}
