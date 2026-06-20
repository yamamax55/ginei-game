using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 国家状態と諸侯の忠誠を繋ぐ純ロジック（社会シミュ ↔ 関ヶ原 #817 の連結）。腐った/正統性の低い/
    /// 結束の弱い国（<see cref="FactionState"/> が不安定）ほど、諸侯(<see cref="Allegiance"/>)の基準忠誠が
    /// 低く、調略に付け入られやすい＝<b>武力でなく寝返りやすさで「戦う前に決まる戦い」に負ける</b>。
    /// テーマ（神話→歴史・カリスマの日常化）を盤面の勝敗に接続する。純ロジック・test-first。
    /// </summary>
    public static class FactionLoyaltyRules
    {
        /// <summary>国家状態から諸侯の基準忠誠 0..1 を導く（正統性・結束・希望の平均）。</summary>
        public static float BaselineLoyalty(FactionState s)
        {
            if (s == null) return 0.5f;
            return Mathf.Clamp01((s.regime.legitimacy + s.organization.cohesion + s.community.hope) / 3f);
        }

        /// <summary>調略の付け入りやすさ＝1 - 基準忠誠（弱った国の諸侯は寝返りやすい）。</summary>
        public static float BribeSusceptibility(FactionState s) => 1f - BaselineLoyalty(s);

        /// <summary>諸侯に国家状態由来の基準忠誠を反映する（loyalty を baseline に設定）。</summary>
        public static void ApplyBaseline(Allegiance a, FactionState s)
        {
            if (a == null || s == null) return;
            a.loyalty = BaselineLoyalty(s);
        }
    }
}
