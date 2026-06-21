using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 軍の全階級ラダー（TKO-13・#2490）。兵卒（二等兵〜兵長）→下士官（伍長〜曹長）→准士官（准尉）→士官（少尉〜元帥）。
    /// 士官（少尉以上）は既存の階級 tier（少尉1〜元帥10・<see cref="RankSystem"/>）に一致＝中尉/中佐は圧縮済みラダーに合わせる。
    /// 序数 0=二等兵（最下）… 末尾=元帥（最上）。
    /// </summary>
    public enum MilitaryGrade
    {
        二等兵, 一等兵, 上等兵, 兵長,        // 兵卒
        伍長, 軍曹, 曹長,                  // 下士官
        准尉,                            // 准士官
        少尉, 大尉, 少佐, 大佐,            // 尉官・佐官（士官tier 1-4）
        准将, 少将, 中将, 大将, 上級大将, 元帥 // 将官（士官tier 5-10）
    }

    /// <summary>
    /// 勢力の階級別人数（TKO-13・純データ）。個体に降りず<b>階級ごとの頭数</b>だけを持つ（マクロ集約＝PERF）。
    /// 階級ピラミッド（下が広く上が狭い）の実体。集計・目標定員・空き判定は <see cref="RankDistributionRules"/> が窓口。
    /// </summary>
    public class RankDistribution
    {
        public readonly int[] counts;

        public RankDistribution()
        {
            counts = new int[RankDistributionRules.GradeCount];
        }

        public int Get(MilitaryGrade g) => counts[(int)g];
        public void Set(MilitaryGrade g, int n) => counts[(int)g] = Mathf.Max(0, n);
        public void Add(MilitaryGrade g, int n) => counts[(int)g] = Mathf.Max(0, counts[(int)g] + n);
        public void Clear() { for (int i = 0; i < counts.Length; i++) counts[i] = 0; }
    }

    /// <summary>
    /// 階級ピラミッド／定員の純ロジック（TKO-13・太閤立志伝V「上ほど狭き門」のマクロ裏打ち・#2490・唯一の窓口）。
    /// 兵卒〜元帥の<b>階級別人数</b>（<see cref="RankDistribution"/>）を、個体に降りずに管理する：理想ピラミッド定員
    /// （<see cref="PyramidTarget"/>＝幾何減衰で下が広く上が狭い）・定員空き（<see cref="Vacancy"/>＝昇進枠＝上が詰まれば昇進が律速）・
    /// 将校過多（<see cref="IsTopHeavy"/>＝頭でっかちな組織病理）・階級区分。士官グレードは <see cref="ToOfficerTier"/> で既存の
    /// 階級 tier（<see cref="RankSystem"/>）へ橋渡しする（並行系を作らない＝additive）。決定論・test-first。
    /// </summary>
    public static class RankDistributionRules
    {
        /// <summary>全階級数。</summary>
        public static readonly int GradeCount = Enum.GetValues(typeof(MilitaryGrade)).Length;

        /// <summary>ピラミッドの調整値。</summary>
        public readonly struct PyramidParams
        {
            /// <summary>1段上がるごとの人数比（&lt;1＝上ほど少ない）。</summary>
            public readonly float ratio;

            public PyramidParams(float ratio) { this.ratio = Mathf.Clamp(ratio, 0.05f, 0.95f); }

            /// <summary>既定＝1段上で約45%（下が広く上が狭いピラミッド）。</summary>
            public static PyramidParams Default => new PyramidParams(0.45f);
        }

        // ===== 階級区分 =====

        public static bool IsEnlisted(MilitaryGrade g) => g <= MilitaryGrade.兵長;       // 兵卒
        public static bool IsNco(MilitaryGrade g) => g >= MilitaryGrade.伍長 && g <= MilitaryGrade.曹長; // 下士官
        public static bool IsWarrant(MilitaryGrade g) => g == MilitaryGrade.准尉;          // 准士官
        public static bool IsOfficer(MilitaryGrade g) => g >= MilitaryGrade.少尉;          // 士官（将校）
        public static bool IsFlag(MilitaryGrade g) => g >= MilitaryGrade.准将;             // 将官

        /// <summary>
        /// 士官グレードを既存の階級 tier（少尉1/大尉2/少佐3/大佐4/准将5/少将6/中将7/大将8/上級大将9/元帥10）へ写す。
        /// 兵卒〜准尉（任官前）は 0（tier を持たない）。<see cref="RankSystem"/> との橋＝並行系を作らない。
        /// </summary>
        public static int ToOfficerTier(MilitaryGrade g)
        {
            switch (g)
            {
                case MilitaryGrade.少尉: return 1;
                case MilitaryGrade.大尉: return 2;
                case MilitaryGrade.少佐: return 3;
                case MilitaryGrade.大佐: return 4;
                case MilitaryGrade.准将: return 5;
                case MilitaryGrade.少将: return 6;
                case MilitaryGrade.中将: return 7;
                case MilitaryGrade.大将: return 8;
                case MilitaryGrade.上級大将: return 9;
                case MilitaryGrade.元帥: return 10;
                default: return 0; // 兵卒〜准尉
            }
        }

        /// <summary>士官 tier（少尉1〜元帥10）に対応する <see cref="MilitaryGrade"/>（<see cref="ToOfficerTier"/> の逆）。
        /// 範囲外は安全側（&lt;1＝准尉／&gt;10＝元帥）。昇進の定員ゲート（その階級に空きがあるか）で使う。</summary>
        public static MilitaryGrade GradeForOfficerTier(int tier)
        {
            switch (tier)
            {
                case 1: return MilitaryGrade.少尉;
                case 2: return MilitaryGrade.大尉;
                case 3: return MilitaryGrade.少佐;
                case 4: return MilitaryGrade.大佐;
                case 5: return MilitaryGrade.准将;
                case 6: return MilitaryGrade.少将;
                case 7: return MilitaryGrade.中将;
                case 8: return MilitaryGrade.大将;
                case 9: return MilitaryGrade.上級大将;
                case 10: return MilitaryGrade.元帥;
                default: return tier < 1 ? MilitaryGrade.准尉 : MilitaryGrade.元帥;
            }
        }

        // ===== 集計 =====

        public static int TotalForce(RankDistribution d)
        {
            if (d == null) return 0;
            int sum = 0;
            for (int i = 0; i < d.counts.Length; i++) sum += d.counts[i];
            return sum;
        }

        /// <summary>士官（少尉以上）の総数。</summary>
        public static int OfficerCount(RankDistribution d)
        {
            if (d == null) return 0;
            int sum = 0;
            for (int i = (int)MilitaryGrade.少尉; i < d.counts.Length; i++) sum += d.counts[i];
            return sum;
        }

        /// <summary>士官比率（士官÷総数・0..1）。総数0は0。</summary>
        public static float OfficerRatio(RankDistribution d)
        {
            int total = TotalForce(d);
            return total <= 0 ? 0f : (float)OfficerCount(d) / total;
        }

        // ===== ピラミッド／定員 =====

        /// <summary>
        /// 理想ピラミッドの階級別目標定員。重み w[i]=ratio^i（i=0 二等兵…最上 元帥）＝下が広く上が狭い。
        /// 合計が <paramref name="totalForce"/> になるよう比例配分（丸め）。<paramref name="totalForce"/>≤0 は全0。
        /// </summary>
        public static int[] PyramidTarget(int totalForce, PyramidParams p)
        {
            int n = GradeCount;
            var target = new int[n];
            if (totalForce <= 0) return target;
            var w = new double[n];
            double sum = 0d;
            for (int i = 0; i < n; i++) { w[i] = Math.Pow(p.ratio, i); sum += w[i]; }
            for (int i = 0; i < n; i++) target[i] = (int)Math.Round(totalForce * w[i] / sum);
            return target;
        }

        /// <summary>
        /// その階級の定員空き＝目標定員−現員（≥0）。空きがあるほど昇進・補充の余地がある（上が詰まれば 0＝昇進律速）。
        /// </summary>
        public static int Vacancy(RankDistribution d, MilitaryGrade g, int[] target)
        {
            if (d == null || target == null) return 0;
            int idx = (int)g;
            if (idx < 0 || idx >= target.Length) return 0;
            return Mathf.Max(0, target[idx] - d.Get(g));
        }

        /// <summary>
        /// 名のある士官（提督名鑑の実在人物）の階級別人数を、ピラミッドの士官段（少尉〜元帥）へ重ねて
        /// <b>頂点を実態（実在の提督）に一致</b>させる。各士官段を max(現員, 実在数) へ引き上げる＝置換でなく下限保証
        /// （マクロ集計との二重計上を抑えつつ、幾何減衰で 0 に丸まる「元帥0/大将0」を防ぐ）。
        /// <paramref name="namedByTier"/>[t] は階級 tier t（少尉1〜元帥10・<see cref="ToOfficerTier"/>）の実在士官数。
        /// index 0 は未使用、長さ不足の tier は無視。null/空は無変更（後方互換）。
        /// </summary>
        public static void OverlayNamedOfficers(RankDistribution d, IReadOnlyList<int> namedByTier)
        {
            if (d == null || namedByTier == null) return;
            for (int tier = 1; tier <= 10; tier++)
            {
                if (tier >= namedByTier.Count) break;
                int n = namedByTier[tier];
                if (n <= 0) continue;
                MilitaryGrade g = GradeForOfficerTier(tier);
                if (d.Get(g) < n) d.Set(g, n);
            }
        }

        /// <summary>
        /// 頭でっかち（将校過多）か＝実際の士官比率が、理想ピラミッドの士官比率＋許容を超える（組織病理）。
        /// 既定ピラミッドの士官比率より明確に多ければ true。
        /// </summary>
        public static bool IsTopHeavy(RankDistribution d, PyramidParams p, float tolerance)
        {
            int total = TotalForce(d);
            if (total <= 0) return false;
            int[] target = PyramidTarget(total, p);
            int tt = 0, to = 0;
            for (int i = 0; i < target.Length; i++)
            {
                tt += target[i];
                if (IsOfficer((MilitaryGrade)i)) to += target[i];
            }
            float pyramidOfficer = tt > 0 ? (float)to / tt : 0f;
            return OfficerRatio(d) > pyramidOfficer + Mathf.Max(0f, tolerance);
        }
    }
}
