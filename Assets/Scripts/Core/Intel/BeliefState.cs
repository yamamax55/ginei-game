namespace Ginei
{
    /// <summary>
    /// 信念状態（IHER-4 #2400『星を継ぐもの』）。ある勢力が特定の主題(topic)について抱く仮説(hypothesis)と、
    /// その仮説への傾倒度(commitment 0..1)を持つ。傾倒が深いほど、反証が来たときの更新コストが高くつく
    /// （評価は <see cref="BeliefRules"/>）。制度化された組織は更新を拒み硬直する(locked)＝既存パラダイムに固執する。
    /// 純データ（非 MonoBehaviour・直列化可）。
    /// </summary>
    [System.Serializable]
    public class BeliefState
    {
        /// <summary>この信念を抱く勢力 ID。</summary>
        public int factionId;
        /// <summary>主題（何についての信念か）。</summary>
        public string topic;
        /// <summary>現在採用している仮説。</summary>
        public string hypothesis;
        /// <summary>仮説への傾倒度（0..1・深いほど更新コスト大）。</summary>
        public float commitment;
        /// <summary>更新を拒む硬直状態（制度化された組織でロックされる）。</summary>
        public bool locked;

        public BeliefState(int factionId, string topic, string hypothesis, float commitment, bool locked)
        {
            this.factionId = factionId;
            this.topic = topic;
            this.hypothesis = hypothesis;
            this.commitment = commitment;
            this.locked = locked;
        }
    }
}
