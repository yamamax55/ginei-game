using UnityEngine;

namespace Ginei
{
    /// <summary>賠償金の調整係数。</summary>
    public readonly struct ReparationsParams
    {
        /// <summary>賠償率最大のとき敗者経済を絞める最大割合（経済絞殺の上限）。</summary>
        public readonly float maxStrangulation;
        /// <summary>絞殺の非線形度（賠償率の冪指数・1以上）。重いほど加速度的に絞まる。</summary>
        public readonly float strangulationExponent;
        /// <summary>復讐主義が育つ速度（per dt・賠償率に掛かる）。</summary>
        public readonly float revanchismGrowthRate;
        /// <summary>軽賠償時に復讐主義が風化する速度（per dt）。</summary>
        public readonly float revanchismDecayRate;
        /// <summary>この賠償率以下なら「寛大な講和」＝復讐主義は育たず風化する閾値。</summary>
        public readonly float lightBurdenThreshold;
        /// <summary>支払不能リスクの係数（賠償率×経済不健全度に掛かる）。</summary>
        public readonly float defaultRiskScale;

        public ReparationsParams(float maxStrangulation, float strangulationExponent,
            float revanchismGrowthRate, float revanchismDecayRate,
            float lightBurdenThreshold, float defaultRiskScale)
        {
            this.maxStrangulation = Mathf.Clamp01(maxStrangulation);
            this.strangulationExponent = Mathf.Max(1f, strangulationExponent);
            this.revanchismGrowthRate = Mathf.Max(0f, revanchismGrowthRate);
            this.revanchismDecayRate = Mathf.Max(0f, revanchismDecayRate);
            this.lightBurdenThreshold = Mathf.Clamp01(lightBurdenThreshold);
            this.defaultRiskScale = Mathf.Max(0f, defaultRiskScale);
        }

        /// <summary>既定＝絞殺上限1.0・冪指数2・復讐成長0.1・風化0.02・寛大閾値0.1・支払不能係数1.5。</summary>
        public static ReparationsParams Default => new ReparationsParams(1f, 2f, 0.1f, 0.02f, 0.1f, 1.5f);
    }

    /// <summary>
    /// 賠償金の純ロジック（ヴェルサイユの罠）。過酷な賠償は敗者の経済を非線形に絞め殺し
    /// （取り分はラッファー曲線＝重くするほどかえって減る）、復讐主義を育てて次の戦争の種を播く。
    /// 寛大な講和なら復讐主義は時間で風化する。「取り立てすぎは金の卵の鶏を殺し、恨みだけが残る」。
    /// 講和条件一般（<see cref="WarGoalRules"/>＝厭戦・講和受諾の即時判定）とは別系統＝こちらは
    /// 講和後の賠償が生む長期帰結を扱う。倍率は経済係数に掛けて使う（実効値パターン・基準非破壊）。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class ReparationsRules
    {
        /// <summary>
        /// 敗者経済への絞殺度（0..maxStrangulation）＝最大幅×賠償率^冪指数。
        /// 軽い賠償はほぼ無害、重い賠償は加速度的に経済を殺す（非線形）。
        /// 敗者の成長係数には（1−これ）を掛けて使う。
        /// </summary>
        public static float EconomicStrangulation(float burden, ReparationsParams p)
        {
            return p.maxStrangulation * Mathf.Pow(Mathf.Clamp01(burden), p.strangulationExponent);
        }

        public static float EconomicStrangulation(float burden)
            => EconomicStrangulation(burden, ReparationsParams.Default);

        /// <summary>
        /// 勝者の年間取り分＝敗者経済×賠償率×（1−絞殺度）。賠償率を上げるほど徴収効率が落ちる
        /// ラッファー曲線＝全力で取り立てると鶏が死んで取り分ゼロ。
        /// </summary>
        public static float AnnualPayment(float burden, float loserEconomy, ReparationsParams p)
        {
            return Mathf.Max(0f, loserEconomy) * Mathf.Clamp01(burden) * (1f - EconomicStrangulation(burden, p));
        }

        public static float AnnualPayment(float burden, float loserEconomy)
            => AnnualPayment(burden, loserEconomy, ReparationsParams.Default);

        /// <summary>
        /// 復讐主義の1tick後の値（0..1）。寛大閾値を超える賠償は率に比例して恨みを育て
        /// （成長速度×賠償率×dt）、閾値以下なら時間で風化する（風化速度×dt でゼロへ）。
        /// </summary>
        public static float RevanchismTick(float revanchism, float burden, float dt, ReparationsParams p)
        {
            float r = Mathf.Clamp01(revanchism);
            float b = Mathf.Clamp01(burden);
            float t = Mathf.Max(0f, dt);
            if (b > p.lightBurdenThreshold)
            {
                return Mathf.Clamp01(r + p.revanchismGrowthRate * b * t);
            }
            return Mathf.MoveTowards(r, 0f, p.revanchismDecayRate * t);
        }

        public static float RevanchismTick(float revanchism, float burden, float dt)
            => RevanchismTick(revanchism, burden, dt, ReparationsParams.Default);

        /// <summary>
        /// 支払不能リスク（0..1）＝賠償率×経済不健全度(1−health)×係数。
        /// 健全な経済なら重賠償も払えるが、絞め殺した経済からは取り立てる物がなくなる。
        /// </summary>
        public static float DefaultRisk(float burden, float loserEconomyHealth, ReparationsParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(burden) * (1f - Mathf.Clamp01(loserEconomyHealth)) * p.defaultRiskScale);
        }

        public static float DefaultRisk(float burden, float loserEconomyHealth)
            => DefaultRisk(burden, loserEconomyHealth, ReparationsParams.Default);

        /// <summary>
        /// 絞め殺さず取れる賠償率の上限の目安（0..1）＝年間取り分を最大化する賠償率
        /// （ラッファー曲線の頂点を解析解で返す）。絞殺が無効（上限0）なら全部取れる＝1。
        /// </summary>
        public static float SustainableBurden(ReparationsParams p)
        {
            if (p.maxStrangulation <= 0f) return 1f;
            // d/db [b(1 − m·b^k)] = 0 → b = (1 / (m(k+1)))^(1/k)
            float peak = Mathf.Pow(1f / (p.maxStrangulation * (p.strangulationExponent + 1f)), 1f / p.strangulationExponent);
            return Mathf.Clamp01(peak);
        }

        public static float SustainableBurden()
            => SustainableBurden(ReparationsParams.Default);
    }
}
