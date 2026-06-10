namespace Ginei
{
    /// <summary>
    /// キャンペーン暦（LIFE-1 #151・唯一の時間窓口）。<see cref="currentYear"/> を進めて人物の加齢・寿命判定の
    /// トリガにする。年齢は人物が直接持たず <c>currentYear - birthYear</c> で導出する（暦とズレない）。
    /// シナリオ会戦モードは暦を進めない＝加齢しない静的メタdata（後方互換）。純データ（非 MonoBehaviour・test-first）。
    /// </summary>
    public class Calendar
    {
        /// <summary>現在の年（宇宙暦等）。</summary>
        public int currentYear;

        /// <summary>1ターンあたりの年数（既定1年）。死亡率も per-turn 換算でこれを掛ける。</summary>
        public int yearsPerTurn = 1;

        public Calendar() { }

        public Calendar(int startYear, int yearsPerTurn = 1)
        {
            this.currentYear = startYear;
            this.yearsPerTurn = yearsPerTurn < 1 ? 1 : yearsPerTurn;
        }

        /// <summary>1ターン進める（<see cref="yearsPerTurn"/> 年）。</summary>
        public void Advance() => currentYear += yearsPerTurn;
    }
}
