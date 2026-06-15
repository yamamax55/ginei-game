namespace Ginei
{
    /// <summary>
    /// 製材の純ロジック（VCHAIN-3・#2091）。木材を加工して建材を作る＝投入産出（木材→建材）。
    /// 数量・歩留まりは既存 `ManufacturerRules`#2016 に委譲（材料がボトルネック・歩留まりで良品）＝二重実装しない。test-first。
    /// </summary>
    public static class SawmillRules
    {
        /// <summary>製材の粗産出＝目標と木材で作れる量の小さい方（`ManufacturerRules.ManufacturedOutput`＝材料律速）。</summary>
        public static float GrossMaterials(float targetMaterials, float woodAvailable, float woodPerMaterial)
            => ManufacturerRules.ManufacturedOutput(targetMaterials, woodAvailable, woodPerMaterial);

        /// <summary>良品建材＝粗産出×歩留まり（`ManufacturerRules.GoodUnits`）。</summary>
        public static float MaterialOutput(float grossMaterials, float yieldRate)
            => ManufacturerRules.GoodUnits(grossMaterials, yieldRate);

        /// <summary>消費した木材＝粗産出×1建材あたり木材投入。</summary>
        public static float WoodConsumed(float grossMaterials, float woodPerMaterial)
            => UnityEngine.Mathf.Max(0f, grossMaterials) * UnityEngine.Mathf.Max(0f, woodPerMaterial);
    }
}
