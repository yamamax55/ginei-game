using UnityEngine;

namespace Ginei
{
    /// <summary>希望・末人（ロンドン派）の調整係数（#852〜#856）。</summary>
    public readonly struct HopeParams
    {
        /// <summary>希望がこれ未満（かつ抑圧不足）で末人（ロンドン派）が立つ。</summary>
        public readonly float dissentThreshold;
        /// <summary>抑圧がこれ以上なら末人を力で鎮圧できる（秩序ルート）。</summary>
        public readonly float suppressThreshold;
        /// <summary>抑圧がこれ以上なら専制（虚構の暴走 #856）。</summary>
        public readonly float tyrannyThreshold;

        public HopeParams(float dissentThreshold, float suppressThreshold, float tyrannyThreshold)
        {
            this.dissentThreshold = dissentThreshold;
            this.suppressThreshold = suppressThreshold;
            this.tyrannyThreshold = tyrannyThreshold;
        }

        public static HopeParams Default => new HopeParams(0.25f, 0.5f, 0.8f);
    }

    /// <summary>
    /// 共同体の希望と末人（ロンドン派）の純ロジック（フロストパンク #852〜#856・FRONT 末人 #847）。
    /// 希望が尽きると末人が内部に立つ（意味の喪失＝ニヒリズム）。対抗＝<b>信仰ルート</b>（意味を捏造して
    /// 希望を上げる）／<b>秩序ルート</b>（力で抑える＝抑圧を上げて鎮圧）。秩序を進めすぎると専制（虚構の暴走）。
    /// 「生き残る価値はあったか？」（救いの暗い鏡 #857）に繋がる。test-first。
    /// </summary>
    public static class HopeRules
    {
        /// <summary>出来事で希望を上下する（破局で大きく下げ、成長/勝利で上げる）。</summary>
        public static void Shift(Community c, float delta)
        {
            if (c == null) return;
            c.hope = Mathf.Clamp01(c.hope + delta);
        }

        /// <summary>
        /// 末人（ロンドン派）の発火を判定・更新する。希望が閾値割れ＆抑圧が不十分なら立つ（dissent=true）。
        /// 希望が回復するか、力で抑え込めば鎮静（dissent=false）。
        /// </summary>
        public static bool UpdateDissent(Community c, HopeParams p)
        {
            if (c == null) return false;
            c.dissent = c.hope < p.dissentThreshold && c.repression < p.suppressThreshold;
            return c.dissent;
        }

        public static bool UpdateDissent(Community c) => UpdateDissent(c, HopeParams.Default);

        /// <summary>信仰ルート（#855）：超越的フィクションで「試練に意味がある」と納得させ、希望を上げる。</summary>
        public static void Faith(Community c, float amount) => Shift(c, Mathf.Abs(amount));

        /// <summary>秩序ルート（#855）：規律・監視・力で不満を抑える＝抑圧を上げる（希望は上げない）。</summary>
        public static void Order(Community c, float amount)
        {
            if (c == null) return;
            c.repression = Mathf.Clamp01(c.repression + Mathf.Abs(amount));
        }

        /// <summary>専制か（虚構の暴走 #856）＝抑圧が過剰。意味を求めた末に人間性を捨てたバグった虚構。</summary>
        public static bool IsTyranny(Community c, HopeParams p) => c != null && c.repression >= p.tyrannyThreshold;

        public static bool IsTyranny(Community c) => IsTyranny(c, HopeParams.Default);
    }
}
