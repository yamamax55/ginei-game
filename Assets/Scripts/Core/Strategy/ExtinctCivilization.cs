using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 先行文明の記録（#2399 IHER-3）。現勢力より前に銀河に栄え滅んだ文明の遺産データ。
    /// 領域(territorySystemIds)・到達技術段階(peakTechTier)・滅亡時代(extinctionEra)・滅亡原因(extinctionCause) を持つ純データ。
    /// 遺産は <see cref="ExtinctCivRules"/> が現勢力の研究・地勢・気質へ橋渡しする（ロジックは持たない）。
    /// </summary>
    [System.Serializable]
    public class ExtinctCivilization
    {
        /// <summary>識別ID。</summary>
        public int id;
        /// <summary>文明名。</summary>
        public string civName;
        /// <summary>かつての版図（星系IDの一覧）。現銀河マップに残る星系のみ遺跡になる。</summary>
        public List<int> territorySystemIds = new List<int>();
        /// <summary>到達した技術段階（高いほど後継勢力への技術遺産が大きい）。</summary>
        public int peakTechTier;
        /// <summary>滅亡した時代（暦・将来の年代記用）。</summary>
        public int extinctionEra;
        /// <summary>滅亡原因（後継勢力の気質に影響）。</summary>
        public ExtinctionCause extinctionCause = ExtinctionCause.不明;

        public ExtinctCivilization() { }

        public ExtinctCivilization(int id, string civName, int peakTechTier = 0,
            int extinctionEra = 0, ExtinctionCause extinctionCause = ExtinctionCause.不明)
        {
            this.id = id;
            this.civName = civName;
            this.peakTechTier = peakTechTier;
            this.extinctionEra = extinctionEra;
            this.extinctionCause = extinctionCause;
        }
    }
}
