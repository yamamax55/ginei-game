using NUnit.Framework;

namespace Ginei.Tests
{
    public class ShipDesignCatalogTests
    {
        [Test]
        public void Catalog_ReturnsIndependentCopiesAndClassSpecificHulls()
        {
            ShipModule[] first = ShipDesignCatalog.Modules();
            ShipModule[] second = ShipDesignCatalog.Modules();
            first[0].rating = 999f;

            Assert.AreNotSame(first[0], second[0]);
            Assert.AreNotEqual(first[0].rating, second[0].rating);
            Assert.Greater(ShipDesignCatalog.HullsFor(ShipClass.戦艦)[0].maxWeight,
                ShipDesignCatalog.HullsFor(ShipClass.駆逐艦)[0].maxWeight);
        }

        [Test]
        public void EveryCatalogModule_HasReadableNameAndPositiveRating()
        {
            foreach (ShipModule module in ShipDesignCatalog.Modules())
            {
                Assert.IsNotEmpty(module.moduleName);
                Assert.Greater(module.rating, 0f);
                StringAssert.Contains(module.moduleName, ShipDesignCatalog.ModuleLabel(module));
            }
        }
    }
}
