using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 宇宙建設・テラフォーミングのロジック（業種細分化・建設 #2024 の惑星改造サブ業種・#2025・純ロジック・唯一の窓口）：テラフォーム進捗（TERA-1）／
    /// 居住適性の向上（TERA-2＝改造で惑星が住めるようになる）／工事の請負額（TERA-3）／利益（TERA-4）。
    /// 建設（#2024）の超長期版＝惑星を改造して居住可能化（入植#129/#117の前段）。惑星の抵抗（重力・大気）が進捗を律速し、巨額の資材・エネルギーを要する国家事業。マクロ近似。test-first。
    /// </summary>
    public static class TerraformingRules
    {
        /// <summary>テラフォーム進捗＝投入能力×(1−惑星抵抗)×時間（過酷な惑星ほど抵抗が高く進みが遅い）。</summary>
        public static float TerraformProgress(float investedCapacity, float planetResistance, float dt)
            => Mathf.Max(0f, investedCapacity) * (1f - Mathf.Clamp01(planetResistance)) * Mathf.Max(0f, dt);

        /// <summary>居住適性の向上＝現在の適性+改造産出（上限cap・改造で惑星が住めるようになる→入植#129へ）。</summary>
        public static float HabitabilityGain(float currentHabitability, float terraformOutput, float cap)
            => Mathf.Min(Mathf.Max(0f, cap), Mathf.Max(0f, currentHabitability) + Mathf.Max(0f, terraformOutput));

        /// <summary>工事請負額＝改造面積×面積単価（建設#2024同様の請負＝惑星規模で額が決まる）。</summary>
        public static float ProjectContractValue(float planetArea, float pricePerArea)
            => Mathf.Max(0f, planetArea) * Mathf.Max(0f, pricePerArea);

        /// <summary>テラフォーミング利益＝請負額−資材費−エネルギー費−固定費（巨額の資材#92/エネルギー#2025を要する）。</summary>
        public static float TerraformingProfit(float contractValue, float materialCost, float energyCost, float fixedCost)
            => contractValue - Mathf.Max(0f, materialCost) - Mathf.Max(0f, energyCost) - Mathf.Max(0f, fixedCost);
    }
}
