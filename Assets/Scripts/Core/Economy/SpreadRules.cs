using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// スプレッド（マージン＝出力−入力・原料高で採算消失・#1111）の純ロジック（唯一の窓口）。
    /// 加工マージン（クラックスプレッド型＝製品価値−原料コスト）が生産の継続を決める＝<b>原料高×製品安が同時に来ると
    /// マージンが挟み撃ちで消え、工場は静かに止まる</b>。可変費すら賄えなければ操業停止（ミクロ経済の操業停止条件）。
    /// 分担：<see cref="FiscalRules"/>＝国家財政（歳入歳出・債務）のマクロ層／<see cref="MarketRules"/>＝需給で価格そのものを
    /// 決める市場層／<see cref="CoupledProductionRules"/>＝1原料→複数製品の連産（産出比）。本ルールは<b>個別工程の採算の力学</b>
    /// ＝与えられた価格から「この工程を回す意味があるか」だけを判定する（価格決定も国家会計も持たない）。乱数なし決定論・test-first。
    /// </summary>
    public static class SpreadRules
    {
        /// <summary>スプレッドの調整値（マジックナンバー禁止＝集約）。</summary>
        public readonly struct SpreadParams
        {
            public readonly float breakEvenMargin;   // 損益分岐マージン（これを割ると操業意欲0＝採算割れ停止）
            public readonly float incentiveSlope;     // 損益分岐超過1あたりの操業意欲の立ち上がり
            public readonly float squeezeSensitivity; // 原料高×製品安の挟み撃ちがマージンを削る感度

            public SpreadParams(float breakEvenMargin, float incentiveSlope, float squeezeSensitivity)
            {
                this.breakEvenMargin = Mathf.Max(0f, breakEvenMargin);
                this.incentiveSlope = Mathf.Max(0f, incentiveSlope);
                this.squeezeSensitivity = Mathf.Max(0f, squeezeSensitivity);
            }

            /// <summary>既定＝損益分岐マージン1.0・操業意欲の傾き0.5・挟み撃ち感度1.0。</summary>
            public static SpreadParams Default => new SpreadParams(1.0f, 0.5f, 1.0f);
        }

        /// <summary>粗マージン＝出力価値−投入コスト（クラックスプレッド型）。負を許容＝原料高での採算割れを表現。</summary>
        public static float GrossSpread(float outputValue, float inputCost)
            => Mathf.Max(0f, outputValue) - Mathf.Max(0f, inputCost);

        /// <summary>純マージン＝粗マージン−加工費（変換コストを引いた手取り）。負を許容。</summary>
        public static float NetMargin(float grossSpread, float conversionCost)
            => grossSpread - Mathf.Max(0f, conversionCost);

        /// <summary>採算判定＝純マージンが正なら操業する意味がある（0以下は止める）。</summary>
        public static bool IsProfitable(float netMargin)
            => netMargin > 0f;

        /// <summary>
        /// 操業意欲 0..1＝マージンが薄いほど縮小し、損益分岐を割ると停止（0）。
        /// breakEven 超過ぶんを incentiveSlope で立ち上げ、1で飽和＝原料高でマージンが痩せると工場は静かに減速・停止する。
        /// </summary>
        public static float ProductionIncentive(float netMargin, float breakEvenMargin, SpreadParams p)
        {
            float floor = Mathf.Max(0f, breakEvenMargin);
            if (netMargin <= floor) return 0f; // 損益分岐割れ＝操業停止
            return Mathf.Clamp01((netMargin - floor) * p.incentiveSlope);
        }

        /// <summary>
        /// マージンの圧迫（挟み撃ち）。原料高(inputPriceChange&gt;0)と製品安(outputPriceChange&lt;0)が同時に来ると
        /// マージンが二重に削れる＝<b>原料高×製品安でマージンが消える</b>。dt 追従・負マージン（採算割れ）まで沈める。
        /// </summary>
        public static float MarginSqueezeTick(float margin, float inputPriceChange, float outputPriceChange, float dt, SpreadParams p)
        {
            if (dt <= 0f) return margin;
            // 製品安（出力低下）はそのまま、原料高（入力上昇）はそのままマージンを削る向きに合成。
            float pressure = inputPriceChange - outputPriceChange; // 原料高＋製品安の両方が pressure を増やす
            return margin - pressure * p.squeezeSensitivity * dt;
        }

        /// <summary>
        /// 操業停止点＝固定費を除き可変費すら賄えるか（ミクロ経済の操業停止条件）。
        /// 必要な最低出力価値＝可変費（固定費は短期では埋没＝止めても掛かる）。これを下回る価格では操業を止めた方がよい。
        /// </summary>
        public static float ShutdownThreshold(float fixedCost, float variableCost)
            => Mathf.Max(0f, variableCost);
    }
}
