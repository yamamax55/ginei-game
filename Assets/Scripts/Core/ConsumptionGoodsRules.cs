using UnityEngine;

namespace Ginei
{
    /// <summary>POP が要求する消費財のカテゴリ（POPDEM-1・#2042・少数階層＝タイクン回避）。マズロー#403 に対応：必需=生理/安全・快適=所属/承認・奢侈=自己実現/自己超越。</summary>
    public enum ConsumptionCategory { 必需, 快適, 奢侈 }

    /// <summary>
    /// 要求物資カテゴリの基盤（POPDEM-1・#2042・純ロジック・唯一の窓口）。
    /// 1人当たり需要原単位（必需は高く硬直・上位財は低く弾力的）・所得弾力性・マズロー欲求段（#403 NeedLevel）への対応。
    /// 既存 `ResourceType{物資/弾薬/燃料}`（軍事寄り）とは別レイヤー＝POP消費財。集約・lookup。test-first。
    /// </summary>
    public static class ConsumptionGoodsRules
    {
        /// <summary>1人当たり需要原単位（必需1.0・快適0.5・奢侈0.2）。必需ほど多く要る。</summary>
        public static float PerCapitaDemand(ConsumptionCategory c)
        {
            switch (c)
            {
                case ConsumptionCategory.必需: return 1.0f;
                case ConsumptionCategory.快適: return 0.5f;
                default:                       return 0.2f; // 奢侈
            }
        }

        /// <summary>所得弾力性（必需0.1＝硬直・快適0.6・奢侈1.0＝所得で大きく動く）。購買力#1969 への反応度。</summary>
        public static float IncomeElasticity(ConsumptionCategory c)
        {
            switch (c)
            {
                case ConsumptionCategory.必需: return 0.1f;
                case ConsumptionCategory.快適: return 0.6f;
                default:                       return 1.0f; // 奢侈
            }
        }

        /// <summary>必需（食料・生活物資）か＝不足が飢餓に直結。</summary>
        public static bool IsNecessity(ConsumptionCategory c) => c == ConsumptionCategory.必需;

        /// <summary>マズロー欲求段（#403 NeedLevel）への対応＝低位/高位の2段のインデックス。必需→生理/安全、快適→所属/承認、奢侈→自己実現/自己超越。</summary>
        public static int[] NeedTiers(ConsumptionCategory c)
        {
            switch (c)
            {
                case ConsumptionCategory.必需: return new[] { (int)NeedLevel.生理, (int)NeedLevel.安全 };
                case ConsumptionCategory.快適: return new[] { (int)NeedLevel.所属, (int)NeedLevel.承認 };
                default:                       return new[] { (int)NeedLevel.自己実現, (int)NeedLevel.自己超越 };
            }
        }
    }
}
