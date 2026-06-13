namespace Ginei
{
    /// <summary>
    /// 部隊の交戦規定（ROE: Rules of Engagement）スタンス。
    /// 発砲・追尾・前進の挙動を恒常的に制御する（#2258）。
    /// 後方互換：既定＝攻撃的で従来どおりの挙動。
    /// </summary>
    public enum EngagementStance
    {
        /// <summary>積極的に接近・発砲・追尾する（従来挙動＝後方互換の既定値）。</summary>
        攻撃的,
        /// <summary>射界内の敵には発砲するが、追尾せず前進を抑制する。</summary>
        防御的,
        /// <summary>発砲を停止（射撃管制）し、潜伏・隠密行動に徹する。追尾・前進もしない。</summary>
        射撃管制,
        /// <summary>発砲・追尾・前進をすべて停止し、後方へ退避する。</summary>
        退避,
    }
}
