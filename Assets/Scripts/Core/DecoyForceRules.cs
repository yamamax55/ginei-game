using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 囮部隊・陽動（デコイ／フェイント）の調整値（#陽動）＝偽の主攻・囮の艦隊で敵の注意・予備・戦力を別方向へ引きつけ、本当の主攻を手薄なところへ通すための係数。
    /// 囮が本物らしく見える度合いの行動寄与・慎重な敵ほど食いつく食いつき率・囮の被る損害・見破りやすさ・見破られたときの意図露見、すべて ctor で安全側へクランプ。
    /// </summary>
    public readonly struct DecoyForceParams
    {
        /// <summary>囮の説得力に占める「行動の派手さ（積極的な動き）」の寄与（0..1。1.0＝兵力規模を無視して行動だけで本物らしさを決める）。</summary>
        public readonly float behaviorWeight;
        /// <summary>敵の慎重さが囮への食いつき（割く戦力）をどれだけ増幅するか（慎重な敵ほど囮の脅威を過大視して戦力を割く）。</summary>
        public readonly float cautionBiteScale;
        /// <summary>囮が敵の反応戦力から受ける損害の係数（囮はあえて晒すので損害を受ける）。</summary>
        public readonly float attritionScale;
        /// <summary>囮だと見破られるリスクの係数（敵情報力と説得力の薄さに比例）。</summary>
        public readonly float detectionScale;
        /// <summary>見破られたときに本隊（真の主攻）の意図が露見する係数。</summary>
        public readonly float backfireScale;

        public DecoyForceParams(float behaviorWeight, float cautionBiteScale,
            float attritionScale, float detectionScale, float backfireScale)
        {
            this.behaviorWeight = Mathf.Clamp01(behaviorWeight);
            this.cautionBiteScale = Mathf.Clamp(cautionBiteScale, 0f, 10f);
            this.attritionScale = Mathf.Clamp(attritionScale, 0f, 10f);
            this.detectionScale = Mathf.Clamp(detectionScale, 0f, 10f);
            this.backfireScale = Mathf.Clamp(backfireScale, 0f, 10f);
        }

        /// <summary>既定：行動寄与0.5・食いつき増幅1.0・囮損害0.5・見破り1.0・露見1.0。</summary>
        public static DecoyForceParams Default => new DecoyForceParams(
            DefaultBehaviorWeight, DefaultCautionBiteScale,
            DefaultAttritionScale, DefaultDetectionScale, DefaultBackfireScale);

        public const float DefaultBehaviorWeight = 0.5f;
        public const float DefaultCautionBiteScale = 1.0f;
        public const float DefaultAttritionScale = 0.5f;
        public const float DefaultDetectionScale = 1.0f;
        public const float DefaultBackfireScale = 1.0f;
    }

    /// <summary>
    /// 囮部隊・陽動（デコイ／フェイント）の純ロジック（#陽動・唯一の窓口）＝<b>偽の主攻・囮の艦隊を見せて敵の注意・予備・戦力を別方向へ引きつけ、本当の主攻を手薄なところへ通す</b>。
    /// 囮が本物の脅威に見えるほど（<see cref="Plausibility"/>）、慎重な敵ほど戦力を割き（<see cref="EnemyForcesDrawn"/>）、本当の主攻正面が手薄になる（<see cref="MainEffortRelief"/>）。
    /// 囮はあえて晒すので損害を受け（<see cref="DecoyAttrition"/>）、説得力が薄く敵情報力が高いと見破られ（<see cref="Detection"/>）、見破られると逆に本隊の意図が露見する（<see cref="BackfireOnDetection"/>）。
    /// 手薄化の利得から囮損害と露見の代償を引いて陽動の正味価値を見て（<see cref="DecoyNetValue"/>）、敵が十分に食いついたか＝陽動が成功したかを判定する（<see cref="IsDeceptionSuccessful"/>）。
    /// <b>分担</b>：偽装退却（<see cref="FeignedRetreatRules"/>＝引きずり出して罠へ誘い込む退却の演技）とは別＝こちらは<b>偽の主攻による陽動で敵の戦力を別方向へ引きつける</b>。
    /// 戦術的奇襲（<see cref="TacticalSurpriseRules"/>＝予期せぬ一撃で混乱させる欺瞞）とも別＝こちらは<b>敵の注意・戦力をそらす持続的な陽動</b>。
    /// 盤面非依存の plain 引数・実効値パターン（基準値非破壊）・各メソッドに Params 明示版＋Default 委譲版・test-first。
    /// </summary>
    public static class DecoyForceRules
    {
        /// <summary>ゼロ割回避の微小値。</summary>
        public const float Epsilon = 0.0001f;

        /// <summary>説得力を正規化するときの基準兵力（この兵力で兵力寄与が満点）。</summary>
        public const float StrengthReference = 100f;

        /// <summary>慎重さが最低のときの食いつき率（慎重でない敵でもこの割合は食いつく）。</summary>
        public const float MinBite = 0.5f;

        /// <summary>正味価値で露見（0..1）を兵力換算する重み（露見1.0＝この兵力ぶんの損として差し引く）。</summary>
        public const float BackfirePenalty = 100f;

        // ── 囮の説得力 ────────────────────────────────────────────

        /// <summary>囮が本物の脅威に見える度合い（0..1）＝兵力規模の見栄えと行動の派手さを混ぜる。</summary>
        public static float Plausibility(float decoyStrength, float decoyBehavior)
            => Plausibility(decoyStrength, decoyBehavior, DecoyForceParams.Default);

        /// <summary>説得力（0..1）＝lerp(兵力見栄え, 行動派手さ, behaviorWeight)。兵力見栄え＝clamp01(max(0,兵力)÷基準兵力)、行動＝clamp01(decoyBehavior)。</summary>
        public static float Plausibility(float decoyStrength, float decoyBehavior, DecoyForceParams p)
        {
            float strengthLook = Mathf.Clamp01(Mathf.Max(0f, decoyStrength) / StrengthReference);
            float behaviorLook = Mathf.Clamp01(decoyBehavior);
            return Mathf.Clamp01(Mathf.Lerp(strengthLook, behaviorLook, p.behaviorWeight));
        }

        // ── 敵が囮に割く戦力 ──────────────────────────────────────

        /// <summary>敵が囮に割く戦力の割合（0..1）＝説得力×食いつき率。慎重な敵ほど食いつく。</summary>
        public static float EnemyForcesDrawn(float plausibility, float enemyCaution)
            => EnemyForcesDrawn(plausibility, enemyCaution, DecoyForceParams.Default);

        /// <summary>食いつき割合（0..1）＝clamp01(clamp01(説得力)×食いつき率×cautionBiteScale)。食いつき率＝lerp(MinBite,1, clamp01(慎重さ))。</summary>
        public static float EnemyForcesDrawn(float plausibility, float enemyCaution, DecoyForceParams p)
        {
            float plaus = Mathf.Clamp01(plausibility);
            float bite = Mathf.Lerp(MinBite, 1f, Mathf.Clamp01(enemyCaution));
            return Mathf.Clamp01(plaus * bite * p.cautionBiteScale);
        }

        // ── 主攻正面の手薄化 ──────────────────────────────────────

        /// <summary>本当の主攻正面が手薄になる度合い（絶対戦力）＝囮へ割いた割合×敵総兵力（別方向へ釣り出された戦力）。</summary>
        public static float MainEffortRelief(float enemyForcesDrawn, float enemyTotalForce)
            => MainEffortRelief(enemyForcesDrawn, enemyTotalForce, DecoyForceParams.Default);

        /// <summary>手薄化（≥0）＝clamp01(食いつき割合)×max(0,敵総兵力)。釣り出された分だけ主攻正面が空く。</summary>
        public static float MainEffortRelief(float enemyForcesDrawn, float enemyTotalForce, DecoyForceParams p)
        {
            float drawn = Mathf.Clamp01(enemyForcesDrawn);
            float total = Mathf.Max(0f, enemyTotalForce);
            return drawn * total;
        }

        // ── 囮部隊の損害 ──────────────────────────────────────────

        /// <summary>囮部隊が受ける損害（絶対戦力）＝敵の反応戦力×損害係数（ただし囮の兵力を超えない）。</summary>
        public static float DecoyAttrition(float decoyStrength, float enemyResponse)
            => DecoyAttrition(decoyStrength, enemyResponse, DecoyForceParams.Default);

        /// <summary>囮損害（0..囮兵力）＝clamp(max(0,敵反応)×attritionScale, 0, max(0,囮兵力))。晒した囮は損害を受けるが全滅以上は受けない。</summary>
        public static float DecoyAttrition(float decoyStrength, float enemyResponse, DecoyForceParams p)
        {
            float strength = Mathf.Max(0f, decoyStrength);
            float response = Mathf.Max(0f, enemyResponse);
            return Mathf.Clamp(response * p.attritionScale, 0f, strength);
        }

        // ── 見破りリスク ──────────────────────────────────────────

        /// <summary>囮だと見破られるリスク（0..1）＝説得力の薄さ×敵情報力。本物らしくないほど・敵の眼が利くほど見破られる。</summary>
        public static float Detection(float plausibility, float enemyIntel)
            => Detection(plausibility, enemyIntel, DecoyForceParams.Default);

        /// <summary>見破りリスク（0..1）＝clamp01((1−clamp01(説得力))×clamp01(敵情報力)×detectionScale)。</summary>
        public static float Detection(float plausibility, float enemyIntel, DecoyForceParams p)
        {
            float thinness = 1f - Mathf.Clamp01(plausibility);
            float intel = Mathf.Clamp01(enemyIntel);
            return Mathf.Clamp01(thinness * intel * p.detectionScale);
        }

        // ── 見破られたときの意図露見 ──────────────────────────────

        /// <summary>見破られると本隊（真の主攻）の意図が露見する度合い（0..1）＝見破りリスク×本隊の秘匿不足。秘匿が高いほど露見を抑える。</summary>
        public static float BackfireOnDetection(float detection, float mainEffortConcealment)
            => BackfireOnDetection(detection, mainEffortConcealment, DecoyForceParams.Default);

        /// <summary>意図露見（0..1）＝clamp01(clamp01(見破り)×(1−clamp01(秘匿))×backfireScale)。囮が見破られると逆に主攻がバレる。</summary>
        public static float BackfireOnDetection(float detection, float mainEffortConcealment, DecoyForceParams p)
        {
            float detect = Mathf.Clamp01(detection);
            float exposure = 1f - Mathf.Clamp01(mainEffortConcealment);
            return Mathf.Clamp01(detect * exposure * p.backfireScale);
        }

        // ── 陽動の正味価値 ────────────────────────────────────────

        /// <summary>陽動の正味価値＝主攻正面の手薄化（利得）−囮損害−意図露見の代償（負にもなりうる）。</summary>
        public static float DecoyNetValue(float mainEffortRelief, float decoyAttrition, float backfire)
            => DecoyNetValue(mainEffortRelief, decoyAttrition, backfire, BackfirePenalty);

        /// <summary>正味価値＝max(0,手薄化)−max(0,囮損害)−clamp01(露見)×backfirePenalty。手薄化が代償（損害＋露見）を上回れば正。</summary>
        public static float DecoyNetValue(float mainEffortRelief, float decoyAttrition, float backfire, float backfirePenalty)
        {
            float relief = Mathf.Max(0f, mainEffortRelief);
            float attrition = Mathf.Max(0f, decoyAttrition);
            float exposed = Mathf.Clamp01(backfire) * Mathf.Max(0f, backfirePenalty);
            return relief - attrition - exposed;
        }

        // ── 陽動成功の判定 ────────────────────────────────────────

        /// <summary>陽動が成功したか＝敵が囮へ割いた割合が既定閾値以上（既定 `SuccessThreshold`=0.3）。</summary>
        public static bool IsDeceptionSuccessful(float enemyForcesDrawn)
            => IsDeceptionSuccessful(enemyForcesDrawn, SuccessThreshold);

        /// <summary>陽動成功（bool）＝clamp01(食いつき割合)≥clamp01(threshold)。敵が十分に食いつけば成功。</summary>
        public static bool IsDeceptionSuccessful(float enemyForcesDrawn, float threshold)
        {
            float drawn = Mathf.Clamp01(enemyForcesDrawn);
            return drawn >= Mathf.Clamp01(threshold);
        }

        /// <summary>陽動成功とみなす敵食いつき割合の既定閾値。</summary>
        public const float SuccessThreshold = 0.3f;
    }
}
