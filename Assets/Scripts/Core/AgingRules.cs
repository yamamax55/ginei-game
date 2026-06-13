using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 加齢の能力曲線の純ロジック（CDR-5 #2315）。`GrowthRules`（ADM-2）の伸びと対の<b>減衰</b>＝
    /// 若さ（機動↑）・全盛期・老練（運営/情報↑・機動↓）・老衰（全体↓）のキャリアアーク。
    /// 実効値パターン（基準非破壊・倍率を返すだけ）。`LifecycleRules`#152 の年齢を入力に取る。test-first。
    /// </summary>
    public static class AgingRules
    {
        /// <summary>総合的な能力倍率（全盛期1.0／若年・老年で低下）。</summary>
        public static float GeneralAgingFactor(int age)
        {
            if (age < 25) return 0.85f;                              // 未熟
            if (age <= 40) return Mathf.Lerp(0.85f, 1.0f, (age - 25f) / 15f); // 成長
            if (age <= 55) return 1.0f;                              // 全盛期
            if (age <= 70) return Mathf.Lerp(1.0f, 0.75f, (age - 55f) / 15f); // 老練→衰え
            return 0.75f;                                            // 老衰
        }

        /// <summary>機動系の倍率（若いほど高く・加齢で衰える）。</summary>
        public static float MobilityAgingFactor(int age)
        {
            if (age <= 30) return 1.15f;
            if (age <= 60) return Mathf.Lerp(1.15f, 0.8f, (age - 30f) / 30f);
            return 0.8f;
        }

        /// <summary>運営・情報（知略系）の倍率（経験で上がり、円熟で高止まり）。</summary>
        public static float WisdomAgingFactor(int age)
        {
            if (age <= 25) return 0.85f;
            if (age <= 55) return Mathf.Lerp(0.85f, 1.15f, (age - 25f) / 30f);
            return 1.15f;
        }

        /// <summary>全盛期か（総合倍率が最大の年代）。</summary>
        public static bool IsPrime(int age) => age >= 40 && age <= 55;
    }
}
