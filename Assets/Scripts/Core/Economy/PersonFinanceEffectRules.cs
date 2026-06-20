using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 財産行動のメリット・デメリットの効果（PFIN-5・#2056・#113/#530/#181 連携・純ロジック）。
    /// 浪費＝気前で人望#113・生活水準↑だが破産しやすい／貯金＝財産が厚く安全余裕・恩給#530 に強いがケチで人望低い／投資＝成長だが暴落で破産。
    /// 実効値パターン（基準非破壊）。test-first。
    /// </summary>
    public static class PersonFinanceEffectRules
    {
        /// <summary>人望/支持#113 への寄与＝浪費額×気前×スケール（浪費型は部下/民へ振る舞い人望↑・貯金型はケチで伸びない）。</summary>
        public static float PopularityDelta(float spent, FinancialTrait t, float scale)
            => Mathf.Max(0f, spent) * FinanceTraitRules.Generosity(t) * Mathf.Max(0f, scale);

        /// <summary>安全余裕（年数）＝財産/年間需要（貯金型は厚く緊急時/退役#530 に強い）。需要0以下は0。</summary>
        public static float SecurityYears(float wealth, float annualNeed)
            => annualNeed <= 0f ? 0f : Mathf.Max(0f, wealth) / annualNeed;

        /// <summary>破産リスク＝(1−安全余裕)の不足ぶん×特性の破産傾向（浪費型は財産が薄く高リスク・貯金型は低い）。</summary>
        public static float RuinRisk(float wealth, float annualNeed, FinancialTrait t)
        {
            float bufferShort = Mathf.Clamp01(1f - SecurityYears(wealth, annualNeed));
            return Mathf.Clamp01(bufferShort * FinanceTraitRules.RuinPropensity(t));
        }

        /// <summary>破産しているか＝財産が尽きた。</summary>
        public static bool IsBankrupt(float wealth) => wealth <= 0f;
    }
}
