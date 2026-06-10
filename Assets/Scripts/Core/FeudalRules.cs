using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 封建制・貴族制の純ロジック（#168/#169）。封臣の<b>軍役供出</b>・<b>反乱リスク</b>と、
    /// 門地開放（#169）の<b>平民登用率</b>を単一の数式に集約する。test-first。
    /// 「忠誠が高いほど兵を出すが、自治権が高く中央が弱いほど反乱しやすい。門地（家柄独占）への投資が
    /// 平民の登用を開く」を表す。基準値非破壊（実効値パターン）・入力に対して決定的（乱数なし）。
    /// </summary>
    public static class FeudalRules
    {
        /// <summary>封建ロジックの調整値。</summary>
        public readonly struct FeudalParams
        {
            public readonly float autonomyRebelWeight;   // 自治権が反乱リスクを押し上げる重み
            public readonly float kingPowerRebelWeight;   // 君主の威令が反乱リスクを抑える重み
            public readonly float monopolyInvestScale;    // 門地開放投資の効き（投資→開放率）
            public readonly float maxCommonerRatio;       // 平民登用率の上限（門地が完全には消えない）

            public FeudalParams(float autonomyRebelWeight, float kingPowerRebelWeight,
                                float monopolyInvestScale, float maxCommonerRatio)
            {
                this.autonomyRebelWeight = autonomyRebelWeight;
                this.kingPowerRebelWeight = kingPowerRebelWeight;
                this.monopolyInvestScale = monopolyInvestScale;
                this.maxCommonerRatio = Mathf.Clamp01(maxCommonerRatio);
            }

            /// <summary>既定＝自治権重み0.6・君主威令重み0.7・投資効き0.5・登用上限0.8。</summary>
            public static FeudalParams Default => new FeudalParams(0.6f, 0.7f, 0.5f, 0.8f);
        }

        /// <summary>
        /// 封臣が供出する軍役兵力＝忠誠×levy。忠誠が低いほど兵を出し渋る。
        /// 0以上の整数で返す（端数切り捨て）。
        /// </summary>
        public static int LevyContribution(Fief fief, FeudalParams p)
        {
            if (fief == null) return 0;
            float loyalty = Mathf.Clamp01(fief.vassalLoyalty);
            int levy = Mathf.Max(0, fief.levySize);
            return Mathf.FloorToInt(loyalty * levy);
        }

        /// <summary>
        /// 封臣の反乱リスク（0..1）＝(1−忠誠) を基準に、自治権が押し上げ・君主の威令(kingPower)が抑える。
        /// 忠誠が高く君主が強いほど低い。clamp で 0..1 に収める。
        /// </summary>
        public static float VassalRebellionRisk(Fief fief, float kingPower, FeudalParams p)
        {
            if (fief == null) return 0f;
            float disloyalty = 1f - Mathf.Clamp01(fief.vassalLoyalty);
            float autonomy = Mathf.Clamp01(fief.autonomy);
            float king = Mathf.Clamp01(kingPower);
            float risk = disloyalty
                         + p.autonomyRebelWeight * autonomy
                         - p.kingPowerRebelWeight * king;
            return Mathf.Clamp01(risk);
        }

        /// <summary>
        /// 門地開放（#169）＝家柄独占を投資で切り崩した結果の<b>平民登用率</b>（0..1）。
        /// investment(0..1) が大きいほど開放が進むが上限 <see cref="FeudalParams.maxCommonerRatio"/> を超えない。
        /// </summary>
        public static float MonopolyOpening(float investment, FeudalParams p)
        {
            float invest = Mathf.Clamp01(investment);
            float opened = invest * p.monopolyInvestScale;
            return Mathf.Clamp(opened, 0f, p.maxCommonerRatio);
        }
    }
}
