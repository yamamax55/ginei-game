namespace Ginei
{
    /// <summary>
    /// 新兵訓練所＝兵（軍属POP）の大量養成施設（RECRUIT・米軍モデル＝Basic Combat Training/AIT を担う教育隊）。
    /// ネームド将校を生む士官学校（<see cref="Academy"/>/<see cref="MilitaryAcademyRules"/> #155）とは別系統＝<b>兵の練度</b>を作る。
    /// 学校データ作法（<see cref="HighSchool"/>/<see cref="VocationalTrainingSchool"/> #2034）に倣う純データ。解決は <see cref="RecruitTrainingRules"/> が唯一の窓口。
    /// </summary>
    [System.Serializable]
    public class RecruitDepot
    {
        public int depotId;
        public Faction faction = Faction.帝国;
        public string name = "新兵訓練所";

        /// <summary>年間の基礎訓練スループット上限（訓練枠＝教育隊のボトルネック）。</summary>
        public int capacity = 200;

        /// <summary>教官（教導隊＝drill instructor）の質 0..1。練度↑・脱落率↓に効く。</summary>
        public float cadreQuality = 0.5f;

        /// <summary>選抜基準 0..1（高いほど厳選＝少数精鋭＝ASVAB カット相当）。受入数↓・練度↑・脱落率↓。</summary>
        public float standards = 0.5f;

        public int foundedYear;

        public RecruitDepot() { }

        public RecruitDepot(int depotId, Faction faction, int capacity, float cadreQuality, float standards)
        {
            this.depotId = depotId;
            this.faction = faction;
            this.capacity = capacity;
            this.cadreQuality = cadreQuality;
            this.standards = standards;
        }
    }
}
