namespace Ginei
{
    /// <summary>
    /// 決裁デスクの絞り込み（純・決定論）。「自分のところに回ってきた稟議だけ表示する」＝プレイヤーの決裁箱
    /// （元首なら <see cref="BoxKind.国王"/>）に宛てられた稟議のみを通す唯一の窓口。箱に紐づかない一般決裁
    /// （イベント/システム）は常に表示。<see cref="DecisionDeck"/>/<see cref="DecisionBoardPanel"/> が使う。test-first。
    /// </summary>
    public static class DecisionRoutingRules
    {
        /// <summary>プレイヤー（勢力の元首）が裁可する箱。既定は国王箱（君主/元首）。※民主政の精緻化は将来ここで。</summary>
        public static BoxKind PlayerDecisionBox() => BoxKind.国王;

        /// <summary>
        /// この決裁をプレイヤーの決裁デスクに出すか。箱宛て（boxRouted）なら自分の箱と一致するときだけ、
        /// 箱に紐づかない一般決裁（boxRouted=false）は常に表示。
        /// </summary>
        public static bool IsForPlayer(bool boxRouted, BoxKind addresseeBox, BoxKind playerBox)
            => !boxRouted || addresseeBox == playerBox;
    }
}
