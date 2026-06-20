namespace Ginei
{
    /// <summary>
    /// 保険会社（#1982 INS・純データ）。自己資本（巨大損失のクッション＝ソルベンシー）・収入保険料・支払保険金・経費・
    /// 運用フロート（保険料を集めてから払うまでの預かり金）を持つ。引受損益と投資収益の二本柱で稼ぐ（解決は
    /// <see cref="InsuranceRules"/>）。ソルベンシーは銀行 BIS（#1976）・証券 net capital（#1963）と同型。少数集約（タイクン化回避）。
    /// </summary>
    [System.Serializable]
    public class Insurer
    {
        public string name = "保険会社";
        public Faction faction;

        /// <summary>自己資本（想定外の巨大損失を吸収するソルベンシーの源）。</summary>
        public float capital = 100f;

        /// <summary>収入保険料（引き受けた契約から受け取った保険料の合計）。</summary>
        public float premiumsWritten = 0f;

        /// <summary>支払保険金（事故で支払った保険金の合計）。</summary>
        public float claimsPaid = 0f;

        /// <summary>経費（事業費＝引受・査定の運営費）。</summary>
        public float expenses = 0f;

        public Insurer() { }

        public Insurer(string name, float capital, float premiumsWritten = 0f, float claimsPaid = 0f,
            float expenses = 0f, Faction faction = default)
        {
            this.name = string.IsNullOrEmpty(name) ? "保険会社" : name;
            this.capital = capital;
            this.premiumsWritten = premiumsWritten;
            this.claimsPaid = claimsPaid;
            this.expenses = expenses;
            this.faction = faction;
        }
    }
}
