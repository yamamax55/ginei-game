namespace Ginei
{
    /// <summary>リース種別（#1989 LEAS）。ファイナンス＝実質割賦購入（残価ほぼ0・借り手が残価リスク）／オペレーティング＝短期返却（残価リスクは貸し手）。</summary>
    public enum LeaseType { ファイナンス, オペレーティング }

    /// <summary>
    /// リース契約（#1989 LEAS・純データ）。取得原価・残価（リース終了時の見込み価値）・リース期間・金利・種別。
    /// 借り手は所有せずリース料を払って使う。<b>戦艦もリース可能</b>（資産＝軍艦）。解決は <see cref="LeasingRules"/>。少数集約（タイクン化回避）。
    /// </summary>
    [System.Serializable]
    public class LeaseContract
    {
        public string name = "リース契約";
        public Faction lessee;

        /// <summary>取得原価（リース会社が資産を買った値段）。</summary>
        public float assetCost;

        /// <summary>残価（リース終了時の見込み価値＝大きいほどリース料が安い）。</summary>
        public float residualValue;

        /// <summary>リース期間（支払回数）。</summary>
        public int termPeriods = 1;

        /// <summary>金利（リース料に乗る資金コスト）。</summary>
        public float interestRate = LeasingRules.DefaultInterestRate;

        public LeaseContract() { }

        public LeaseContract(float assetCost, float residualValue, int termPeriods,
            float interestRate = LeasingRules.DefaultInterestRate, Faction lessee = default, string name = null)
        {
            this.assetCost = assetCost;
            this.residualValue = residualValue;
            this.termPeriods = termPeriods;
            this.interestRate = interestRate;
            this.lessee = lessee;
            if (!string.IsNullOrEmpty(name)) this.name = name;
        }
    }
}
