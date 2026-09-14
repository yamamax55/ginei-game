namespace Ginei
{
    /// <summary>
    /// 一人の国政の当選履歴と現在の議員資格（勢力の <see cref="PoliticsState.legislators"/> に人物IDで1件）。
    /// 当選回数はゲーム内で開票した当選だけを数え、開始前の経歴はシナリオが明示したときだけ <see cref="priorKnown"/> で持つ（年齢から捏造しない）。
    /// 地方知事の当選はここに含めない（<see cref="LocalElectionState"/> 側）。能力・階級・任命権は変えない＝年功の素材のみ。
    /// 更新は <see cref="LegislatorRosterRules"/> が唯一の窓口。純データ（セーブに乗る）。
    /// </summary>
    [System.Serializable]
    public class LegislatorRecord
    {
        public int personId = -1;

        // --- ゲーム内の当選記録（開票イベントごとに1回だけ増える） ---
        public int lowerWins;
        public int upperWins;
        /// <summary>同じ議院で途切れず再選された回数（落選・失職で0。初当選で1）。</summary>
        public int consecutiveWins;
        /// <summary>初当選の年（0=記録なし）。</summary>
        public int firstWinYear;
        /// <summary>直近の当選の年（0=記録なし）。</summary>
        public int lastWinYear;
        /// <summary>この人物の記録を始めた年（それ以前の経歴は <see cref="priorKnown"/> でなければ不明）。</summary>
        public int recordStartYear;
        /// <summary>最後に数えた下院／上院の選挙ID（同じ開票で二重に数えない）。</summary>
        public string lastLowerElectionId = "";
        public string lastUpperElectionId = "";

        // --- ゲーム開始前の経歴（シナリオが明示した場合だけ） ---
        public bool priorKnown;
        public int priorLowerWins;
        public int priorUpperWins;

        // --- 現在の議員資格 ---
        public bool seated;
        public LegislativeChamber seatChamber;
        /// <summary>改選区分（-1=下院、0=上院A、1=上院B）。</summary>
        public int seatClass = -1;
        /// <summary>議席の帰属する政党（当選時の党）。</summary>
        public int seatPartyId = -1;
        public int seatElectedYear;
        public string seatElectionId = "";
        /// <summary>落選・失職などの直近の理由（在任中は空）。</summary>
        public string statusReason = "";

        public LegislatorRecord() { }
        public LegislatorRecord(int personId) { this.personId = personId; }

        /// <summary>下院の累積当選（明示された開始前の経歴を含む）。</summary>
        public int TotalLowerWins => lowerWins + (priorKnown ? priorLowerWins : 0);
        /// <summary>上院の累積当選（明示された開始前の経歴を含む）。</summary>
        public int TotalUpperWins => upperWins + (priorKnown ? priorUpperWins : 0);
        /// <summary>国政の累積当選。</summary>
        public int TotalWins => TotalLowerWins + TotalUpperWins;
    }
}
