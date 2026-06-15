using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 陣形の相性（じゃんけん・#2177）の純ロジック。攻撃側陣形×防御側陣形で与ダメージに小さな相性補正を掛ける。
    /// 史実意匠の三すくみ（紡錘＞横陣＞鶴翼＞紡錘）：
    /// ・紡錘陣（集中突破）は横陣（薄い一直線）を貫く。
    /// ・横陣（最大火力の正面）は鶴翼（薄く広がる弧）を撃ち崩す。
    /// ・鶴翼陣（包囲）は紡錘（突出した楔）の側面を包む。
    /// 円陣・方陣（守勢陣形）は三すくみの外＝相性補正なし（堅さは <see cref="FormationTraitRules"/> の被ダメ倍率で別途効く）。
    /// 過剰にならないよう ±10% にクランプ。実効値パターン（基準ダメージ非破壊）・test-first。
    /// </summary>
    public static class FormationMatchupRules
    {
        public const float Advantage = 1.10f;    // 相性有利（カウンター成立）
        public const float Disadvantage = 0.90f; // 相性不利（カウンターされる）
        public const float Neutral = 1.0f;       // 相性なし

        /// <summary>
        /// 攻撃側陣形が防御側陣形に対して持つ与ダメージ相性倍率。三すくみで有利1.10/不利0.90、その他1.0。
        /// </summary>
        public static float AttackFactor(Formation attacker, Formation defender)
        {
            if (attacker == defender) return Neutral;
            if (Counters(attacker, defender)) return Advantage;
            if (Counters(defender, attacker)) return Disadvantage;
            return Neutral;
        }

        /// <summary>a が b をカウンターするか（三すくみ：紡錘→横陣→鶴翼→紡錘）。</summary>
        private static bool Counters(Formation a, Formation b)
        {
            return (a == Formation.紡錘陣 && b == Formation.横陣)
                || (a == Formation.横陣 && b == Formation.鶴翼陣)
                || (a == Formation.鶴翼陣 && b == Formation.紡錘陣);
        }
    }
}
