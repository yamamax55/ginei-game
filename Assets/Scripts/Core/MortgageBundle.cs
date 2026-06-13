using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 証券化商品（住宅ローンを束ねた MBS/CDO・#185 サブプライム・純データ）。プライム/サブプライムのローン（<see cref="Loan"/>）を
    /// 混ぜて1つの証券にし、格付け会社が格付けする。<b>サブプライム（ジャンク）が紛れ込んでいても、規制(SOX法)前は AAA と格付けされる</b>
    /// （格付けインフレ）＝サブプライム危機の核。解決は <see cref="SubprimeRules"/>。少数集約（タイクン化回避）。
    /// </summary>
    [System.Serializable]
    public class MortgageBundle
    {
        public string name = "住宅ローン証券";
        public List<Loan> loans = new List<Loan>();

        public MortgageBundle() { }

        public MortgageBundle(string name, IEnumerable<Loan> loans)
        {
            this.name = string.IsNullOrEmpty(name) ? "住宅ローン証券" : name;
            this.loans = new List<Loan>();
            if (loans != null) this.loans.AddRange(loans);
        }
    }
}
