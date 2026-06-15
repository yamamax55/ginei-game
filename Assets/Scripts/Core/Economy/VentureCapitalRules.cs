using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// ベンチャーキャピタル（VC）のロジック（業種細分化・証券/投資 #1963 のリスクマネー供給サブ業種・#2025・純ロジック・唯一の窓口）：ポートフォリオ評価額＝
    /// Σ(EXIT倍率×投資額)（VC-1）／パワーロー＝1社のホームランが全体を稼ぐ（VC-2）／ヒット率（VC-3）／ファンド倍率＝評価額/ファンド規模（VC-4）。
    /// 大半は0でも上位1社の数十倍リターンが全体を支える＝企業（#1022）の創業に資本を入れる川上。マクロ近似。test-first。
    /// </summary>
    public static class VentureCapitalRules
    {
        /// <summary>ポートフォリオ評価額＝Σ(各社EXIT倍率×投資額)。倍率0＝倒産（投資額が消える）。</summary>
        public static float PortfolioValue(IReadOnlyList<float> exitMultiples, float investmentPerDeal)
        {
            if (exitMultiples == null) return 0f;
            float inv = Mathf.Max(0f, investmentPerDeal);
            float sum = 0f;
            for (int i = 0; i < exitMultiples.Count; i++) sum += Mathf.Max(0f, exitMultiples[i]) * inv;
            return sum;
        }

        /// <summary>パワーロー集中度＝最大EXIT倍率/総倍率（1社が全リターンを稼ぐほど1.0に近い）。総和0以下は0。</summary>
        public static float TopDealContribution(IReadOnlyList<float> exitMultiples)
        {
            if (exitMultiples == null || exitMultiples.Count == 0) return 0f;
            float sum = 0f, max = 0f;
            for (int i = 0; i < exitMultiples.Count; i++)
            {
                float m = Mathf.Max(0f, exitMultiples[i]);
                sum += m;
                if (m > max) max = m;
            }
            return sum <= 0f ? 0f : max / sum;
        }

        /// <summary>ヒット率＝EXIT成功社数/投資社数（大半は失敗＝低ヒット率でもファンドは成立しうる）。総数0以下は0。</summary>
        public static float HitRate(int winners, int totalDeals)
            => totalDeals <= 0 ? 0f : Mathf.Clamp01((float)Mathf.Max(0, winners) / totalDeals);

        /// <summary>ファンド倍率＝ポートフォリオ評価額/ファンド規模（2倍超で成功ファンド）。規模0以下は0。</summary>
        public static float FundMultiple(float portfolioValue, float fundSize)
            => fundSize <= 0f ? 0f : Mathf.Max(0f, portfolioValue) / fundSize;
    }
}
