using UnityEngine;

namespace Ginei
{
    /// <summary>テロの調整係数（地球教型）。</summary>
    public readonly struct TerrorParams
    {
        /// <summary>規模1の攻撃の直接被害（人口・安定度に対する割合＝実は小さい）。</summary>
        public readonly float directDamageRatio;
        /// <summary>恐怖の増幅率（直接被害の何倍の心理効果が広がるか・媒体到達1のとき）。</summary>
        public readonly float fearAmplification;
        /// <summary>過剰な弾圧が市民の支持を削る係数（テロの真の武器＝報復の自滅）。</summary>
        public readonly float overreactionCost;
        /// <summary>弾圧と不満が新たな細胞を生む係数（自己増殖の燃料）。</summary>
        public readonly float radicalizationRate;
        /// <summary>対テロ作戦が細胞を削る速度（per dt・努力1のとき）。</summary>
        public readonly float counterTerrorRate;

        public TerrorParams(float directDamageRatio, float fearAmplification, float overreactionCost,
                            float radicalizationRate, float counterTerrorRate)
        {
            this.directDamageRatio = Mathf.Clamp01(directDamageRatio);
            this.fearAmplification = Mathf.Max(0f, fearAmplification);
            this.overreactionCost = Mathf.Max(0f, overreactionCost);
            this.radicalizationRate = Mathf.Max(0f, radicalizationRate);
            this.counterTerrorRate = Mathf.Max(0f, counterTerrorRate);
        }

        /// <summary>既定＝直接被害0.01・恐怖増幅10倍・過剰反応コスト0.3・急進化0.05・対テロ0.1。</summary>
        public static TerrorParams Default => new TerrorParams(0.01f, 10f, 0.3f, 0.05f, 0.1f);
    }

    /// <summary>
    /// テロの純ロジック（地球教型＝無差別と恐怖）。攻撃の直接被害は小さいが、恐怖は媒体を通じて
    /// 被害の何倍にも増幅される。そして本当の狙いは**報復の自滅**＝政権が過剰な弾圧で応えるほど
    /// 市民の支持を失い、弾圧の不満が新たな細胞を生む（自己増殖）。冷静な対処（精密な対テロ＋
    /// 過剰反応の抑制）だけが螺旋を断つ。要人暗殺（<see cref="AssassinationRules"/>）とは別系統。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class TerrorRules
    {
        /// <summary>攻撃の直接被害率（0..directDamageRatio）＝規模 scale(0..1) に比例。人口・安定度に掛ける。</summary>
        public static float DirectDamage(float scale, TerrorParams p)
        {
            return Mathf.Clamp01(scale) * p.directDamageRatio;
        }

        public static float DirectDamage(float scale) => DirectDamage(scale, TerrorParams.Default);

        /// <summary>
        /// 恐怖の広がり（0..1）＝直接被害×増幅率×媒体到達 mediaReach(0..1)。被害より恐怖がはるかに大きい
        /// ＝テロは劇場（報じられなければ恐怖は半減）。
        /// </summary>
        public static float FearSpread(float scale, float mediaReach, TerrorParams p)
        {
            return Mathf.Clamp01(DirectDamage(scale, p) * p.fearAmplification * Mathf.Clamp01(mediaReach));
        }

        public static float FearSpread(float scale, float mediaReach) => FearSpread(scale, mediaReach, TerrorParams.Default);

        /// <summary>
        /// 過剰反応の支持コスト（0..overreactionCost）＝弾圧強度のうち脅威に見合わない超過分
        /// （crackdown − 実際の脅威 threat）×係数。脅威の範囲内の対処はコストなし。
        /// </summary>
        public static float OverreactionCost(float crackdown, float threat, TerrorParams p)
        {
            float excess = Mathf.Max(0f, Mathf.Clamp01(crackdown) - Mathf.Clamp01(threat));
            return excess * p.overreactionCost;
        }

        public static float OverreactionCost(float crackdown, float threat)
            => OverreactionCost(crackdown, threat, TerrorParams.Default);

        /// <summary>
        /// テロ細胞の1tick後の規模（0..1）。弾圧×不満 grievance が新たな細胞を生み（急進化）、
        /// 対テロ努力 counterEffort が削る＝雑な弾圧は対テロを上回って細胞を増やしうる。
        /// </summary>
        public static float CellsTick(float cells, float crackdown, float grievance, float counterEffort, float dt, TerrorParams p)
        {
            float d = Mathf.Max(0f, dt);
            float radicalization = p.radicalizationRate * Mathf.Clamp01(crackdown) * Mathf.Clamp01(grievance) * d;
            float suppression = p.counterTerrorRate * Mathf.Clamp01(counterEffort) * Mathf.Clamp01(cells) * d;
            return Mathf.Clamp01(Mathf.Clamp01(cells) + radicalization - suppression);
        }

        public static float CellsTick(float cells, float crackdown, float grievance, float counterEffort, float dt)
            => CellsTick(cells, crackdown, grievance, counterEffort, dt, TerrorParams.Default);

        /// <summary>
        /// 報復の自滅が進行中か＝過剰反応コストが発生し、かつ細胞が増勢（急進化＞鎮圧）。
        /// テロリストの思う壺の状態＝戦略の転換を促す警告。
        /// </summary>
        public static bool IsSelfDefeating(float crackdown, float threat, float grievance, float counterEffort, TerrorParams p)
        {
            bool overreacting = OverreactionCost(crackdown, threat, p) > 0f;
            float radicalization = p.radicalizationRate * Mathf.Clamp01(crackdown) * Mathf.Clamp01(grievance);
            float suppression = p.counterTerrorRate * Mathf.Clamp01(counterEffort);
            return overreacting && radicalization > suppression;
        }

        public static bool IsSelfDefeating(float crackdown, float threat, float grievance, float counterEffort)
            => IsSelfDefeating(crackdown, threat, grievance, counterEffort, TerrorParams.Default);
    }
}
