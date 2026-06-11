using UnityEngine;

namespace Ginei
{
    /// <summary>負債デフレーションの調整係数。</summary>
    public readonly struct DebtDeflationParams
    {
        /// <summary>実質負担→投げ売り圧力のスケール（実質負担が重いほど資産を投げ売る）。</summary>
        public readonly float distressScale;
        /// <summary>流動性逼迫→投げ売り圧力への上乗せ（現金が足りないほど叩き売る）。</summary>
        public readonly float liquidityWeight;
        /// <summary>投げ売り→物価下落への変換係数（自己強化ループの強度）。</summary>
        public readonly float spiralGain;
        /// <summary>市場が完全に薄い(depth=0)とき投げ売りが物価に効く倍率（薄いほど効く）。</summary>
        public readonly float thinMarketImpact;
        /// <summary>悪循環突入とみなす投げ売り圧力の既定閾値。</summary>
        public readonly float spiralThreshold;

        public DebtDeflationParams(float distressScale, float liquidityWeight, float spiralGain, float thinMarketImpact, float spiralThreshold)
        {
            this.distressScale = Mathf.Max(0f, distressScale);
            this.liquidityWeight = Mathf.Max(0f, liquidityWeight);
            this.spiralGain = Mathf.Max(0f, spiralGain);
            this.thinMarketImpact = Mathf.Max(0f, thinMarketImpact);
            this.spiralThreshold = Mathf.Clamp01(spiralThreshold);
        }

        /// <summary>既定＝投げ売りスケール0.6・流動性重み0.4・スパイラル強度0.5・薄市場倍率1.5・突入閾値0.5。</summary>
        public static DebtDeflationParams Default => new DebtDeflationParams(0.6f, 0.4f, 0.5f, 1.5f, 0.5f);
    }

    /// <summary>
    /// フィッシャーの負債デフレーションの純ロジック（KNDB-4 #1619・キンドルバーガー『熱狂、恐慌、崩壊』参考）。
    /// 名目債務は固定でも物価が下がると実質負担が膨らみ、返済のための資産投げ売りが物価をさらに下げる＝
    /// 「借金返済の努力が借金を重くする」逆説の自己強化ループ（物価↓→実質債務↑→投げ売り→物価↓…）。
    /// <see cref="InflationRules"/>（通貨増発による物価上昇＝デフレの裏面）・<see cref="FiscalRules"/>
    /// （国債・金利・財政の債務スパイラル）・<see cref="DebtDiplomacyRules"/>（債務を外交カードに使う側）とは別系統で、
    /// こちらはデフレ下の実質債務スパイラルそのものを扱う。乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class DebtDeflationRules
    {
        /// <summary>物価水準の下限（デフレでもこれ未満には下がらない＝0割回避）。</summary>
        public const float MinPriceLevel = 0.1f;
        /// <summary>名目債務の下限（0割回避用の微小値）。</summary>
        public const float MinDebt = 0.0001f;

        /// <summary>
        /// 実質債務負担＝名目債務÷物価水準。物価が下がるほど（priceLevel が小さいほど）実質負担が膨らむ＝
        /// 名目は1円も増えていないのに返すべき重みだけが増す、負債デフレの核。物価1.0で名目と一致。
        /// </summary>
        public static float RealDebtBurden(float nominalDebt, float priceLevel)
        {
            float debt = Mathf.Max(0f, nominalDebt);
            float level = Mathf.Max(MinPriceLevel, priceLevel);
            return debt / level;
        }

        /// <summary>
        /// 物価下落率(0..1)に対する実質債務の膨張率＝1/(1−下落率)−1。
        /// 例：物価が20%下がる(0.2)と実質債務は0.25(=25%)増える＝下落が大きいほど非線形に膨らむ。
        /// </summary>
        public static float RealDebtChange(float priceDeflation)
        {
            // 完全下落(1.0)は無限大なので0.99で頭打ち（決定論・発散回避）。
            float d = Mathf.Clamp(priceDeflation, 0f, 0.99f);
            return 1f / (1f - d) - 1f;
        }

        /// <summary>
        /// 投げ売り圧力(0..1)＝（実質負担の重さ＋流動性逼迫）を投げ売りスケールで合成。
        /// 実質負担が重く、かつ手元の現金が足りない(liquidityStress 大)ほど、返済のため資産を叩き売る。
        /// 実質負担は1.0を基準(=名目どおり)に超過分のみを効かせる（物価が下がっていなければ投げ売り圧は弱い）。
        /// </summary>
        public static float DistressSelling(float realBurden, float liquidityStress, DebtDeflationParams p)
        {
            float excess = Mathf.Max(0f, realBurden - 1f);       // 実質負担が名目を超えた分
            float stress = Mathf.Clamp01(liquidityStress);
            float pressure = excess * p.distressScale + stress * p.liquidityWeight;
            return Mathf.Clamp01(pressure);
        }

        public static float DistressSelling(float realBurden, float liquidityStress)
            => DistressSelling(realBurden, liquidityStress, DebtDeflationParams.Default);

        /// <summary>
        /// 投げ売りが物価をさらに下げる率(0..1)＝投げ売り圧力×(薄市場倍率で増幅、market が厚いほど薄まる)。
        /// marketDepth=1（厚い）なら影響は素通り、=0（薄い）なら thinMarketImpact 倍に増幅＝買い手のいない市場ほど叩き落ちる。
        /// これが <see cref="RealDebtBurden"/> への入力（次の物価）を押し下げ、ループを閉じる。
        /// </summary>
        public static float PriceImpactOfSelling(float distressSelling, float marketDepth, DebtDeflationParams p)
        {
            float selling = Mathf.Clamp01(distressSelling);
            float depth = Mathf.Clamp01(marketDepth);
            // 薄い(depth→0)ほど thinMarketImpact 倍、厚い(depth→1)ほど等倍。
            float amplify = Mathf.Lerp(p.thinMarketImpact, 1f, depth);
            return Mathf.Clamp01(selling * amplify);
        }

        public static float PriceImpactOfSelling(float distressSelling, float marketDepth)
            => PriceImpactOfSelling(distressSelling, marketDepth, DebtDeflationParams.Default);

        /// <summary>
        /// 物価→実質債務→投げ売り→物価下落 の1ステップ自己強化（spiralGain で強度）。
        /// 現在の物価水準と債務負荷(0..1＝実質負担の重さ・流動性逼迫の代理)から投げ売りを起こし、
        /// その物価押し下げぶんだけ物価水準を下げた次値を返す＝「返そうとするほど物価が下がり、実質債務が膨らむ」逆説の1tick。
        /// 物価は <see cref="MinPriceLevel"/> でクランプ（無限デフレ回避＝決定論）。
        /// </summary>
        public static float DeflationSpiralTick(float priceLevel, float debtLoad, float dt, DebtDeflationParams p)
        {
            float level = Mathf.Max(MinPriceLevel, priceLevel);
            float load = Mathf.Clamp01(debtLoad);
            float step = Mathf.Max(0f, dt);
            // 債務負荷が重いほど投げ売りが起き、物価を spiralGain の強さで押し下げる。
            float priceDrop = load * p.spiralGain * step;
            float next = level * (1f - Mathf.Clamp01(priceDrop));
            return Mathf.Max(MinPriceLevel, next);
        }

        public static float DeflationSpiralTick(float priceLevel, float debtLoad, float dt)
            => DeflationSpiralTick(priceLevel, debtLoad, dt, DebtDeflationParams.Default);

        /// <summary>
        /// 悪循環（負債デフレ）突入か＝物価が基準(1.0)を割り、かつ債務負荷が閾値以上。
        /// 物価が下がっている(level&lt;1)局面でのみ、重い債務がスパイラルの引き金になる。
        /// </summary>
        public static bool IsDebtDeflation(float priceLevel, float debtLoad, float threshold)
        {
            float level = Mathf.Max(MinPriceLevel, priceLevel);
            return level < 1f && Mathf.Clamp01(debtLoad) >= Mathf.Clamp01(threshold);
        }

        public static bool IsDebtDeflation(float priceLevel, float debtLoad, DebtDeflationParams p)
            => IsDebtDeflation(priceLevel, debtLoad, p.spiralThreshold);

        public static bool IsDebtDeflation(float priceLevel, float debtLoad)
            => IsDebtDeflation(priceLevel, debtLoad, DebtDeflationParams.Default);

        /// <summary>
        /// 債務減免(0..1)で循環を断つ＝減免後の実質負担＝実質負担×(1−減免率)。
        /// 物価でなく名目債務そのものを軽くするのでループの起点を直接弱める＝投げ売り誘因を断つ唯一の出口
        /// （フィッシャーの処方＝リフレか債務減免）。
        /// </summary>
        public static float DebtRelief(float forgiveness, float realBurden)
        {
            float f = Mathf.Clamp01(forgiveness);
            float burden = Mathf.Max(0f, realBurden);
            return burden * (1f - f);
        }

        /// <summary>
        /// 累積の深刻度(0..1)＝スパイラルが iterations 回まわった重さ。1回ごとに債務負荷ぶん深まる飽和カーブ
        /// （1−(1−load)^iterations）＝同じ債務負荷でも反復が重なるほど抜け出しにくくなる。
        /// </summary>
        public static float SpiralSeverity(int iterations, float debtLoad)
        {
            int n = Mathf.Max(0, iterations);
            float load = Mathf.Clamp01(debtLoad);
            float survive = 1f;
            for (int i = 0; i < n; i++)
            {
                survive *= (1f - load);
            }
            return Mathf.Clamp01(1f - survive);
        }
    }
}
