using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 固定会戦QAの調整プリセット一覧（SPEED-07・純ロジック）。
    /// 既定＋「1項目だけ倍率を変えた」比較用。倍率なので基準値（スクリプト既定）を書き写さない。
    /// ★バランス調整の提案ではない（比較条件の名札）。
    /// </summary>
    public static class BattleQaTuningCatalog
    {
        /// <summary>速度・回頭の比較倍率。</summary>
        public const float MobilityComparisonScale = 1.5f;

        /// <summary>士気回復量の比較倍率。</summary>
        public const float RecoveryComparisonScale = 2f;

        /// <summary>敗走回復待ちの比較倍率。</summary>
        public const float RoutedDelayComparisonScale = 0.5f;

        /// <summary>軍団隊形の最小間隔の比較倍率（実間隔は占有半径の下限を割らない）。</summary>
        public const float CorpsSpacingComparisonScale = 2f;

        /// <summary>1項目だけ倍率を掛けたプリセット（名前＝項目×倍率）。</summary>
        public static BattleQaTuningProfile ScaleOne(BattleQaTuningField field, float scale)
            => BattleQaTuningProfile.Default.With(field + "×" + scale.ToString("0.##"), field, BattleQaTuningOverride.Scale(scale));

        /// <summary>メニューに並べる一覧（先頭＝既定）。並び順は Editor メニューの番号と一致させる。</summary>
        public static List<BattleQaTuningProfile> All()
        {
            return new List<BattleQaTuningProfile>
            {
                BattleQaTuningProfile.Default,
                ScaleOne(BattleQaTuningField.移動速度, MobilityComparisonScale),
                ScaleOne(BattleQaTuningField.回頭速度, MobilityComparisonScale),
                ScaleOne(BattleQaTuningField.士気回復量, RecoveryComparisonScale),
                ScaleOne(BattleQaTuningField.敗走回復待ち, RoutedDelayComparisonScale),
                ScaleOne(BattleQaTuningField.軍団隊形間隔, CorpsSpacingComparisonScale),
            };
        }
    }
}
