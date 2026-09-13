namespace Ginei
{
    /// <summary>
    /// 手動命令が AI 操舵を上書きしているときの<b>出どころ</b>。
    /// 緊急時（敗走・総退却）に中断してよいかがこれで決まる。
    /// </summary>
    public enum ManualOverrideKind
    {
        /// <summary>上書きしていない＝AI が操舵している。</summary>
        なし,

        /// <summary>
        /// プレイヤーが<b>自分の指揮系統へ</b>出した直接命令。
        /// 緊急でも中断しない（従来どおりの扱い＝ここを変えると既存の操作感が変わる）。
        /// </summary>
        直接命令,

        /// <summary>
        /// <b>承諾された支援要請</b>。系統外の指揮官が「引き受けた」命令であって、
        /// 自分の部隊の生存より優先されるものではない。
        /// ＝敗走・総退却では<b>中断して退がれる</b>（承諾する前と同じように退却できる）。
        /// </summary>
        支援要請,
    }

    /// <summary>
    /// 手動上書きの<b>持続と中断</b>の決まり（純ロジック・test-first）。
    ///
    /// <b>なぜ分けるのか</b>：支援要請を承諾した艦に上書きを立てると、
    /// 立てなかったとき（＝承諾前）にはできていた<b>敗走・総退却が塞がれる</b>。
    /// それは支援要請を受けたせいで死ぬということで、要請の趣旨と合わない。
    /// 一方で直接命令は昔から中断しない扱いなので、そちらは動かさない。
    /// </summary>
    public static class ManualOverrideRules
    {
        /// <summary>
        /// 命令が終わったか（＝AI へ返してよいか）。
        /// 移動が終わり、手動の攻撃目標も無く、標準命令（アタックムーブ／保持）も無いとき。
        /// </summary>
        public static bool IsOrderComplete(bool isMoving, bool hasManualTarget, bool hasStandingOrder)
            => !isMoving && !hasManualTarget && !hasStandingOrder;

        /// <summary>
        /// 緊急（敗走・総退却）のために上書きを手放すべきか。
        /// <b>支援要請だけ</b>が対象＝直接命令は従来どおり中断しない。
        /// </summary>
        public static bool ShouldReleaseForEmergency(ManualOverrideKind kind, bool routed, bool corpsRetreatOrdered)
            => kind == ManualOverrideKind.支援要請 && (routed || corpsRetreatOrdered);

        /// <summary>
        /// 総退却の下令が<b>その艦を対象にしてよいか</b>。
        ///
        /// <list type="bullet">
        ///   <item><see cref="ManualOverrideKind.なし"/>＝AI 操舵中。従来どおり退却させる。</item>
        ///   <item><see cref="ManualOverrideKind.支援要請"/>＝<b>なしと同じ扱い</b>
        ///         ＝承諾したせいで退却できなくなる、を作らない。</item>
        ///   <item><see cref="ManualOverrideKind.直接命令"/>＝従来どおり尊重して除外する。</item>
        /// </list>
        /// </summary>
        public static bool CanOrderRetreat(ManualOverrideKind kind)
            => kind != ManualOverrideKind.直接命令;

        /// <summary>上書き中か（<see cref="ManualOverrideKind.なし"/> 以外）。</summary>
        public static bool IsOverriding(ManualOverrideKind kind)
            => kind != ManualOverrideKind.なし;

        /// <summary>中断されたことを知らせる1行（誰の何が、なぜ止まったか）。</summary>
        public static string InterruptedText(string fleetName, string reason)
        {
            string who = string.IsNullOrEmpty(fleetName) ? "指揮系統外の部隊" : fleetName;
            string why = string.IsNullOrEmpty(reason) ? "緊急事態" : reason;
            return $"{who} は{why}のため、引き受けた支援を中断しました";
        }
    }
}
