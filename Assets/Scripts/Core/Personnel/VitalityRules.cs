using UnityEngine;

namespace Ginei
{
    /// <summary>体質・健康が寿命と回復に与える係数の調整値（PER-PROPS B7）。</summary>
    public readonly struct VitalityParams
    {
        /// <summary>病弱(0)で死亡rollに乗る上振れ（最大 +この値）。</summary>
        public readonly float frailDeathBonus;
        /// <summary>頑健(100)で死亡rollを抑える割合（最大 -この値・0..1）。</summary>
        public readonly float robustDeathCut;
        /// <summary>病弱(0)の疲労回復倍率。</summary>
        public readonly float recoveryMin;
        /// <summary>頑健(100)の疲労回復倍率。</summary>
        public readonly float recoveryMax;

        public VitalityParams(float frailDeathBonus, float robustDeathCut, float recoveryMin, float recoveryMax)
        {
            this.frailDeathBonus = frailDeathBonus;
            this.robustDeathCut = robustDeathCut;
            this.recoveryMin = recoveryMin;
            this.recoveryMax = recoveryMax;
        }

        /// <summary>既定＝病弱死亡+0.5／頑健死亡-0.3／回復0.6〜1.4。</summary>
        public static VitalityParams Default => new VitalityParams(0.5f, 0.3f, 0.6f, 1.4f);
    }

    /// <summary>
    /// 体質・健康（constitution 0..100）が寿命と回復に与える係数（PER-PROPS B7）。低い＝病弱（死亡rollが上振れ・回復遅い）、
    /// 高い＝頑健。<b>一時値の疲労/負傷</b>（ConditionRules）や<b>加齢の死亡</b>（LifecycleRules/AnnualLifecycleRules）に
    /// 掛ける係数を返すだけ＝それらを再実装しない。中庸50で係数1.0。決定論・test-first。
    /// </summary>
    public static class VitalityRules
    {
        /// <summary>死亡roll倍率（&gt;1＝死にやすい）。50で1.0、病弱0で 1+frailDeathBonus、頑健100で 1-robustDeathCut。</summary>
        public static float DeathRiskFactor(int constitution, VitalityParams p)
        {
            float t = (Mathf.Clamp(constitution, 0, 100) - 50) / 50f; // -1..+1
            if (t < 0f) return 1f + (-t) * Mathf.Max(0f, p.frailDeathBonus);
            return 1f - t * Mathf.Clamp01(p.robustDeathCut);
        }

        public static float DeathRiskFactor(int constitution) => DeathRiskFactor(constitution, VitalityParams.Default);

        /// <summary>疲労回復倍率（病弱recoveryMin〜頑健recoveryMax を線形・50で中点）。</summary>
        public static float FatigueRecoveryFactor(int constitution, VitalityParams p)
            => Mathf.Lerp(p.recoveryMin, p.recoveryMax, Mathf.Clamp01(constitution / 100f));

        public static float FatigueRecoveryFactor(int constitution) => FatigueRecoveryFactor(constitution, VitalityParams.Default);
    }
}
