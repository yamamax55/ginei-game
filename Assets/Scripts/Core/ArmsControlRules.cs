using UnityEngine;

namespace Ginei
{
    /// <summary>軍縮条約の調整係数。</summary>
    public readonly struct ArmsControlParams
    {
        /// <summary>完全査察(access=1)下で発覚率が1.0に達する条約超過量（大きな嘘ほど隠しにくい比例の分母）。</summary>
        public readonly float gapForCertainDetection;
        /// <summary>発覚時に即座に失われる信頼の下限（小さな違反でも露見すれば信頼の半分は死ぬ）。</summary>
        public readonly float falloutBase;
        /// <summary>経済規模のうち軍備競争に吸われるはずだった比率（軍縮の配当の源泉）。</summary>
        public readonly float savingsRate;
        /// <summary>査察受け入れが自国の手の内（配備・技術）を晒す係数（透明性のコスト）。</summary>
        public readonly float transparencyCostScale;

        public ArmsControlParams(float gapForCertainDetection, float falloutBase, float savingsRate, float transparencyCostScale)
        {
            this.gapForCertainDetection = Mathf.Max(0.01f, gapForCertainDetection);
            this.falloutBase = Mathf.Clamp01(falloutBase);
            this.savingsRate = Mathf.Clamp01(savingsRate);
            this.transparencyCostScale = Mathf.Clamp01(transparencyCostScale);
        }

        /// <summary>既定＝発覚飽和超過量100・発覚時信頼損失下限0.5・配当率0.3・透明性コスト係数0.6。</summary>
        public static ArmsControlParams Default => new ArmsControlParams(100f, 0.5f, 0.3f, 0.6f);
    }

    /// <summary>
    /// 軍縮条約の純ロジック（ワシントン体制型）＝信頼の制度化と裏切りの誘惑。
    /// 建艦上限を双方が呑めば軍拡費が浮く（軍縮の配当）が、脅威感×不信が秘密再軍備を誘い、
    /// 隠した量×査察の深さの「積」で発覚する＝「信頼するが検証せよ」：信頼(誘惑の抑制)と
    /// 検証(査察アクセス)のどちらか片方では条約は持たない。査察を深く受け入れるほど
    /// 相手の違反は見えるが自国の手の内も晒す（検証のジレンマ）。発覚すれば信頼は崩壊し
    /// 以後の条約が結べなくなる持続ペナルティを負う＝裏切りの期待値を変えるのが制度の核。
    /// 条約一般の opinion 効果・破棄・レバレッジは TreatyRules（DIP-2）＝こちらは
    /// 「軍備の検証問題」専用。軍拡の螺旋そのもの（ArmsRaceRules）とは対＝こちらは
    /// 螺旋を制度で止められるかを解く。乱数なし・決定論（roll は呼び出し側が渡す）。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class ArmsControlRules
    {
        /// <summary>
        /// 条約超過分（隠している量）＝Max(0, 実建艦量−条約上限)。
        /// 上限以下なら0＝遵守（入力は非負にクランプ）。
        /// </summary>
        public static float ComplianceGap(float actualBuild, float treatyCap)
        {
            return Mathf.Max(0f, Mathf.Max(0f, actualBuild) - Mathf.Max(0f, treatyCap));
        }

        /// <summary>
        /// 発覚率（0..1）＝Min(超過量/発覚飽和量, 1)×査察アクセス(0..1)。
        /// 積＝大きな嘘ほど隠しにくいが、査察ゼロならどんな大違反も見えない
        /// （検証なき信頼は裏切りを招き、違反なき国に査察は何も出さない）。
        /// </summary>
        public static float DetectionChance(float complianceGap, float inspectionAccess, ArmsControlParams p)
        {
            float gap = Mathf.Clamp01(Mathf.Max(0f, complianceGap) / p.gapForCertainDetection);
            return gap * Mathf.Clamp01(inspectionAccess);
        }

        public static float DetectionChance(float complianceGap, float inspectionAccess)
            => DetectionChance(complianceGap, inspectionAccess, ArmsControlParams.Default);

        /// <summary>発覚判定＝roll(0..1) が発覚率未満なら露見（roll は呼び出し側が渡す決定論）。</summary>
        public static bool Caught(float complianceGap, float inspectionAccess, float roll, ArmsControlParams p)
        {
            return Mathf.Clamp01(roll) < DetectionChance(complianceGap, inspectionAccess, p);
        }

        public static bool Caught(float complianceGap, float inspectionAccess, float roll)
            => Caught(complianceGap, inspectionAccess, roll, ArmsControlParams.Default);

        /// <summary>
        /// 裏切りの誘惑（0..1）＝脅威感(0..1)×相手への不信(1−信頼0..1)。
        /// 積＝相手を信じ切れば誘惑は消え、脅威がなければそもそも破る理由がない
        /// ＝条約は信頼の制度化でしか持たない。
        /// </summary>
        public static float CheatingTemptation(float securityPressure, float trustInRival)
        {
            return Mathf.Clamp01(securityPressure) * (1f - Mathf.Clamp01(trustInRival));
        }

        /// <summary>
        /// 発覚時の信頼崩壊（0..1）＝下限(falloutBase)＋超過量比例の上乗せ。
        /// 超過0なら0だが、一度露見すれば小さな違反でも信頼の半分は死ぬ（下限）
        /// ＝opinion 損失と「以後の条約が結べない」持続ペナルティの双方にこの値を使う＝条約死。
        /// </summary>
        public static float ExposureFallout(float complianceGap, ArmsControlParams p)
        {
            float gap = Mathf.Max(0f, complianceGap);
            if (gap <= 0f) return 0f;
            float scale = Mathf.Clamp01(gap / p.gapForCertainDetection);
            return Mathf.Clamp01(p.falloutBase + scale * (1f - p.falloutBase));
        }

        public static float ExposureFallout(float complianceGap)
            => ExposureFallout(complianceGap, ArmsControlParams.Default);

        /// <summary>
        /// 双方の節約額（軍縮の配当）＝上限の厳しさ(0=制限なし..1=全面禁止)×経済規模×配当率。
        /// 厳しい上限ほど軍備競争に吸われるはずだった国富が内政へ還る＝条約を結ぶ動機の源泉。
        /// </summary>
        public static float MutualSavings(float capRatio, float economySize, ArmsControlParams p)
        {
            return Mathf.Clamp01(capRatio) * Mathf.Max(0f, economySize) * p.savingsRate;
        }

        public static float MutualSavings(float capRatio, float economySize)
            => MutualSavings(capRatio, economySize, ArmsControlParams.Default);

        /// <summary>
        /// 検証のジレンマ（0..1）＝査察アクセス×透明性コスト係数。
        /// 査察を深く受け入れるほど相手の違反は見えるが、自国の配備・技術も同じ深さで晒される
        /// ＝検証の利得は手の内を見せる対価でしか買えない。
        /// </summary>
        public static float VerificationDilemma(float inspectionAccess, ArmsControlParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(inspectionAccess) * p.transparencyCostScale);
        }

        public static float VerificationDilemma(float inspectionAccess)
            => VerificationDilemma(inspectionAccess, ArmsControlParams.Default);
    }
}
