using UnityEngine;

namespace Ginei
{
    /// <summary>報道の自由の調整係数（監視者の自由度）。</summary>
    public readonly struct FreePressParams
    {
        /// <summary>完全自由（pressFreedom=1）でも届く腐敗発見率の上限（0..1。自由でも全ては暴けない）。</summary>
        public readonly float detectionCeiling;
        /// <summary>露見した隠れ腐敗ストックの排出速度（発見率に乗算・per dt）。</summary>
        public readonly float purgeRate;
        /// <summary>失政が政権の恥になる重み（0..1。自由な報道は政権に痛い＝統制の誘因）。</summary>
        public readonly float embarrassmentWeight;
        /// <summary>体制の長期健全性配当の速さ（自由度に比例・per dt。痛みの対価＝自浄作用）。</summary>
        public readonly float healthDividendRate;
        /// <summary>抑え込んだ醜聞1件あたりの爆発打撃の単価（≥0）。</summary>
        public readonly float blowbackPerStory;
        /// <summary>同時露見の相乗係数（件数が増えるほど1件あたりの打撃も増す＝個別露見の合算を超える）。</summary>
        public readonly float compoundingPerStory;
        /// <summary>資本集中が報道を籠絡する重み（0..1。検閲なき統制＝オーナーの自主規制）。</summary>
        public readonly float captureWeight;

        public FreePressParams(float detectionCeiling, float purgeRate, float embarrassmentWeight,
                               float healthDividendRate, float blowbackPerStory, float compoundingPerStory,
                               float captureWeight)
        {
            this.detectionCeiling = Mathf.Clamp01(detectionCeiling);
            this.purgeRate = Mathf.Max(0f, purgeRate);
            this.embarrassmentWeight = Mathf.Clamp01(embarrassmentWeight);
            this.healthDividendRate = Mathf.Max(0f, healthDividendRate);
            this.blowbackPerStory = Mathf.Max(0f, blowbackPerStory);
            this.compoundingPerStory = Mathf.Max(0f, compoundingPerStory);
            this.captureWeight = Mathf.Clamp01(captureWeight);
        }

        /// <summary>既定＝発見上限0.8・排出1.0・恥重み1.0・健全配当0.05・爆発単価0.1・相乗0.25・籠絡重み1.0。</summary>
        public static FreePressParams Default => new FreePressParams(0.8f, 1f, 1f, 0.05f, 0.1f, 0.25f, 1f);
    }

    /// <summary>
    /// 報道の自由の純ロジック（自由な報道は政権の敵で体制の友）。自由な報道は腐敗・失政を早期に露見させる
    /// ＝政権には恥で痛いが体制には自浄作用の薬。統制は短期の静穏と引き換えに見えない腐敗を溜め、
    /// 溜まった醜聞は一度に漏れて体制を殺す（チェルノブイリ型爆発）。
    /// <see cref="PropagandaRules"/>（発信側＝政権が世論へ語る情報操作）とは別系統＝こちらは監視側
    /// （政権を見張る側の自由度）。<see cref="ScandalRules.ExposureChance(float,float,float,ScandalParams)"/> の
    /// pressFreedom 入力の出所＝醜聞の嗅ぎ回り度はこの自由度から来る。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class FreePressRules
    {
        /// <summary>
        /// 腐敗の早期発見率（0..1）＝発見上限 detectionCeiling × 報道の自由 pressFreedom(0..1)。
        /// 統制下（自由ゼロ）では腐敗は一切露見しない＝静かだが見えていないだけ。
        /// </summary>
        public static float CorruptionDetectionRate(float pressFreedom, FreePressParams p)
        {
            return p.detectionCeiling * Mathf.Clamp01(pressFreedom);
        }

        public static float CorruptionDetectionRate(float pressFreedom)
            => CorruptionDetectionRate(pressFreedom, FreePressParams.Default);

        /// <summary>
        /// 見えない腐敗ストックの1tick更新（≥0）。流入 corruptionInflow(per dt) のうち発見率ぶんは
        /// 都度露見して溜まらず、残りが蓄積する。既存ストックも発見率×purgeRate で排出される：
        /// 自由なら低位で均衡（都度排出）、統制下（自由ゼロ）では全量が溜まり続ける＝長期の腐敗蓄積。
        /// </summary>
        public static float HiddenCorruptionTick(float hidden, float corruptionInflow, float pressFreedom, float dt, FreePressParams p)
        {
            float stock = Mathf.Max(0f, hidden);
            float detect = CorruptionDetectionRate(pressFreedom, p);
            float time = Mathf.Max(0f, dt);
            float gained = Mathf.Max(0f, corruptionInflow) * (1f - detect) * time;
            float purged = stock * detect * p.purgeRate * time;
            return Mathf.Max(0f, stock + gained - purged);
        }

        public static float HiddenCorruptionTick(float hidden, float corruptionInflow, float pressFreedom, float dt)
            => HiddenCorruptionTick(hidden, corruptionInflow, pressFreedom, dt, FreePressParams.Default);

        /// <summary>
        /// 政権の恥（0..1）＝報道の自由 × 失政の度合い incompetence(0..1) × 恥重み。
        /// 自由な報道は政権の失政をそのまま衆目に晒す＝政権には痛い＝これが統制の誘因になる。
        /// 統制下（自由ゼロ）では恥はゼロ＝短期の静穏（失政が消えたわけではない）。
        /// </summary>
        public static float RegimeEmbarrassment(float pressFreedom, float incompetence, FreePressParams p)
        {
            return Mathf.Clamp01(pressFreedom) * Mathf.Clamp01(incompetence) * p.embarrassmentWeight;
        }

        public static float RegimeEmbarrassment(float pressFreedom, float incompetence)
            => RegimeEmbarrassment(pressFreedom, incompetence, FreePressParams.Default);

        /// <summary>
        /// 体制の長期健全性配当（≥0）＝健全配当率 × 報道の自由 × dt。
        /// 政権が払う恥の対価として体制が受け取る自浄作用＝自由な報道は政権の敵で体制の友。
        /// </summary>
        public static float SystemicHealthDividend(float pressFreedom, float dt, FreePressParams p)
        {
            return p.healthDividendRate * Mathf.Clamp01(pressFreedom) * Mathf.Max(0f, dt);
        }

        public static float SystemicHealthDividend(float pressFreedom, float dt)
            => SystemicHealthDividend(pressFreedom, dt, FreePressParams.Default);

        /// <summary>
        /// 検閲の爆発（≥0）＝漏洩確度 eventualLeakChance(0..1) × 件数 × 単価 × 同時露見の相乗。
        /// 統制下で抑え込んだ醜聞は一度に漏れ、件数が多いほど1件あたりの打撃も増す
        /// ＝個別に露見していた場合の合算を超える（チェルノブイリ型＝隠蔽の総決算が体制を殺す）。
        /// </summary>
        public static float CensorshipBlowback(int suppressedStories, float eventualLeakChance, FreePressParams p)
        {
            int n = Mathf.Max(0, suppressedStories);
            float compounding = 1f + p.compoundingPerStory * Mathf.Max(0, n - 1);
            return Mathf.Clamp01(eventualLeakChance) * n * p.blowbackPerStory * compounding;
        }

        public static float CensorshipBlowback(int suppressedStories, float eventualLeakChance)
            => CensorshipBlowback(suppressedStories, eventualLeakChance, FreePressParams.Default);

        /// <summary>
        /// 報道の籠絡リスク（0..1）＝籠絡重み × 資本集中 mediaOwnershipConcentration(0..1) の二乗。
        /// 検閲が無くてもオーナーの自主規制で監視は鈍る：分散所有ではほぼ無害、独占に近づくほど急増する。
        /// </summary>
        public static float PressCaptureRisk(float mediaOwnershipConcentration, FreePressParams p)
        {
            float c = Mathf.Clamp01(mediaOwnershipConcentration);
            return Mathf.Clamp01(p.captureWeight * c * c);
        }

        public static float PressCaptureRisk(float mediaOwnershipConcentration)
            => PressCaptureRisk(mediaOwnershipConcentration, FreePressParams.Default);

        /// <summary>
        /// 実効的な報道の自由（0..1）＝法的自由 × (1 − 籠絡リスク)。
        /// 法律上は自由でも資本が独占されていれば監視は機能しない＝検閲なき統制。
        /// 発見率・恥・配当の pressFreedom 入力には本来こちらを渡すのが筋。
        /// </summary>
        public static float EffectiveFreedom(float pressFreedom, float mediaOwnershipConcentration, FreePressParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(pressFreedom) * (1f - PressCaptureRisk(mediaOwnershipConcentration, p)));
        }

        public static float EffectiveFreedom(float pressFreedom, float mediaOwnershipConcentration)
            => EffectiveFreedom(pressFreedom, mediaOwnershipConcentration, FreePressParams.Default);
    }
}
