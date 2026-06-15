using UnityEngine;

namespace Ginei
{
    /// <summary>農奴制と解放の調整係数。</summary>
    public readonly struct SerfdomParams
    {
        /// <summary>身分制労働の産出水準（安定だが低い＝意欲なき労働の上限）。</summary>
        public readonly float serfProductivity;
        /// <summary>自由労働の長期産出上限（解放が最終的に届く水準）。</summary>
        public readonly float freeProductivityCap;
        /// <summary>解放直後の落ち込み深さ（0..1＝農奴産出に対する低下率。J字カーブの谷）。</summary>
        public readonly float shockDepth;
        /// <summary>解放後の産出目標が上限に達するまでの年数（J字カーブの回復期間）。</summary>
        public readonly float recoveryYears;
        /// <summary>実産出が目標曲線へ追従する遅れ（年）。</summary>
        public readonly float transitionLag;
        /// <summary>領主層の反発の強さ（補償ゼロ・全農奴解放時の最大値）。</summary>
        public readonly float backlashScale;
        /// <summary>解放直後の被解放民の忠誠ボーナス最大（解放者への恩義）。</summary>
        public readonly float loyaltyPeak;
        /// <summary>恩義が日常化して消えるまでの年数（自由が当たり前になると感謝は薄れる）。</summary>
        public readonly float loyaltyFadeYears;
        /// <summary>全農奴解放時に得る労働流動性の最大（工業化・徴兵の土台）。</summary>
        public readonly float mobilityGain;

        public SerfdomParams(float serfProductivity, float freeProductivityCap, float shockDepth,
                             float recoveryYears, float transitionLag, float backlashScale,
                             float loyaltyPeak, float loyaltyFadeYears, float mobilityGain)
        {
            this.serfProductivity = Mathf.Max(0f, serfProductivity);
            this.freeProductivityCap = Mathf.Max(this.serfProductivity, freeProductivityCap); // 上限は農奴水準以上
            this.shockDepth = Mathf.Clamp01(shockDepth);
            this.recoveryYears = Mathf.Max(0f, recoveryYears);
            this.transitionLag = Mathf.Max(0f, transitionLag);
            this.backlashScale = Mathf.Max(0f, backlashScale);
            this.loyaltyPeak = Mathf.Max(0f, loyaltyPeak);
            this.loyaltyFadeYears = Mathf.Max(0f, loyaltyFadeYears);
            this.mobilityGain = Mathf.Max(0f, mobilityGain);
        }

        /// <summary>既定＝農奴産出0.6・自由上限1.2・ショック深さ0.5・回復10年・追従遅れ2年・
        /// 反発0.8・恩義ピーク0.5・恩義風化30年・流動性0.4。</summary>
        public static SerfdomParams Default => new SerfdomParams(0.6f, 1.2f, 0.5f, 10f, 2f, 0.8f, 0.5f, 30f, 0.4f);
    }

    /// <summary>
    /// 農奴制と解放の純ロジック（帝国の農奴解放＝ラインハルトの改革）。身分制労働の産出は安定だが
    /// 上限が低く、解放は短期の混乱（産出低下・領主反発）と引き換えに長期の産出・労働流動性・
    /// 被解放民の忠誠を得る＝J字カーブ。「解放は短期の損と長期の得の交換＝改革者は果実を見ずに種を蒔く」。
    /// 税の階級別負担（カネの再分配）は <see cref="RedistributionRules"/> が扱い、ここは身分そのものの
    /// 再編＝労働の質と忠誠の時間動態のみを扱う。倍率・係数は基準値に掛けて使う（実効値パターン・
    /// 基準非破壊）。乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class SerfdomRules
    {
        /// <summary>
        /// 身分制労働の産出。時間が経っても変わらない＝安定だが上限が低い（意欲のない労働は伸びない）。
        /// </summary>
        public static float SerfProductivity(SerfdomParams p) => p.serfProductivity;

        public static float SerfProductivity() => SerfProductivity(SerfdomParams.Default);

        /// <summary>
        /// 解放後の産出の目標曲線（J字カーブの背骨）。解放直後はショックの谷
        /// （農奴産出×(1−shockDepth)）から始まり、recoveryYears かけて自由労働の上限へ線形に伸びる。
        /// </summary>
        public static float EmancipationTarget(float yearsSinceEmancipation, SerfdomParams p)
        {
            float floor = p.serfProductivity * (1f - p.shockDepth);
            float t = p.recoveryYears <= 0f ? 1f
                : Mathf.Clamp01(Mathf.Max(0f, yearsSinceEmancipation) / p.recoveryYears);
            return Mathf.Lerp(floor, p.freeProductivityCap, t);
        }

        public static float EmancipationTarget(float yearsSinceEmancipation)
            => EmancipationTarget(yearsSinceEmancipation, SerfdomParams.Default);

        /// <summary>
        /// 解放後の実産出の1tick後の値。目標曲線（<see cref="EmancipationTarget"/>）へ
        /// 追従遅れ transitionLag で収束する＝短期は混乱で農奴制を下回り、数年で追い越し、
        /// 長期は大きく上回る（J字カーブ）。dt は年単位。
        /// </summary>
        public static float FreeLaborProductivityTick(float productivity, float yearsSinceEmancipation,
                                                      float dt, SerfdomParams p)
        {
            float current = Mathf.Clamp(productivity, 0f, p.freeProductivityCap);
            float target = EmancipationTarget(yearsSinceEmancipation, p);
            if (p.transitionLag <= 0f) return target; // 遅れなし＝即時追従
            float rate = Mathf.Clamp01(Mathf.Max(0f, dt) / p.transitionLag);
            return Mathf.Lerp(current, target, rate);
        }

        public static float FreeLaborProductivityTick(float productivity, float yearsSinceEmancipation, float dt)
            => FreeLaborProductivityTick(productivity, yearsSinceEmancipation, dt, SerfdomParams.Default);

        /// <summary>
        /// 解放後の産出目標が農奴制の水準を追い越すまでの年数＝果実が見えるまでの待ち時間
        /// （改革者は果実を見ずに種を蒔く）。即時に上回るなら0。
        /// </summary>
        public static float CrossoverYears(SerfdomParams p)
        {
            float floor = p.serfProductivity * (1f - p.shockDepth);
            float span = p.freeProductivityCap - floor;
            if (span <= 0f) return 0f; // 谷がない＝最初から上回る
            return p.recoveryYears * Mathf.Clamp01((p.serfProductivity - floor) / span);
        }

        public static float CrossoverYears() => CrossoverYears(SerfdomParams.Default);

        /// <summary>
        /// 解放直後の社会的混乱の大きさ（0..shockDepth）。身分制が深い社会
        /// （serfShare＝人口に占める農奴の割合 0..1）ほどショックは大きい。安定度低下などの係数に使う。
        /// </summary>
        public static float EmancipationShock(float serfShare, SerfdomParams p)
        {
            return p.shockDepth * Mathf.Clamp01(serfShare);
        }

        public static float EmancipationShock(float serfShare)
            => EmancipationShock(serfShare, SerfdomParams.Default);

        /// <summary>
        /// 領主層の反発の強さ（0..1）。失う農奴が多いほど強く、補償（compensation 0..1）で和らぐ
        /// ＝カネで貴族の牙を抜く。反乱圧力・宮廷の抵抗の係数に使う。
        /// </summary>
        public static float LandlordBacklash(float serfShare, float compensation, SerfdomParams p)
        {
            float share = Mathf.Clamp01(serfShare);
            float comp = Mathf.Clamp01(compensation);
            return Mathf.Clamp01(p.backlashScale * share * (1f - comp));
        }

        public static float LandlordBacklash(float serfShare, float compensation)
            => LandlordBacklash(serfShare, compensation, SerfdomParams.Default);

        /// <summary>
        /// 解放された者の解放者への忠誠ボーナス（0..loyaltyPeak）。解放直後が最大で、
        /// 自由が日常化するにつれ loyaltyFadeYears かけて線形に薄れる
        /// （ラインハルトが民衆に愛された理由＝ただし恩義は永続しない）。
        /// </summary>
        public static float FreedomLoyalty(float yearsSinceEmancipation, SerfdomParams p)
        {
            float years = Mathf.Max(0f, yearsSinceEmancipation);
            if (p.loyaltyFadeYears <= 0f) return years <= 0f ? p.loyaltyPeak : 0f;
            return p.loyaltyPeak * (1f - Mathf.Clamp01(years / p.loyaltyFadeYears));
        }

        public static float FreedomLoyalty(float yearsSinceEmancipation)
            => FreedomLoyalty(yearsSinceEmancipation, SerfdomParams.Default);

        /// <summary>
        /// 解放で得る労働流動性（0..mobilityGain）。解放した農奴が多いほど大きい。
        /// 工業化・大規模徴兵の土台となる係数（身分に縛られた労働は動員できない）。
        /// </summary>
        public static float MobilityGain(float serfShare, SerfdomParams p)
        {
            return p.mobilityGain * Mathf.Clamp01(serfShare);
        }

        public static float MobilityGain(float serfShare)
            => MobilityGain(serfShare, SerfdomParams.Default);
    }
}
