using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 恥の文化の主体（KIKU-1 #1832・ルース・ベネディクト『菊と刀』）の純データ。
    /// 恥の文化は「人目」によって行動が制御される＝罪の文化（内面の良心が制御）と対照的。
    /// 全フィールド 0..1（可変＝時間更新で書き換える）。
    /// </summary>
    [System.Serializable]
    public readonly struct ShameProfile
    {
        /// <summary>可視性 0..1（人目の有無＝見られている度合い。恥は人目がある時だけ効く）。</summary>
        public readonly float visibility;
        /// <summary>面目への敏感さ 0..1（世間体・体面をどれだけ気にするか＝恥の効きやすさ）。</summary>
        public readonly float faceSensitivity;
        /// <summary>内面化された規範 0..1（人目がなくても従う度合い＝罪の文化的な良心。低いほど人目で崩れる）。</summary>
        public readonly float internalizedNorm;

        public ShameProfile(float visibility, float faceSensitivity, float internalizedNorm)
        {
            this.visibility = Mathf.Clamp01(visibility);
            this.faceSensitivity = Mathf.Clamp01(faceSensitivity);
            this.internalizedNorm = Mathf.Clamp01(internalizedNorm);
        }
    }

    /// <summary>恥の文化係数（#1832）の調整係数。マジックナンバー禁止＝ここに集約。</summary>
    public readonly struct ShameParams
    {
        /// <summary>公的な謝罪・償い1.0あたりの面目回復の最大幅（内面でなく公的行為で回復する）。</summary>
        public readonly float restorationScale;
        /// <summary>観衆の大きさが面目の喪失に効く重み（同じ失敗でも見る者が多いほど面目を失う）。</summary>
        public readonly float audienceWeight;
        /// <summary>恥の圧力1.0・集団結束1.0あたりの同調の最大幅（恥が集団同調を強める）。</summary>
        public readonly float conformityScale;
        /// <summary>人目が無い時の規範の自然減衰の最大幅（内面化が低いほど大きく緩む）。</summary>
        public readonly float privateDecayScale;
        /// <summary>恥の文化駆動と判定する可視性依存度の既定閾値。</summary>
        public readonly float shameDrivenThreshold;

        public ShameParams(float restorationScale, float audienceWeight, float conformityScale,
                           float privateDecayScale, float shameDrivenThreshold)
        {
            this.restorationScale = Mathf.Clamp01(restorationScale);
            this.audienceWeight = Mathf.Clamp01(audienceWeight);
            this.conformityScale = Mathf.Clamp01(conformityScale);
            this.privateDecayScale = Mathf.Clamp01(privateDecayScale);
            this.shameDrivenThreshold = Mathf.Clamp01(shameDrivenThreshold);
        }

        /// <summary>
        /// 既定＝面目回復上限0.8/観衆重み0.5/同調上限0.9/人目なし減衰上限0.5/恥文化判定閾値0.5。
        /// </summary>
        public static ShameParams Default =>
            new ShameParams(0.8f, 0.5f, 0.9f, 0.5f, 0.5f);
    }

    /// <summary>
    /// 恥の文化係数の純ロジック（KIKU-1 #1832・ルース・ベネディクト『菊と刀』＝恥の文化 shame culture）。
    /// 恥の文化は「人目」による外的制御＝人に見られている時だけ規範が効き、見られていなければ抑制が弱まる。
    /// 罪の文化（guilt culture＝内面の良心が常に制御）と対照的で、世間体・面目が動機になる。
    /// 恥は隠せば消える（人目を避ければ恥にならない）＝隠蔽の誘因を生み、面目の回復も内面でなく<b>公的行為</b>（謝罪・償い）で行う。
    /// <see cref="PanoptismRules"/>（監視＝「見られているかもしれない」可能性が自己規律を生む規律権力）とは別＝
    /// こちらは恥の<b>内面化と文化的な可視性依存</b>（恥は実際に人目がある時だけ効く・監視インフラ非依存）。
    /// CulturalControlType は将来 <see cref="CultureRules"/> へ統合予定だがここでは独立＝罪の文化との対比を数値化する。
    /// 値は徹底して 0..1 に clamp・乱数なし決定論。盤面非依存の plain 引数。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class ShameRules
    {
        /// <summary>
        /// 恥の圧力 0..1：可視性（人目）×規範違反の度合い。人目がある時だけ恥は効く＝
        /// visibility=0 なら違反しても恥の圧力は 0（見られていなければ恥にならない＝恥の文化の核）。
        /// </summary>
        public static float ShamePressure(float visibility, float normViolation, ShameParams p)
        {
            float v = Mathf.Clamp01(visibility);
            float n = Mathf.Clamp01(normViolation);
            return Mathf.Clamp01(v * n);
        }

        public static float ShamePressure(float visibility, float normViolation)
            => ShamePressure(visibility, normViolation, ShameParams.Default);

        /// <summary>
        /// 行動抑制 0..1：恥の圧力×面目への敏感さ。面目を気にする者ほど恥の圧力が強く行動を抑える＝
        /// 世間体が動機になる。面目に鈍感（faceSensitivity=0）なら恥の圧力があっても抑制は効かない。
        /// </summary>
        public static float BehaviorControl(float shamePressure, float faceSensitivity)
        {
            return Mathf.Clamp01(Mathf.Clamp01(shamePressure) * Mathf.Clamp01(faceSensitivity));
        }

        /// <summary>
        /// 面目の喪失 0..1：公的な失敗×観衆の大きさ。同じ失敗でも見る者が多いほど面目を失う。
        /// 観衆ゼロでも失敗自体の体面低下は残る（audienceWeight ぶんだけ観衆で底上げ）。
        /// </summary>
        public static float FaceLoss(float publicFailure, float audienceSize, ShameParams p)
        {
            float f = Mathf.Clamp01(publicFailure);
            float a = Mathf.Clamp01(audienceSize);
            // 失敗を土台に、観衆の大きさで効きを底上げ（a=0なら(1-audienceWeight)倍・a=1なら満額）。
            float factor = (1f - p.audienceWeight) + p.audienceWeight * a;
            return Mathf.Clamp01(f * factor);
        }

        public static float FaceLoss(float publicFailure, float audienceSize)
            => FaceLoss(publicFailure, audienceSize, ShameParams.Default);

        /// <summary>
        /// 隠蔽の誘因 0..1：規範違反があり、かつ人目が少ないほど隠す誘因が強い（恥は隠せば消える）。
        /// 違反の大きさ×（1-可視性）＝人目がなければ隠して恥を回避できる＝恥の文化の隠蔽インセンティブ。
        /// </summary>
        public static float ConcealmentIncentive(float normViolation, float visibility)
        {
            float n = Mathf.Clamp01(normViolation);
            float v = Mathf.Clamp01(visibility);
            return Mathf.Clamp01(n * (1f - v));
        }

        /// <summary>
        /// 面目の回復 0..1：失った面目を公的な謝罪・償いで回復する（内面の悔悟でなく公的行為で）。
        /// 残る面目喪失 = faceLoss − publicAtonement×restorationScale（償いが大きいほど回復）。0..1 にクランプ。
        /// </summary>
        public static float HonorRestoration(float faceLoss, float publicAtonement, ShameParams p)
        {
            float loss = Mathf.Clamp01(faceLoss);
            float atone = Mathf.Clamp01(publicAtonement);
            return Mathf.Clamp01(loss - atone * p.restorationScale);
        }

        public static float HonorRestoration(float faceLoss, float publicAtonement)
            => HonorRestoration(faceLoss, publicAtonement, ShameParams.Default);

        /// <summary>
        /// 恥／罪の文化の軸 -1..1：可視性依存度が高いほど恥の文化（+1へ）・低いほど罪の文化（-1へ）。
        /// 人目で制御される（外的制御）か内面の良心で制御される（内的制御）かを連続値で表す。
        /// </summary>
        public static float ShameVsGuilt(float visibilityDependence)
        {
            float d = Mathf.Clamp01(visibilityDependence);
            return Mathf.Clamp(d * 2f - 1f, -1f, 1f);
        }

        /// <summary>
        /// 集団同調 0..1：恥の圧力が集団同調を強める。恥の圧力×集団結束＝
        /// 結束が固い集団ほど「人と違うこと」への恥が同調を促す（恥の文化の集団主義的側面）。
        /// </summary>
        public static float SocialConformity(float shamePressure, float groupCohesion, ShameParams p)
        {
            float s = Mathf.Clamp01(shamePressure);
            float c = Mathf.Clamp01(groupCohesion);
            return Mathf.Clamp01(s * c * p.conformityScale);
        }

        public static float SocialConformity(float shamePressure, float groupCohesion)
            => SocialConformity(shamePressure, groupCohesion, ShameParams.Default);

        /// <summary>
        /// 人目がない時の規範の緩み 0..1：可視性が低いほど・内面化が低いほど規範が緩む。
        /// 内面化された規範が高ければ人目がなくても従う（罪の文化的）＝緩みは小さい。
        /// 緩み = (1-可視性)×(1-内面化)×privateDecayScale＝人目もなく良心もなければ最も緩む。
        /// </summary>
        public static float PrivateNormDecay(float visibility, float internalizedNorm, ShameParams p)
        {
            float v = Mathf.Clamp01(visibility);
            float norm = Mathf.Clamp01(internalizedNorm);
            return Mathf.Clamp01((1f - v) * (1f - norm) * p.privateDecayScale);
        }

        public static float PrivateNormDecay(float visibility, float internalizedNorm)
            => PrivateNormDecay(visibility, internalizedNorm, ShameParams.Default);

        /// <summary>
        /// 恥の文化駆動の判定：可視性依存度が threshold 以上なら恥の文化に駆動されていると見なす。
        /// </summary>
        public static bool IsShameDriven(float visibilityDependence, float threshold)
        {
            return Mathf.Clamp01(visibilityDependence) >= Mathf.Clamp01(threshold);
        }

        /// <summary>既定閾値（<see cref="ShameParams.shameDrivenThreshold"/>）での恥の文化駆動判定。</summary>
        public static bool IsShameDriven(float visibilityDependence)
            => IsShameDriven(visibilityDependence, ShameParams.Default.shameDrivenThreshold);
    }
}
