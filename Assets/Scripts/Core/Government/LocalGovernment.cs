namespace Ginei
{
    /// <summary>
    /// 地方自治体（#109 内政・近代的な地方分権＝封建制 <see cref="Fief"/> #168 とは別軸）。星系/惑星を治める行政体で、
    /// <b>自治度</b>（中央直轄↔高度自治）と<b>行政能力</b>を持つ。自治度が高いほど現地に即した統治で<b>地方の安定</b>が上がるが、
    /// <b>中央の税収/統制</b>が下がり<b>分離独立リスク</b>が上がる（中央集権↔地方分権のトレードオフ）。解決は <see cref="LocalGovernmentRules"/>。
    /// 首長は政府役職（<see cref="OfficeRules"/> #142）として任命する想定（ここは行政体の状態のみ）。純データ。
    /// </summary>
    [System.Serializable]
    public class LocalGovernment
    {
        /// <summary>治める星系ID（StarSystem.id）。</summary>
        public int systemId;

        public string name = "地方自治体";

        /// <summary>自治度（0=中央直轄 .. 1=高度自治）。高いほど地方の応答性↑・中央集権↓・分離リスク↑。</summary>
        public float autonomy = 0.5f;

        /// <summary>行政能力（0..1）。自治の効きは能力に依存（有能な自治は応答的・無能なら効果薄）。</summary>
        public float competence = 0.5f;

        public LocalGovernment() { }

        public LocalGovernment(int systemId, float autonomy = 0.5f, float competence = 0.5f, string name = "地方自治体")
        {
            this.systemId = systemId;
            this.autonomy = autonomy;
            this.competence = competence;
            this.name = string.IsNullOrEmpty(name) ? "地方自治体" : name;
        }
    }
}
