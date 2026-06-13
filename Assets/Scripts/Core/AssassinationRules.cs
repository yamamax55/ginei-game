using UnityEngine;

namespace Ginei
{
    /// <summary>暗殺企図の結末。</summary>
    public enum AssassinationOutcome
    {
        失敗,   // 仕損じたが企てはばれていない
        成功,   // 標的を排除した
        露見    // 仕損じた上に企てと黒幕が露見した（最悪）
    }

    /// <summary>暗殺の調整係数（地球教型の要人テロ）。</summary>
    public readonly struct AssassinationParams
    {
        /// <summary>警護ゼロ時の基礎成功率。</summary>
        public readonly float baseSuccess;
        /// <summary>仕損じたとき露見する条件付き確率（警護が固いほど露見しやすい係数）。</summary>
        public readonly float exposureOnFailure;
        /// <summary>継承ショックの最大幅（標的の重要度最大・制度化ゼロのとき）。</summary>
        public readonly float successionShockScale;
        /// <summary>露見時に黒幕が失う正統性の幅。</summary>
        public readonly float exposureLegitimacyHit;

        public AssassinationParams(float baseSuccess, float exposureOnFailure,
                                   float successionShockScale, float exposureLegitimacyHit)
        {
            this.baseSuccess = Mathf.Clamp01(baseSuccess);
            this.exposureOnFailure = Mathf.Clamp01(exposureOnFailure);
            this.successionShockScale = Mathf.Max(0f, successionShockScale);
            this.exposureLegitimacyHit = Mathf.Max(0f, exposureLegitimacyHit);
        }

        /// <summary>既定＝基礎成功0.6・露見係数0.5・継承ショック幅0.5・露見正統性ダメージ0.3。</summary>
        public static AssassinationParams Default => new AssassinationParams(0.6f, 0.5f, 0.5f, 0.3f);
    }

    /// <summary>
    /// 要人暗殺の純ロジック（地球教型）。成否は工作の浸透度と標的の警護水準の綱引きで決まり、
    /// 仕損じれば企てが露見して黒幕が正統性を失うリスクを負う。成功しても、組織が属人的
    /// （制度化が低い）なほど継承ショックが大きい＝制度化された組織は英雄を殺しても止まらない
    /// （<see cref="SuccessionRules"/> の継承モデルと整合）。死亡処理そのものは
    /// <see cref="LifecycleRules.Kill"/>、諜報網の浸透値は <see cref="EspionageRules"/> 側が出す
    /// （ここは企ての解決のみ）。乱数は外から roll∈[0,1) を渡す決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class AssassinationRules
    {
        /// <summary>
        /// 暗殺成功率（0..1）＝基礎成功率×浸透度(0..1)×（1−警護水準(0..1)）。
        /// 警護が完全なら0、浸透がなければ0。
        /// </summary>
        public static float SuccessChance(float infiltration, float security, AssassinationParams p)
        {
            return Mathf.Clamp01(p.baseSuccess * Mathf.Clamp01(infiltration) * (1f - Mathf.Clamp01(security)));
        }

        public static float SuccessChance(float infiltration, float security)
            => SuccessChance(infiltration, security, AssassinationParams.Default);

        /// <summary>
        /// 仕損じたときの露見確率（0..1）＝露見係数×警護水準。固い警護は刺客を生け捕りにして黒幕まで辿る。
        /// </summary>
        public static float ExposureChance(float security, AssassinationParams p)
        {
            return Mathf.Clamp01(p.exposureOnFailure * Mathf.Clamp01(security));
        }

        public static float ExposureChance(float security) => ExposureChance(security, AssassinationParams.Default);

        /// <summary>
        /// 企ての解決（決定論）。roll∈[0,1) が成功率未満なら成功。仕損じた場合は同じ roll を
        /// 失敗域に正規化し、露見確率未満なら露見・以上ならただの失敗。
        /// </summary>
        public static AssassinationOutcome Attempt(float infiltration, float security, float roll, AssassinationParams p)
        {
            float success = SuccessChance(infiltration, security, p);
            float r = Mathf.Clamp01(roll);
            if (r < success) return AssassinationOutcome.成功;
            // 失敗域 [success,1) を [0,1) に正規化して露見判定に使う（1つの roll で決定論）
            float failSpan = 1f - success;
            float subRoll = failSpan > 0f ? (r - success) / failSpan : 0f;
            return subRoll < ExposureChance(security, p) ? AssassinationOutcome.露見 : AssassinationOutcome.失敗;
        }

        public static AssassinationOutcome Attempt(float infiltration, float security, float roll)
            => Attempt(infiltration, security, roll, AssassinationParams.Default);

        /// <summary>
        /// 継承ショック（0..successionShockScale）＝標的の重要度(0..1)×（1−制度化(0..1)）に比例。
        /// 属人的な組織の柱を折れば大きく揺れ、制度化された組織は人が変わっても回り続ける。
        /// </summary>
        public static float SuccessionShock(float victimImportance, float institutionalization, AssassinationParams p)
        {
            return Mathf.Clamp01(victimImportance) * (1f - Mathf.Clamp01(institutionalization)) * p.successionShockScale;
        }

        public static float SuccessionShock(float victimImportance, float institutionalization)
            => SuccessionShock(victimImportance, institutionalization, AssassinationParams.Default);

        /// <summary>露見時に黒幕が失う正統性（0..exposureLegitimacyHit）＝標的の重要度に比例（大物狙いほど外聞が悪い）。</summary>
        public static float ExposureLegitimacyLoss(float victimImportance, AssassinationParams p)
        {
            return Mathf.Clamp01(victimImportance) * p.exposureLegitimacyHit;
        }

        public static float ExposureLegitimacyLoss(float victimImportance)
            => ExposureLegitimacyLoss(victimImportance, AssassinationParams.Default);
    }
}
