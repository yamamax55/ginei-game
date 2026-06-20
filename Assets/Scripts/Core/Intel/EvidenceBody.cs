using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 証拠体（IHER-1 #2397『星を継ぐもの』）。ある主題群について集まった証拠断片
    /// (<see cref="EvidenceFragment"/>) の集合。断片は独立に蓄積され、収束（分野の多様性）で
    /// 実効信頼性が高まる（評価は <see cref="EvidenceRules"/>）。純データ（非 MonoBehaviour・直列化可）。
    /// </summary>
    [System.Serializable]
    public class EvidenceBody
    {
        /// <summary>集まった証拠断片の一覧。</summary>
        public List<EvidenceFragment> fragments = new List<EvidenceFragment>();

        /// <summary>断片を1つ追加する（null は無視）。</summary>
        public void Add(EvidenceFragment fragment)
        {
            if (fragment == null) return;
            fragments.Add(fragment);
        }
    }
}
