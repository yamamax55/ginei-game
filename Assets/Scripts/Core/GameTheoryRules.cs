using UnityEngine;

namespace Ginei
{
    /// <summary>2x2 ゲームの手（#388 汎用2人ゲーム）。</summary>
    public enum Move
    {
        協調,
        裏切り
    }

    /// <summary>
    /// 2x2 対称ゲームの利得表の調整値（#388）。
    /// 既定は囚人のジレンマ T(誘惑)&gt;R(協調報酬)&gt;P(裏切り罰)&gt;S(間抜けの報酬)。
    /// </summary>
    public readonly struct GameTheoryParams
    {
        /// <summary>T：誘惑＝自分だけ裏切ったときの最高利得。</summary>
        public readonly float temptation;
        /// <summary>R：報酬＝双方協調の利得。</summary>
        public readonly float reward;
        /// <summary>P：罰＝双方裏切りの利得。</summary>
        public readonly float punishment;
        /// <summary>S：間抜け＝自分だけ協調したときの最低利得。</summary>
        public readonly float sucker;

        public GameTheoryParams(float temptation, float reward, float punishment, float sucker)
        {
            this.temptation = temptation;
            this.reward = reward;
            this.punishment = punishment;
            this.sucker = sucker;
        }

        /// <summary>既定＝古典的な囚人のジレンマ（T5 &gt; R3 &gt; P1 &gt; S0）。</summary>
        public static GameTheoryParams Default => new GameTheoryParams(5f, 3f, 1f, 0f);
    }

    /// <summary>
    /// 汎用 2x2 対称ゲームの純ロジック（#388）。
    /// 利得表（<see cref="Payoff"/>）・ナッシュ均衡手（<see cref="NashEquilibrium"/>）・
    /// しっぺ返し戦略（<see cref="TitForTat"/>）・ゼロサム判定（<see cref="IsZeroSum"/>）を提供する。
    /// 外交・寝返り・AI 方針など「協調か裏切りか」の意思決定の土台。test-first。
    /// </summary>
    public static class GameTheoryRules
    {
        /// <summary>
        /// 自分の手 self・相手の手 other に対する自分の利得を利得表から返す。
        /// 対称ゲームなので相手の利得は引数を入れ替えて同じ関数で得られる。
        /// </summary>
        public static float Payoff(Move self, Move other, GameTheoryParams p)
        {
            switch (self)
            {
                case Move.協調:
                    // 協調×協調＝報酬R／協調×裏切り＝間抜けS
                    return other == Move.協調 ? p.reward : p.sucker;
                case Move.裏切り:
                    // 裏切り×協調＝誘惑T／裏切り×裏切り＝罰P
                    return other == Move.協調 ? p.temptation : p.punishment;
                default:
                    return 0f;
            }
        }

        public static float Payoff(Move self, Move other) => Payoff(self, other, GameTheoryParams.Default);

        /// <summary>
        /// 支配戦略（ナッシュ均衡）の手を返す。相手の各手に対する最善手が一致する＝支配戦略を採用。
        /// 一致しない（協調×裏切りの混在）なら、相手協調時の最善手で代表する。
        /// 囚人のジレンマ（T&gt;R かつ P&gt;S）では裏切りが支配戦略になる。
        /// </summary>
        public static Move NashEquilibrium(GameTheoryParams p)
        {
            // 相手が協調したときの最善手：T(裏切り) vs R(協調)。
            // 支配戦略があればこれと相手裏切り時の最善手が一致し、一致しない場合も
            // 協調時の最善手で代表する仕様なので、いずれにせよこの手を返す。
            return p.temptation > p.reward ? Move.裏切り : Move.協調;
        }

        public static Move NashEquilibrium() => NashEquilibrium(GameTheoryParams.Default);

        /// <summary>しっぺ返し：相手が前回出した手をそのまま返す（初手は別途協調から始める運用）。</summary>
        public static Move TitForTat(Move prevOpponent) => prevOpponent;

        /// <summary>
        /// ゼロサム判定：全セルで「自分の利得＋相手の利得＝0」が成り立つか。
        /// 対称ゲームでは双方協調 2R・双方裏切り 2P・片側 T+S の和がすべて 0 のとき真。
        /// </summary>
        public static bool IsZeroSum(GameTheoryParams p)
        {
            const float Epsilon = 0.0001f;
            bool coopCoop = Mathf.Abs(p.reward + p.reward) < Epsilon;        // (C,C)＝R+R
            bool defectDefect = Mathf.Abs(p.punishment + p.punishment) < Epsilon; // (D,D)＝P+P
            bool mixed = Mathf.Abs(p.temptation + p.sucker) < Epsilon;       // (D,C)＝T+S（裏返しも同値）
            return coopCoop && defectDefect && mixed;
        }

        public static bool IsZeroSum() => IsZeroSum(GameTheoryParams.Default);
    }
}
