using UnityEngine;

namespace Ginei
{
    /// <summary>斜行陣（片翼拒否＝refused flank）の調整係数。</summary>
    public readonly struct RefusedFlankParams
    {
        /// <summary>集中度1のときに集中翼へ振り分ける戦力比の上限（0.5＝均等〜massBias＝偏重）。</summary>
        public readonly float massBias;
        /// <summary>退げた翼が稼ぐ遅延のスケール（撤退度→接触猶予の倍率）。</summary>
        public readonly float delayScale;
        /// <summary>集中翼の打撃を局所戦力比から出す指数（<1＝集中の逓減・ランチェスター風）。</summary>
        public readonly float impactExponent;
        /// <summary>退げた翼の脆弱性スケール（薄いほど崩されやすい度合いの上限）。</summary>
        public readonly float vulnerabilityScale;
        /// <summary>一角崩壊から戦線を巻き取る効果のスケール（敵結束が低いほど効く）。</summary>
        public readonly float rollUpScale;

        public RefusedFlankParams(float massBias, float delayScale, float impactExponent,
                                  float vulnerabilityScale, float rollUpScale)
        {
            this.massBias = Mathf.Clamp(massBias, 0.5f, 1f);
            this.delayScale = Mathf.Max(0f, delayScale);
            this.impactExponent = Mathf.Clamp(impactExponent, 0.1f, 2f);
            this.vulnerabilityScale = Mathf.Clamp01(vulnerabilityScale);
            this.rollUpScale = Mathf.Clamp01(rollUpScale);
        }

        /// <summary>既定＝集中上限0.65・遅延1.2・打撃指数0.5・脆弱性0.8・巻取り0.7。</summary>
        public static RefusedFlankParams Default => new RefusedFlankParams(0.65f, 1.2f, 0.5f, 0.8f, 0.7f);
    }

    /// <summary>
    /// 斜行陣＝片翼拒否（refused flank・エパミノンダス/フリードリヒ型）の純ロジック。
    /// 全戦線で均等に当たるのでなく、**一翼へ戦力を集中して決定的打撃を与え、もう一翼は接触を避けて退げる**。
    /// 集中翼が敵の一角を撃破する前に、退げた翼が崩されないかが勝負＝撃破が先（<see cref="EchelonTiming"/>）なら
    /// 一角崩壊から戦線を端から巻き取る（<see cref="RollUpEffect"/>）。退げた翼が薄すぎて先に崩れれば失敗。
    /// 陣形の戦術特性（攻撃/防御/機動の倍率）を扱う <see cref="FormationTraitRules"/> とは別＝
    /// 「戦力をどう非対称に配分し時間差で当てるか」という**運用**の純ロジック。盤面非依存・plain 引数。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class RefusedFlankRules
    {
        /// <summary>
        /// 集中翼に振り分けた戦力＝総戦力 × Lerp(0.5, massBias, 集中度)。集中度0で均等(0.5)、1で偏重(massBias)。
        /// 残りが退げた翼の戦力（total − massed）になる。
        /// </summary>
        public static float MassedWingStrength(float totalStrength, float massConcentration, RefusedFlankParams p)
        {
            float total = Mathf.Max(0f, totalStrength);
            float c = Mathf.Clamp01(massConcentration);
            return total * Mathf.Lerp(0.5f, p.massBias, c);
        }

        public static float MassedWingStrength(float totalStrength, float massConcentration)
            => MassedWingStrength(totalStrength, massConcentration, RefusedFlankParams.Default);

        /// <summary>
        /// 退げた翼が接触を遅らせる時間＝撤退度(0..1) × delayScale ÷ (1+敵の前進速度)。
        /// 大きく退げ、敵の前進が遅いほど猶予が増える＝集中翼の決着までの時間を稼ぐ。
        /// </summary>
        public static float RefusedWingDelay(float refusedWingWithdrawal, float enemyAdvance, RefusedFlankParams p)
        {
            float w = Mathf.Clamp01(refusedWingWithdrawal);
            float adv = Mathf.Max(0f, enemyAdvance);
            return w * p.delayScale / (1f + adv);
        }

        public static float RefusedWingDelay(float refusedWingWithdrawal, float enemyAdvance)
            => RefusedWingDelay(refusedWingWithdrawal, enemyAdvance, RefusedFlankParams.Default);

        /// <summary>
        /// 集中翼が敵一角を破る打撃＝pow(集中翼戦力 ÷ (敵翼戦力+1), impactExponent)。局所優勢が打撃に直結。
        /// 指数<1なので集中の効きは逓減（過集中の無駄を表す）。
        /// </summary>
        public static float DecisiveWingImpact(float massedWingStrength, float enemyWingStrength, RefusedFlankParams p)
        {
            float massed = Mathf.Max(0f, massedWingStrength);
            float enemy = Mathf.Max(0f, enemyWingStrength);
            return Mathf.Pow(massed / (enemy + 1f), p.impactExponent);
        }

        public static float DecisiveWingImpact(float massedWingStrength, float enemyWingStrength)
            => DecisiveWingImpact(massedWingStrength, enemyWingStrength, RefusedFlankParams.Default);

        /// <summary>
        /// 集中翼の撃破が退げた翼の崩壊より先に来るかの度合い＝打撃 × (1+遅延)。
        /// 退げた翼が稼いだ遅延だけ、集中翼の決着が相対的に早まる（時間差攻撃の核）。
        /// </summary>
        public static float EchelonTiming(float decisiveWingImpact, float refusedWingDelay)
        {
            float impact = Mathf.Max(0f, decisiveWingImpact);
            float delay = Mathf.Max(0f, refusedWingDelay);
            return impact * (1f + delay);
        }

        /// <summary>
        /// 退げた翼が薄くて崩される危険(0..1)＝敵圧力 × vulnerabilityScale ÷ (退げた翼戦力+1)。
        /// 戦力を集中翼へ吸われて薄い翼ほど、敵の圧力で先に崩れる危険が高い。
        /// </summary>
        public static float RefusedWingVulnerability(float refusedWingStrength, float enemyPressure, RefusedFlankParams p)
        {
            float refused = Mathf.Max(0f, refusedWingStrength);
            float pressure = Mathf.Max(0f, enemyPressure);
            return Mathf.Clamp01(pressure * p.vulnerabilityScale / (refused + 1f));
        }

        public static float RefusedWingVulnerability(float refusedWingStrength, float enemyPressure)
            => RefusedWingVulnerability(refusedWingStrength, enemyPressure, RefusedFlankParams.Default);

        /// <summary>
        /// 一角崩壊から敵戦線を端から巻き取る効果＝打撃 × (1−敵戦線の結束) × rollUpScale。
        /// 敵の結束(0..1)が低いほど、破った一角から横へ崩壊が連鎖する。
        /// </summary>
        public static float RollUpEffect(float decisiveWingImpact, float enemyLineCohesion, RefusedFlankParams p)
        {
            float impact = Mathf.Max(0f, decisiveWingImpact);
            float cohesion = Mathf.Clamp01(enemyLineCohesion);
            return impact * (1f - cohesion) * p.rollUpScale;
        }

        public static float RollUpEffect(float decisiveWingImpact, float enemyLineCohesion)
            => RollUpEffect(decisiveWingImpact, enemyLineCohesion, RefusedFlankParams.Default);

        /// <summary>
        /// 斜行陣の正味の利得＝時間差の度合い × (1+巻取り効果)。撃破が先に来て、かつ崩壊が横へ広がるほど大きい。
        /// </summary>
        public static float ObliqueAdvantage(float echelonTiming, float rollUpEffect)
        {
            float timing = Mathf.Max(0f, echelonTiming);
            float roll = Mathf.Max(0f, rollUpEffect);
            return timing * (1f + roll);
        }

        /// <summary>斜行陣が決まったか＝時間差の度合いが閾値 threshold を超える（集中翼が先に決着できた）。</summary>
        public static bool IsObliqueSuccessful(float echelonTiming, float threshold)
        {
            return Mathf.Max(0f, echelonTiming) > Mathf.Max(0f, threshold);
        }
    }
}
