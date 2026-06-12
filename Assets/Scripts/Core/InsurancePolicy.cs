namespace Ginei
{
    /// <summary>
    /// 保険契約（#1982 INS・純データ）。1件の引受＝損害発生確率・損害額（保険金額）・保険料。多数集めてリスクプールにすると
    /// 大数の法則で損失率が安定する（解決は <see cref="InsuranceRules"/>）。個別契約は持たず少数の代表契約で集計（タイクン化回避）。
    /// </summary>
    [System.Serializable]
    public class InsurancePolicy
    {
        public string name = "保険契約";

        /// <summary>損害発生確率（0..1）。</summary>
        public float probability;

        /// <summary>損害額＝保険金額（事故時に支払う額）。</summary>
        public float lossAmount;

        /// <summary>保険料（契約者から受け取る額）。</summary>
        public float premium;

        public InsurancePolicy() { }

        public InsurancePolicy(float probability, float lossAmount, float premium = 0f, string name = null)
        {
            this.probability = probability;
            this.lossAmount = lossAmount;
            this.premium = premium;
            if (!string.IsNullOrEmpty(name)) this.name = name;
        }
    }
}
