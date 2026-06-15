using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 電気機器/半導体メーカーのロジック（東証33業種「電気機器」・#2024・純ロジック・唯一の窓口）：シリコンサイクル＝需給で
    /// 価格乱高下（ELC-1）／微細化世代の優位（ELC-2・ムーアの法則）／巨額設備の循環利益（ELC-3）／製品ライフサイクルが短く価格が
    /// 急落（ELC-4）。製造は <see cref="ManufacturerRules"/>(#2016)、装置は工作機械(#2023)へ接続。マクロ近似。test-first。
    /// </summary>
    public static class ElectronicsRules
    {
        /// <summary>チップ価格＝基準価格×(需要/供給)（半導体は需給で激しく上下＝シリコンサイクルの feast/famine）。供給0以下は超高。</summary>
        public static float ChipPrice(float demand, float supply, float basePrice)
            => supply <= 0f ? 999999f : Mathf.Max(0f, basePrice) * (Mathf.Max(0f, demand) / supply);

        /// <summary>世代優位＝自社プロセス世代−競合世代（プラスは先端＝コスト/性能で優位、マイナスは周回遅れ）。</summary>
        public static float GenerationAdvantage(float ownNodeLevel, float competitorNodeLevel)
            => ownNodeLevel - competitorNodeLevel;

        /// <summary>価格下落＝発売価格×max(下限, 1−経過月×月あたり下落率)（製品寿命が短くコモディティ化で価格が急落）。</summary>
        public static float PriceErosion(float launchPrice, float monthsSinceLaunch, float erosionPerMonth, float floor)
            => Mathf.Max(0f, launchPrice) * Mathf.Max(Mathf.Clamp01(floor), 1f - Mathf.Max(0f, monthsSinceLaunch) * Mathf.Max(0f, erosionPerMonth));

        /// <summary>シリコンサイクル利益＝数量×(価格−単価)−固定費（巨額固定費＝好況で爆益・不況で大赤字）。</summary>
        public static float SiliconCycleProfit(float price, float unitCost, float volume, float fixedCost)
            => Mathf.Max(0f, volume) * (price - unitCost) - Mathf.Max(0f, fixedCost);
    }
}
