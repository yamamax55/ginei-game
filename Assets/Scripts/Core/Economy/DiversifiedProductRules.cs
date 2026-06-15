using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// その他製品メーカーのロジック（東証33業種「その他製品」・#2024・純ロジック・唯一の窓口）：玩具/文具/楽器/スポーツ用品等の
    /// 雑多な製品群＝複数事業の合算（OTH-1）／ポートフォリオ分散（OTH-2・1−HHI）／ブランド価格プレミアム（OTH-3）。製造（#2016）・
    /// 小売（#2017）・消費（#1951）へ接続。マクロ近似。test-first。
    /// </summary>
    public static class DiversifiedProductRules
    {
        /// <summary>事業群の合計収益＝各製品ラインの和（多角化した雑多な製品の集計）。</summary>
        public static float ProductLineRevenue(IReadOnlyList<float> lineRevenues)
        {
            if (lineRevenues == null) return 0f;
            float sum = 0f;
            for (int i = 0; i < lineRevenues.Count; i++) sum += Mathf.Max(0f, lineRevenues[i]);
            return sum;
        }

        /// <summary>ポートフォリオ分散度（0..1）＝1−ハーフィンダル指数（多くの製品に分散するほど高い＝特定製品の不振に強い）。</summary>
        public static float PortfolioDiversification(IReadOnlyList<float> lineRevenues)
        {
            float total = ProductLineRevenue(lineRevenues);
            if (total <= 0f) return 0f;
            float hhi = 0f;
            for (int i = 0; i < lineRevenues.Count; i++)
            {
                float share = Mathf.Max(0f, lineRevenues[i]) / total;
                hhi += share * share;
            }
            return Mathf.Clamp01(1f - hhi);
        }

        /// <summary>ブランド価格＝基準価格×(1＋ブランド力×上限)（玩具/楽器等はブランドが価格を支える）。</summary>
        public static float BrandedPrice(float basePrice, float brandStrength, float maxPremium)
            => Mathf.Max(0f, basePrice) * (1f + Mathf.Clamp01(brandStrength) * Mathf.Max(0f, maxPremium));
    }
}
