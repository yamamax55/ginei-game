using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 命中・回避（accuracy/evasion・#2255）の純ロジック。確定命中に揺らぎを入れ、高速/小型艦の回避や提督の情報/機動を活かす。
    /// 命中率は中庸付近（0.5〜0.95）に収束させ、過度な乱数を避ける。外れても0でなく「かすり」（grazeFactor）に留め理不尽を防ぐ。
    /// 決定論 roll を注入（再現性）。test-first。
    /// </summary>
    public static class AccuracyRules
    {
        public const float MinHit = 0.5f;        // 命中率の下限（コイントス未満にしない）
        public const float MaxHit = 0.95f;       // 命中率の上限（必中にしない）
        public const float BaseHit = 0.75f;      // 命中・回避が拮抗したときの命中率
        public const float GrazeFactor = 0.3f;   // 外れ時の与ダメ（0でなくかすり）

        /// <summary>命中率（0.5〜0.95）。命中＝攻撃側精度−防御側回避、拮抗で BaseHit。入力は 0..100。</summary>
        public static float HitChance(float accuracy, float evasion)
        {
            float diff = Mathf.Clamp(accuracy, 0f, 100f) - Mathf.Clamp(evasion, 0f, 100f);
            return Mathf.Clamp(BaseHit + diff / 200f, MinHit, MaxHit);
        }

        /// <summary>命中倍率（命中=1.0／外れ=grazeFactor）。roll∈[0,1]。</summary>
        public static float HitFactor(float hitChance, float roll, float grazeFactor = GrazeFactor)
            => roll <= Mathf.Clamp01(hitChance) ? 1f : Mathf.Clamp01(grazeFactor);
    }
}
