using UnityEngine;

namespace Ginei
{
    /// <summary>戦後復興の調整係数。</summary>
    public readonly struct ReconstructionParams
    {
        /// <summary>自然回復の速度（荒廃/時間・投資ゼロでも人々が細々と再建する分。固定化後は働かない）。</summary>
        public readonly float naturalRecoveryRate;
        /// <summary>投資による回復の速度係数（投資1.0あたりの荒廃削減/時間）。</summary>
        public readonly float investRecoveryRate;
        /// <summary>荒廃の固定化までの放置時間の閾値（これを超えて放置すると人が去り回復不能）。</summary>
        public readonly float ossifyThreshold;
        /// <summary>復興需要の経済ブースト係数（投資×荒廃に掛ける）。</summary>
        public readonly float boomScale;
        /// <summary>「手を付けている」と見なす最小投資（これ未満は放置扱い＝放置時間が積み上がる）。</summary>
        public readonly float minActiveInvestment;

        public ReconstructionParams(float naturalRecoveryRate, float investRecoveryRate,
            float ossifyThreshold, float boomScale, float minActiveInvestment)
        {
            this.naturalRecoveryRate = Mathf.Max(0f, naturalRecoveryRate);
            this.investRecoveryRate = Mathf.Max(0f, investRecoveryRate);
            this.ossifyThreshold = Mathf.Max(0f, ossifyThreshold);
            this.boomScale = Mathf.Max(0f, boomScale);
            this.minActiveInvestment = Mathf.Clamp01(minActiveInvestment);
        }

        /// <summary>既定＝自然回復0.01/時間・投資回復係数0.1・固定化閾値50時間・ブースト係数0.5・最小投資0.05。</summary>
        public static ReconstructionParams Default => new ReconstructionParams(0.01f, 0.1f, 50f, 0.5f, 0.05f);
    }

    /// <summary>
    /// 戦後復興の純ロジック。荒廃地（devastation 0..1）への投資が回復を早め、復興需要が経済を押す。
    /// 放置が長引くと荒廃は固定化（人が去り回復不能）＝「早く手を付けるほど安く済む・放置は永遠に高くつく」。
    /// 新規入植（未入植星系の開拓＝<see cref="ColonizationRules"/>）とは別系統で、既存支配地の再建のみを扱う。
    /// 破壊側（焦土戦術＝ScorchedEarthRules・バックログ）と対になる再建側。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class ReconstructionRules
    {
        /// <summary>
        /// 復興の1tick後の荒廃度。自然回復＋投資×係数 のぶん荒廃を削る（0で下限クランプ）。
        /// 固定化済み（ossified）の地は回復しない＝据え置き（放置の代償は取り返せない）。
        /// </summary>
        public static float RecoveryTick(float devastation, float investment, float dt, bool ossified, ReconstructionParams p)
        {
            float dev = Mathf.Clamp01(devastation);
            if (ossified) return dev;
            float rate = p.naturalRecoveryRate + Mathf.Clamp01(investment) * p.investRecoveryRate;
            return Mathf.Clamp01(dev - rate * Mathf.Max(0f, dt));
        }

        public static float RecoveryTick(float devastation, float investment, float dt, ReconstructionParams p)
            => RecoveryTick(devastation, investment, dt, false, p);

        public static float RecoveryTick(float devastation, float investment, float dt)
            => RecoveryTick(devastation, investment, dt, false, ReconstructionParams.Default);

        /// <summary>
        /// 放置時間の1tick更新。投資が最小投資（minActiveInvestment）以上なら「手を付けた」＝放置時間リセット、
        /// 未満なら放置が dt ぶん積み上がる。固定化判定は <see cref="IsOssified"/>。
        /// </summary>
        public static float OssificationTick(float neglectDuration, float investment, float dt, ReconstructionParams p)
        {
            if (Mathf.Clamp01(investment) >= p.minActiveInvestment) return 0f;
            return Mathf.Max(0f, neglectDuration) + Mathf.Max(0f, dt);
        }

        public static float OssificationTick(float neglectDuration, float investment, float dt)
            => OssificationTick(neglectDuration, investment, dt, ReconstructionParams.Default);

        /// <summary>荒廃が固定化したか＝放置時間が閾値以上（人が去り回復不能）。</summary>
        public static bool IsOssified(float neglectDuration, ReconstructionParams p)
        {
            return Mathf.Max(0f, neglectDuration) >= p.ossifyThreshold;
        }

        public static bool IsOssified(float neglectDuration) => IsOssified(neglectDuration, ReconstructionParams.Default);

        /// <summary>
        /// 復興需要の経済ブースト倍率（1.0＝平常）。1 + boomScale×投資×荒廃＝荒廃が深いほど
        /// 同じ投資の限界効用が大きい（復興需要が経済を押す）。荒廃ゼロ・投資ゼロでは1.0。
        /// </summary>
        public static float ReconstructionBoom(float investment, float devastation, ReconstructionParams p)
        {
            return 1f + p.boomScale * Mathf.Clamp01(investment) * Mathf.Clamp01(devastation);
        }

        public static float ReconstructionBoom(float investment, float devastation)
            => ReconstructionBoom(investment, devastation, ReconstructionParams.Default);

        /// <summary>荒廃地の産出倍率＝1−荒廃（全壊で0・無傷で1）。</summary>
        public static float OutputFactor(float devastation)
        {
            return 1f - Mathf.Clamp01(devastation);
        }

        /// <summary>
        /// 完全回復までの所要時間。荒廃／（自然回復＋投資×係数）＝荒廃が浅いうち（早く手を付けるほど）短く済む。
        /// 回復速度0（params で自然回復0かつ投資0）または固定化済みは無限大＝放置の固定化は永遠に取り返せない。
        /// </summary>
        public static float TimeToRecover(float devastation, float investment, bool ossified, ReconstructionParams p)
        {
            float dev = Mathf.Clamp01(devastation);
            if (dev <= 0f) return 0f;
            if (ossified) return float.PositiveInfinity;
            float rate = p.naturalRecoveryRate + Mathf.Clamp01(investment) * p.investRecoveryRate;
            if (rate <= 0f) return float.PositiveInfinity;
            return dev / rate;
        }

        public static float TimeToRecover(float devastation, float investment, ReconstructionParams p)
            => TimeToRecover(devastation, investment, false, p);

        public static float TimeToRecover(float devastation, float investment)
            => TimeToRecover(devastation, investment, false, ReconstructionParams.Default);
    }
}
