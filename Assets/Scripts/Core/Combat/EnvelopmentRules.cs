using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 挟撃／包囲（envelopment・#2178）の純ロジック。敵を複数方向（前＋側背）から捉えているほど、その敵への与ダメが増す。
    /// 機動の意味を生み、軍団陣形・前列ローテ・側背面と相乗する。攻撃元方向の角度分布から包囲度を出し、
    /// 過剰にならないよう被ダメ増を上限クランプ。test-first。
    /// </summary>
    public static class EnvelopmentRules
    {
        /// <summary>包囲とみなし始める最小角度差（度）。これ未満は同方向＝挟撃なし。</summary>
        public const float MinSpreadDeg = 60f;
        /// <summary>完全包囲時の被ダメ増（+25%）。</summary>
        public const float MaxBonus = 0.25f;

        /// <summary>
        /// 攻撃元方向（標的から見た各攻撃者の方位・度）の角度分布から包囲度（0..1）を返す。
        /// 攻撃者2未満は0。最大ペア角度差が MinSpreadDeg〜180 のとき0→1へ線形。
        /// </summary>
        public static float EncirclementFactor(IReadOnlyList<float> attackerAnglesDeg, float minSpreadDeg = MinSpreadDeg)
        {
            if (attackerAnglesDeg == null || attackerAnglesDeg.Count < 2) return 0f;

            float maxDiff = 0f;
            for (int i = 0; i < attackerAnglesDeg.Count; i++)
                for (int j = i + 1; j < attackerAnglesDeg.Count; j++)
                {
                    float d = CircularDiff(attackerAnglesDeg[i], attackerAnglesDeg[j]);
                    if (d > maxDiff) maxDiff = d;
                }

            float min = Mathf.Clamp(minSpreadDeg, 0f, 179f);
            if (maxDiff <= min) return 0f;
            return Mathf.Clamp01((maxDiff - min) / (180f - min));
        }

        /// <summary>包囲度から被ダメージ倍率（1＋包囲度×maxBonus）。</summary>
        public static float DamageFactor(float encirclement, float maxBonus = MaxBonus)
            => 1f + Mathf.Clamp01(encirclement) * Mathf.Max(0f, maxBonus);

        /// <summary>2方位の最小角度差（0..180）。</summary>
        private static float CircularDiff(float a, float b)
        {
            float d = Mathf.Abs(Mathf.DeltaAngle(a, b));
            return d;
        }
    }
}
