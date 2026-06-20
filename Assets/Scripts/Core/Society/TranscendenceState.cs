namespace Ginei
{
    /// <summary>
    /// 超越（人類進化）の進行状態（CEND-1 #2355）。勢力が安定・正統性・成熟（希望）を高め切ると
    /// 「超越」へ進み、達成すると盤面から退場する＝従来の征服勝利の枠から外れる勝利条件。
    /// 解決は <see cref="TranscendenceRules"/>（static）。純データ（非 MonoBehaviour・test-first）。
    /// </summary>
    [System.Serializable]
    public class TranscendenceState
    {
        /// <summary>超越適格スコア 0..1（<see cref="TranscendenceRules.EligibilityScore(FactionState)"/> の最新値を保持してもよい）。</summary>
        public float transcendenceScore;

        /// <summary>超越進行中か（適格に達して進化を開始した・まだ未完了）。</summary>
        public bool isTranscending;

        /// <summary>超越達成済みか（進化を完了し盤面から退場＝従来勝利の対象外）。</summary>
        public bool isTranscended;

        public TranscendenceState() { }

        public TranscendenceState(float transcendenceScore, bool isTranscending = false, bool isTranscended = false)
        {
            this.transcendenceScore = transcendenceScore;
            this.isTranscending = isTranscending;
            this.isTranscended = isTranscended;
        }
    }
}
