using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>擬制商品ストレスの純ロジック（POLA-3 #1596）の EditMode テスト。既定 Params の具体値で期待値を固定する。</summary>
    public class FictitiousCommodityRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>種別ごとの商品化ストレス＝労働＞土地＞貨幣の固有感度（既定 0.8/0.7/0.6）が出る。</summary>
        [Test]
        public void CommodificationStress_種別ごとの固有感度()
        {
            // 完全商品化 1.0 で種別感度がそのまま出る
            Assert.AreEqual(0.8f, FictitiousCommodityRules.CommodificationStress(1f, FictitiousCommodity.労働), Eps);
            Assert.AreEqual(0.7f, FictitiousCommodityRules.CommodificationStress(1f, FictitiousCommodity.土地), Eps);
            Assert.AreEqual(0.6f, FictitiousCommodityRules.CommodificationStress(1f, FictitiousCommodity.貨幣), Eps);
            // 商品化度に比例
            Assert.AreEqual(0.4f, FictitiousCommodityRules.CommodificationStress(0.5f, FictitiousCommodity.労働), Eps);
            // 非商品化はゼロ
            Assert.AreEqual(0f, FictitiousCommodityRules.CommodificationStress(0f, FictitiousCommodity.労働), Eps);
        }

        /// <summary>労働の摩耗＝商品化×労働強度の積に労働感度（0.8）＝人間は休息が要る。どちらかゼロなら摩耗なし。</summary>
        [Test]
        public void LaborWear_商品化と労働強度の積で摩耗する()
        {
            // 0.8 * 1.0 * 1.0 = 0.8
            Assert.AreEqual(0.8f, FictitiousCommodityRules.LaborWear(1f, 1f), Eps);
            // 0.8 * 1.0 * 0.5 = 0.4
            Assert.AreEqual(0.4f, FictitiousCommodityRules.LaborWear(1f, 0.5f), Eps);
            // 労働強度ゼロなら摩耗なし
            Assert.AreEqual(0f, FictitiousCommodityRules.LaborWear(1f, 0f), Eps);
            // 商品化ゼロなら摩耗なし
            Assert.AreEqual(0f, FictitiousCommodityRules.LaborWear(0f, 1f), Eps);
        }

        /// <summary>土地の破壊＝商品化×収奪度の積に土地感度（0.7）＝自然・地域が壊れる。</summary>
        [Test]
        public void LandDegradation_商品化と収奪で自然地域が壊れる()
        {
            // 0.7 * 1.0 * 1.0 = 0.7
            Assert.AreEqual(0.7f, FictitiousCommodityRules.LandDegradation(1f, 1f), Eps);
            // 0.7 * 0.5 * 1.0 = 0.35
            Assert.AreEqual(0.35f, FictitiousCommodityRules.LandDegradation(0.5f, 1f), Eps);
            // 収奪ゼロなら破壊なし
            Assert.AreEqual(0f, FictitiousCommodityRules.LandDegradation(1f, 0f), Eps);
        }

        /// <summary>貨幣の不安定＝商品化×投機度の積に貨幣感度（0.6）＝金融不安定を生む。</summary>
        [Test]
        public void MoneyInstability_商品化と投機で金融が不安定化する()
        {
            // 0.6 * 1.0 * 1.0 = 0.6
            Assert.AreEqual(0.6f, FictitiousCommodityRules.MoneyInstability(1f, 1f), Eps);
            // 0.6 * 1.0 * 0.5 = 0.3
            Assert.AreEqual(0.3f, FictitiousCommodityRules.MoneyInstability(1f, 0.5f), Eps);
            // 投機ゼロなら不安定化なし
            Assert.AreEqual(0f, FictitiousCommodityRules.MoneyInstability(1f, 0f), Eps);
        }

        /// <summary>擬制商品ストレスは社会の保護需要（二重運動）を呼ぶ＝感度1.0でストレスがそのまま需要に写る。</summary>
        [Test]
        public void ProtectionDemandFromStress_ストレスが保護需要を呼ぶ()
        {
            // 感度 1.0：ストレスがそのまま需要
            Assert.AreEqual(0.6f, FictitiousCommodityRules.ProtectionDemandFromStress(0.6f), Eps);
            Assert.AreEqual(1f, FictitiousCommodityRules.ProtectionDemandFromStress(1f), Eps);
            // ストレスゼロなら需要なし
            Assert.AreEqual(0f, FictitiousCommodityRules.ProtectionDemandFromStress(0f), Eps);
        }

        /// <summary>商品化ストレスが社会の紐帯を解く＝ストレス×崩し速度(0.5)×dt ぶん紐帯が減る。ストレスゼロなら不変。</summary>
        [Test]
        public void SocialUnravelingTick_ストレスが紐帯を解く()
        {
            // 1.0 - 0.5*0.8*1.0 = 0.6
            Assert.AreEqual(0.6f, FictitiousCommodityRules.SocialUnravelingTick(1f, 0.8f, 1f), Eps);
            // ストレスゼロなら紐帯不変
            Assert.AreEqual(1f, FictitiousCommodityRules.SocialUnravelingTick(1f, 0f, 1f), Eps);
            // 下限クランプ（過剰減で負にならない）
            Assert.AreEqual(0f, FictitiousCommodityRules.SocialUnravelingTick(0.2f, 1f, 1f), Eps);
        }

        /// <summary>脱商品化（保護）がストレスを和らげる＝保護が高いほど実効ストレスが減る。保護ゼロなら緩和なし。</summary>
        [Test]
        public void DecommodificationRelief_保護がストレスを和らげる()
        {
            // 保護ゼロ＝緩和なし＝元のストレスのまま
            Assert.AreEqual(0.6f, FictitiousCommodityRules.DecommodificationRelief(0.6f, 0f, FictitiousCommodity.労働), Eps);
            // 労働・保護1.0：relief = 0.7*0.8*1.0 = 0.56 → 0.6*(1-0.56)=0.264
            Assert.AreEqual(0.264f, FictitiousCommodityRules.DecommodificationRelief(0.6f, 1f, FictitiousCommodity.労働), Eps);
            // 保護が効くほどストレスは小さくなる（単調減）
            float p0 = FictitiousCommodityRules.DecommodificationRelief(0.6f, 0.3f, FictitiousCommodity.労働);
            float p1 = FictitiousCommodityRules.DecommodificationRelief(0.6f, 0.7f, FictitiousCommodity.労働);
            Assert.Less(p1, p0);
        }

        /// <summary>過剰商品化の判定＝商品化水準が既定しきい値(0.8)以上で true。</summary>
        [Test]
        public void IsOvercommodified_しきい値で過剰商品化を判定()
        {
            Assert.IsTrue(FictitiousCommodityRules.IsOvercommodified(0.9f));
            Assert.IsTrue(FictitiousCommodityRules.IsOvercommodified(0.8f)); // 境界は以上で true
            Assert.IsFalse(FictitiousCommodityRules.IsOvercommodified(0.7f));
        }
    }
}
