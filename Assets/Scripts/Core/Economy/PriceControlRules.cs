using UnityEngine;

namespace Ginei
{
    /// <summary>価格統制の調整係数。</summary>
    public readonly struct PriceControlParams
    {
        /// <summary>品不足を行列の長さへ換算する係数（不足1あたりの相対行列長）。</summary>
        public readonly float queueRate;
        /// <summary>闇価格プレミアムの最大倍率（不足最大×取締最強で統制価格のこの倍まで跳ねる）。</summary>
        public readonly float maxBlackPremium;
        /// <summary>取り締まりが闇値を押し上げる寄与（取締が厳しいほどリスク料が闇値に乗る）。</summary>
        public readonly float enforcementPremiumWeight;
        /// <summary>採算割れの統制価格が供給を縮める速度（per dt・割れ幅1のとき）。</summary>
        public readonly float supplyDestructionRate;
        /// <summary>統制失敗とみなす闇値プレミアムの肩（このプレミアムで失敗度が0.5に達する）。</summary>
        public readonly float failureHalfPremium;

        public PriceControlParams(float queueRate, float maxBlackPremium, float enforcementPremiumWeight,
                                  float supplyDestructionRate, float failureHalfPremium)
        {
            this.queueRate = Mathf.Max(0f, queueRate);
            this.maxBlackPremium = Mathf.Max(0f, maxBlackPremium);
            this.enforcementPremiumWeight = Mathf.Clamp01(enforcementPremiumWeight);
            this.supplyDestructionRate = Mathf.Max(0f, supplyDestructionRate);
            this.failureHalfPremium = Mathf.Max(0.01f, failureHalfPremium);
        }

        /// <summary>既定＝行列係数2・闇値プレミアム上限3倍・取締寄与0.5・供給縮小0.1・失敗半飽和プレミアム1.0。</summary>
        public static PriceControlParams Default
            => new PriceControlParams(2f, 3f, 0.5f, 0.1f, 1f);
    }

    /// <summary>
    /// 価格統制の純ロジック＝統制価格は紙の上の安さ。統制価格（controlledPrice）を市場均衡価格
    /// （marketClearingPrice）より下に固定しても希少性は消えず、抑えた歪みは品不足（<see cref="Shortage"/>）と
    /// 行列（<see cref="QueueLength"/>＝金の代わりに時間で払う）と闇値（<see cref="BlackMarketPremium"/>）で噴出する。
    /// 採算割れの統制価格は生産者を撤退させ不足を悪化させる（<see cref="SupplyDestructionTick"/>＝統制の自己破壊）。
    /// 闇価格との乖離が大きいほど統制は形骸（<see cref="ControlFailureIndex"/>）。買えれば安いが買えない＝安さの幻想
    /// （<see cref="ConsumerSurplusIllusion"/>）。量の配分（行列の中身＝誰にいくつ）は <see cref="RationingRules"/>、
    /// 非合法の並行市場そのものの規模・取締は <see cref="BlackMarketRules"/>（本クラスは闇値プレミアムを供給する側）、
    /// 自由な需給均衡価格は <see cref="MarketRules"/> が扱い、ここは統制価格と均衡価格の乖離が生む歪みのみを扱う。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class PriceControlRules
    {
        /// <summary>
        /// 品不足の規模（0..1）＝統制価格が均衡を下回るほど超過需要で棚が空く。
        /// (均衡−統制)/均衡＝統制が均衡以上なら不足0（抑えていない）、統制0なら不足1（タダ＝総取り）。
        /// 価格を抑えても希少性は消えない＝抑えた分がそのまま不足になる。
        /// </summary>
        public static float Shortage(float controlledPrice, float marketClearingPrice)
        {
            float pc = Mathf.Max(0f, controlledPrice);
            float pm = Mathf.Max(0f, marketClearingPrice);
            if (pm <= 0f) return 0f;
            return Mathf.Clamp01((pm - pc) / pm);
        }

        /// <summary>
        /// 行列の長さ（相対値）＝品不足を時間で配給する＝金の代わりに時間で払う。不足に比例（queueRate 倍）。
        /// 価格で割り当てられない需要は並ぶ時間で割り当てられる＝統制の歪みは行列に化ける。
        /// </summary>
        public static float QueueLength(float shortage, PriceControlParams p)
        {
            return p.queueRate * Mathf.Clamp01(shortage);
        }

        public static float QueueLength(float shortage) => QueueLength(shortage, PriceControlParams.Default);

        /// <summary>
        /// 闇価格のプレミアム（統制価格に対する上乗せ倍率、0..maxBlackPremium）。
        /// 品不足が深く取り締まり（enforcement 0..1）が厳しいほど跳ねる＝統制を強めるほど闇値が高くつく
        /// （取締はリスク料として闇値に転嫁される）。<see cref="BlackMarketRules"/> の規模・命綱判定の価格入力。
        /// 不足0なら0＝買えるなら闇市の出番は無い。
        /// </summary>
        public static float BlackMarketPremium(float shortage, float enforcement, PriceControlParams p)
        {
            float sh = Mathf.Clamp01(shortage);
            float e = Mathf.Clamp01(enforcement);
            // 取締が厳しいほどリスク料が乗る：基礎(1)＋取締寄与
            float riskMul = 1f + p.enforcementPremiumWeight * e;
            return p.maxBlackPremium * sh * riskMul / (1f + p.enforcementPremiumWeight);
        }

        public static float BlackMarketPremium(float shortage, float enforcement)
            => BlackMarketPremium(shortage, enforcement, PriceControlParams.Default);

        /// <summary>
        /// 供給の1tick後の値＝採算割れの統制価格は生産者を撤退させる。統制価格が生産コスト（costOfProduction）を
        /// 下回る割れ幅に比例して供給が縮む（per dt）。不足を悪化させ次期の <see cref="Shortage"/> を深める
        /// ＝統制の自己破壊（抑えるほど作る者が去り、ますます足りなくなる）。統制価格≥コストなら供給は減らない。下限0。
        /// </summary>
        public static float SupplyDestructionTick(float supply, float controlledPrice, float costOfProduction,
                                                  float dt, PriceControlParams p)
        {
            float s = Mathf.Max(0f, supply);
            float pc = Mathf.Max(0f, controlledPrice);
            float cost = Mathf.Max(0f, costOfProduction);
            float d = Mathf.Max(0f, dt);
            if (cost <= 0f || pc >= cost) return s;
            // 割れ幅（コストに対する不足分の割合 0..1）
            float underwater = Mathf.Clamp01((cost - pc) / cost);
            float exit = p.supplyDestructionRate * underwater * s * d;
            return Mathf.Max(0f, s - exit);
        }

        public static float SupplyDestructionTick(float supply, float controlledPrice, float costOfProduction, float dt)
            => SupplyDestructionTick(supply, controlledPrice, costOfProduction, dt, PriceControlParams.Default);

        /// <summary>
        /// 統制の失敗度指標（0..1）＝闇価格プレミアムが大きいほど統制は形骸。
        /// プレミアム/(プレミアム＋failureHalfPremium) の飽和カーブ＝乖離が肩で0.5、大乖離で1へ漸近。
        /// 闇値が統制価格から離れるほど、紙の上の統制価格は実体を失う。
        /// </summary>
        public static float ControlFailureIndex(float blackMarketPremium, PriceControlParams p)
        {
            float prem = Mathf.Max(0f, blackMarketPremium);
            return prem / (prem + p.failureHalfPremium);
        }

        public static float ControlFailureIndex(float blackMarketPremium)
            => ControlFailureIndex(blackMarketPremium, PriceControlParams.Default);

        /// <summary>
        /// 安さの幻想（買い手1人あたりの見かけ余剰の期待値）＝統制価格が均衡より安い分の便益に、実際に買える確率
        /// （available／均衡需要≈不足の裏）を掛けたもの。買えれば安いが買えない＝紙の上の安さ。
        /// 見かけ余剰＝(均衡−統制)を、入手可能性 available(0..1) で割り引く＝公示価格の安さは入手率で目減りする。
        /// </summary>
        public static float ConsumerSurplusIllusion(float controlledPrice, float marketClearingPrice, float available)
        {
            float pc = Mathf.Max(0f, controlledPrice);
            float pm = Mathf.Max(0f, marketClearingPrice);
            float avail = Mathf.Clamp01(available);
            float nominalGain = Mathf.Max(0f, pm - pc); // 公示価格の見かけの安さ
            return nominalGain * avail;                 // 買えなければ幻＝入手率で割り引く
        }
    }
}
