using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 投入制約つき生産（FIRMPROD-3・#2084・純ロジック）。
    /// 利用可能な投入から作れる最大産出を解く＝レオンチェフ型の最小律（最も不足する投入がボトルネック）。
    /// 物的3投入（原材料/エネルギー/資本財）を扱う。労働は既存 `EnterpriseRules`#1022 が律速。test-first。
    /// </summary>
    public static class ProductionConstraintRules
    {
        /// <summary>ある投入から作れる最大産出＝可用量/係数（係数0以下は無制約＝<see cref="float.MaxValue"/>）。</summary>
        public static float MaxOutputFromInput(float available, float coefficient)
            => coefficient <= 0f ? float.MaxValue : Mathf.Max(0f, available) / coefficient;

        /// <summary>制約つき産出＝min(計画, 各投入で作れる量)。最も不足する投入で決まる。</summary>
        public static float ConstrainedOutput(float plannedOutput, float availMaterials, float availEnergy, float availCapital)
        {
            float cap = Mathf.Max(0f, plannedOutput);
            cap = Mathf.Min(cap, MaxOutputFromInput(availMaterials, EnterpriseInputRules.InputCoefficient(ProductionInput.原材料)));
            cap = Mathf.Min(cap, MaxOutputFromInput(availEnergy, EnterpriseInputRules.InputCoefficient(ProductionInput.エネルギー)));
            cap = Mathf.Min(cap, MaxOutputFromInput(availCapital, EnterpriseInputRules.InputCoefficient(ProductionInput.資本財)));
            return Mathf.Max(0f, cap);
        }

        /// <summary>
        /// ボトルネックの投入を返す（投入制約で減産しているか＝<paramref name="constrained"/>）。
        /// 計画どおり作れるなら constrained=false（戻り値は無意味）。
        /// </summary>
        public static ProductionInput BindingInput(float plannedOutput, float availMaterials, float availEnergy, float availCapital, out bool constrained)
        {
            float mMat = MaxOutputFromInput(availMaterials, EnterpriseInputRules.InputCoefficient(ProductionInput.原材料));
            float mEne = MaxOutputFromInput(availEnergy, EnterpriseInputRules.InputCoefficient(ProductionInput.エネルギー));
            float mCap = MaxOutputFromInput(availCapital, EnterpriseInputRules.InputCoefficient(ProductionInput.資本財));
            float planned = Mathf.Max(0f, plannedOutput);

            float minCap = Mathf.Min(mMat, Mathf.Min(mEne, mCap));
            constrained = minCap < planned;
            if (mMat <= mEne && mMat <= mCap) return ProductionInput.原材料;
            if (mEne <= mCap) return ProductionInput.エネルギー;
            return ProductionInput.資本財;
        }
    }
}
