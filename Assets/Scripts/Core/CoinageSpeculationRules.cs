using UnityEngine;

namespace Ginei
{
    /// <summary>改鋳投機（噂による相場変動）の調整係数。</summary>
    public readonly struct CoinageSpeculationParams
    {
        /// <summary>噂が相場を動かす基礎強度（信用×予想方向の大きさ×これ＝相場インパクト）。</summary>
        public readonly float rumorImpactScale;
        /// <summary>投機ポジションの建ち方（相場インパクト×資本×リスク選好×これ＝張る量）。</summary>
        public readonly float positionScale;
        /// <summary>噂の的中度が利益に効く強度（実改鋳が噂どおりなら満額・外れれば損へ転じる傾き）。</summary>
        public readonly float profitScale;
        /// <summary>内部情報の優位の基礎倍率（情報の早さ×これ＝改鋳を事前に知る者の取り分）。</summary>
        public readonly float insiderScale;
        /// <summary>噂の累積が信認を揺らす強度（相場インパクト×累積噂×これ＝改鋳前から動く実体）。</summary>
        public readonly float panicScale;

        public CoinageSpeculationParams(float rumorImpactScale, float positionScale, float profitScale, float insiderScale, float panicScale)
        {
            this.rumorImpactScale = Mathf.Max(0f, rumorImpactScale);
            this.positionScale = Mathf.Max(0f, positionScale);
            this.profitScale = Mathf.Max(0f, profitScale);
            this.insiderScale = Mathf.Max(0f, insiderScale);
            this.panicScale = Mathf.Max(0f, panicScale);
        }

        /// <summary>既定＝噂強度1.0・ポジション1.0・利益傾き1.0・内部情報2.0・混乱0.2。</summary>
        public static CoinageSpeculationParams Default => new CoinageSpeculationParams(1f, 1f, 1f, 2f, 0.2f);
    }

    /// <summary>
    /// 改鋳投機の純ロジック（#1073・狼と香辛料＝戦わぬ経済戦）。通貨の品位改定（改鋳）の「噂」が流れると、
    /// 商人は品位上昇の噂なら旧貨を買い集め、低下の噂なら売り抜けて儲ける＝情報で動く投機。
    /// 噂どおりに改鋳が起きれば儲かり、噂が外れれば損をする＝情報の精度が利益を決める。さらに改鋳を
    /// 事前に知る内部者が一番儲け（情報の非対称）、噂が噂を呼べば改鋳より先に相場と信認が動く
    /// ＝「改鋳の噂が改鋳より先に相場を動かす＝戦わぬ経済戦は情報戦」。
    /// <see cref="CoinageRules"/>（改鋳の実体＝品位低下・発行益・グレシャム）の投機版で、こちらは噂が
    /// 起こす相場変動を扱う。情報の非対称は <see cref="InformationAsymmetryRules"/>（同Wave並行）と接続し、
    /// 物価そのものの上昇は <see cref="InflationRules"/> が別系統で扱う。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class CoinageSpeculationRules
    {
        /// <summary>
        /// 噂が相場を動かす力＝噂の信憑性(0..1)×予想される品位改定方向(-1..1＝品位上昇+/低下-)×強度。
        /// 信じられた噂ほど相場が大きく動く＝噂で値が動く。符号は方向を引き継ぐ（買い圧+/売り圧-）。
        /// </summary>
        public static float RumorImpact(float rumorCredibility, float expectedDebasementDirection, CoinageSpeculationParams p)
        {
            float cred = Mathf.Clamp01(rumorCredibility);
            float dir = Mathf.Clamp(expectedDebasementDirection, -1f, 1f);
            return cred * dir * p.rumorImpactScale;
        }

        public static float RumorImpact(float rumorCredibility, float expectedDebasementDirection)
            => RumorImpact(rumorCredibility, expectedDebasementDirection, CoinageSpeculationParams.Default);

        /// <summary>
        /// 投機ポジション＝相場インパクト×投入資本×リスク選好(0..1)×強度。品位上昇の噂（正）なら旧貨を
        /// 買い込む（正＝ロング）、低下の噂（負）なら売り抜ける（負＝ショート）。符号がそのまま建玉の向き。
        /// </summary>
        public static float SpeculativePosition(float rumorImpact, float merchantCapital, float riskAppetite, CoinageSpeculationParams p)
        {
            float capital = Mathf.Max(0f, merchantCapital);
            float appetite = Mathf.Clamp01(riskAppetite);
            return rumorImpact * capital * appetite * p.positionScale;
        }

        public static float SpeculativePosition(float rumorImpact, float merchantCapital, float riskAppetite)
            => SpeculativePosition(rumorImpact, merchantCapital, riskAppetite, CoinageSpeculationParams.Default);

        /// <summary>
        /// 投機の損益＝ポジション×(実改鋳−噂された改鋳)が同方向なら儲け・逆なら損。噂どおり（実＝噂方向）に
        /// 動けば利益、噂が外れれば損＝情報の精度が利益を決める。買い（正ポジ）は値上がり（実が噂を上回る側）で
        /// 儲かり、売り（負ポジ）は値下がりで儲かる。引数は改鋳幅(-1..1＝上昇+/低下-)。
        /// </summary>
        public static float SpeculativeProfit(float position, float actualDebasement, float rumoredDebasement, CoinageSpeculationParams p)
        {
            float actual = Mathf.Clamp(actualDebasement, -1f, 1f);
            float rumored = Mathf.Clamp(rumoredDebasement, -1f, 1f);
            // 噂どおりに実改鋳が出れば(actual と rumored 同符号で大)、建てた向き(position)に沿って利益。
            float realized = actual; // 確定した改鋳幅が実現値
            return position * realized * p.profitScale;
        }

        public static float SpeculativeProfit(float position, float actualDebasement, float rumoredDebasement)
            => SpeculativeProfit(position, actualDebasement, rumoredDebasement, CoinageSpeculationParams.Default);

        /// <summary>
        /// 内部情報の優位＝情報の早さ(0..1)×基礎倍率。改鋳を事前に知る者ほど噂が信用される前に建玉でき、
        /// 一番儲ける＝情報の非対称（<see cref="InformationAsymmetryRules"/> と接続）。早さ1.0で最大優位。
        /// </summary>
        public static float InsiderAdvantage(float informationLead, CoinageSpeculationParams p)
        {
            return Mathf.Clamp01(informationLead) * p.insiderScale;
        }

        public static float InsiderAdvantage(float informationLead)
            => InsiderAdvantage(informationLead, CoinageSpeculationParams.Default);

        /// <summary>
        /// 相場の混乱＝|相場インパクト|×累積噂数×強度（0..1）。噂が噂を呼ぶ（累積が増える）ほど通貨の信認が
        /// 揺らぐ＝改鋳が実施される前から実体（相場・信認）が動く。0で平穏、1で信認崩壊寸前。
        /// </summary>
        public static float MarketPanic(float rumorImpact, int cumulativeRumors, CoinageSpeculationParams p)
        {
            int count = Mathf.Max(0, cumulativeRumors);
            return Mathf.Clamp01(Mathf.Abs(rumorImpact) * count * p.panicScale);
        }

        public static float MarketPanic(float rumorImpact, int cumulativeRumors)
            => MarketPanic(rumorImpact, cumulativeRumors, CoinageSpeculationParams.Default);

        /// <summary>
        /// 退蔵圧力（0..1）＝品位低下の予想方向（負）ほど、退蔵性向(0..1)に応じて良貨が抱え込まれる。
        /// 品位低下の噂が出た時点で良貨が市場から消える＝<see cref="CoinageRules.GreshamEffect"/>を改鋳前に
        /// 前倒しする（噂が実体を先取りする）。品位上昇の予想（正）では退蔵は起きない（0）。
        /// </summary>
        public static float HoardingPressure(float expectedDebasementDirection, float hoardingTendency)
        {
            float dir = Mathf.Clamp(expectedDebasementDirection, -1f, 1f);
            float tendency = Mathf.Clamp01(hoardingTendency);
            float debaseExpectation = Mathf.Max(0f, -dir); // 低下方向の強さ（0..1）
            return Mathf.Clamp01(debaseExpectation * tendency);
        }
    }
}
