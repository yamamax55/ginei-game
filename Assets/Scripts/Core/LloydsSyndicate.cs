namespace Ginei
{
    /// <summary>
    /// ロイズのシンジケート（引受組合・#1982 INS-5・純データ）。<b>ロイズは保険“会社”ではなく保険“市場”</b>であり、ネーム
    /// （資本提供者）が集まってシンジケートを作り、ブローカーの持ち込むリスクに<b>ライン（引受割合）</b>を入れて共同引受する。
    /// 引受能力（capacity）・ネームの拠出資本・引受割合を持つ。1つの巨大リスクを多数のシンジケートで分け合う（解決は
    /// <see cref="InsuranceRules"/>）。海上保険の起源＝通商破壊（#94/#95）の船団を引き受ける。
    /// </summary>
    [System.Serializable]
    public class LloydsSyndicate
    {
        public string name = "シンジケート";

        /// <summary>引受能力（このシンジケートが引き受けられる上限額＝ネームの資本に裏打ちされる）。</summary>
        public float capacity = 100f;

        /// <summary>ネームの拠出資本（引受能力を支える資本提供者の出資）。</summary>
        public float nameCapital = 0f;

        /// <summary>引受割合（提示されたリスクのうち取りにいくライン＝0..1）。</summary>
        public float lineShare = 0.1f;

        public LloydsSyndicate() { }

        public LloydsSyndicate(string name, float capacity, float lineShare = 0.1f, float nameCapital = 0f)
        {
            this.name = string.IsNullOrEmpty(name) ? "シンジケート" : name;
            this.capacity = capacity;
            this.lineShare = lineShare;
            this.nameCapital = nameCapital;
        }
    }
}
