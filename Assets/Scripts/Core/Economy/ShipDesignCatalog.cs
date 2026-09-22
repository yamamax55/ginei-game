using System;

namespace Ginei
{
    /// <summary>艦艇設計画面が提示する標準艦体と技術モジュール。返却値は常に複製し、編集中データと共有しない。</summary>
    public static class ShipDesignCatalog
    {
        private static readonly ShipModule[] modules =
        {
            new ShipModule(ModuleType.主砲, 18f, 16f, 120f, 24f, "重粒子砲"),
            new ShipModule(ModuleType.主砲, 11f, 9f, 75f, 14f, "速射砲"),
            new ShipModule(ModuleType.装甲, 20f, 0f, 95f, 22f, "積層装甲"),
            new ShipModule(ModuleType.機関, 16f, -24f, 110f, 20f, "高出力機関"),
            new ShipModule(ModuleType.シールド, 12f, 18f, 115f, 20f, "偏向シールド"),
            new ShipModule(ModuleType.電子機器, 7f, 7f, 65f, 16f, "統合電子戦装置"),
            new ShipModule(ModuleType.格納庫, 17f, 12f, 105f, 20f, "艦載機格納庫")
        };

        public static ShipModule[] Modules()
        {
            var copy = new ShipModule[modules.Length];
            for (int i = 0; i < modules.Length; i++) copy[i] = Clone(modules[i]);
            return copy;
        }

        public static HullSpec[] HullsFor(ShipClass shipClass)
        {
            switch (shipClass)
            {
                case ShipClass.戦艦:
                    return new[] { new HullSpec(110f, 52f, 6, "戦艦標準船体"), new HullSpec(138f, 60f, 7, "戦艦重船体") };
                case ShipClass.巡航艦:
                    return new[] { new HullSpec(78f, 44f, 5, "巡航艦標準船体"), new HullSpec(66f, 50f, 5, "巡航艦高速船体") };
                default:
                    return new[] { new HullSpec(48f, 34f, 4, "駆逐艦標準船体"), new HullSpec(39f, 39f, 4, "駆逐艦軽船体") };
            }
        }

        public static HullSpec Clone(HullSpec hull)
            => hull == null ? null : new HullSpec(hull.maxWeight, hull.maxPower, hull.slots, hull.hullName);

        public static ShipModule Clone(ShipModule module)
            => module == null ? null : new ShipModule(module.type, module.weight, module.powerDraw,
                module.cost, module.rating, module.moduleName);

        public static string ModuleLabel(ShipModule module)
            => module == null ? "（空き）" : $"{module.moduleName}　{module.type} / 搭載{module.weight:0} / 電力{module.powerDraw:+0;-0;0}";
    }
}
