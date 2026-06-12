using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 足し高の制（#1975・<see cref="TashidakaRules"/>）を固定する：足し高と実効俸禄、就任資格（俸禄の壁の撤廃）、
    /// 国庫の負担と節約（在職限定＜恒久昇禄）、階級版（俸給 #1969 接続）。
    /// </summary>
    public class TashidakaTests
    {
        [Test]
        public void SupplementAndEffectiveStipend()
        {
            // 家禄500の人材を役高2000の役職へ＝足し高1500、在職中は実効2000
            Assert.AreEqual(1500f, TashidakaRules.Supplement(500f, 2000f), 1e-3f);
            Assert.AreEqual(2000f, TashidakaRules.EffectiveStipend(500f, 2000f), 1e-3f);
            // 家禄が役高を超えるなら足し高なし
            Assert.AreEqual(0f, TashidakaRules.Supplement(2000f, 500f), 1e-3f);
            Assert.AreEqual(2000f, TashidakaRules.EffectiveStipend(2000f, 500f), 1e-3f);
            // 退任後は家禄に戻る（恒久的に上がらない）
            Assert.AreEqual(500f, TashidakaRules.RevertedStipend(500f), 1e-3f);
        }

        [Test]
        public void ServiceEligibility_StipendWallRemoved()
        {
            // 旧制：低禄の俊英は役高に届かず排除される
            Assert.IsFalse(TashidakaRules.CanServeTraditional(500f, 2000f));
            Assert.IsTrue(TashidakaRules.ExcludedByStipend(500f, 2000f));
            // 足し高の制：俸禄の壁が外れ就任できる（ゲートは実力へ移る）
            Assert.IsTrue(TashidakaRules.CanServeWithTashidaka(500f, 2000f));
            // 家禄が足りていれば旧制でも就任可・排除されない
            Assert.IsTrue(TashidakaRules.CanServeTraditional(2000f, 2000f));
            Assert.IsFalse(TashidakaRules.ExcludedByStipend(2000f, 2000f));
        }

        [Test]
        public void TreasuryCost_TenureLimitedIsCheaperThanPermanent()
        {
            var a = new TashidakaAppointment(500f, 2000f); // 足し高1500
            Assert.AreEqual(15000f, TashidakaRules.TenureSupplementCost(a, 10f), 1e-2f);  // 在職10年
            Assert.AreEqual(45000f, TashidakaRules.PermanentRaiseCost(a, 30f), 1e-2f);    // 恒久昇禄30年
            // 退任後の負担を負わないぶん安い＝1500×(30−10)
            Assert.AreEqual(30000f, TashidakaRules.Savings(a, 10f, 30f), 1e-2f);
            // 複数任用の足し高総額
            var list = new List<TashidakaAppointment>
            {
                new TashidakaAppointment(500f, 2000f),  // 1500
                new TashidakaAppointment(1000f, 2000f), // 1000
            };
            Assert.AreEqual(2500f, TashidakaRules.TotalSupplementCost(list), 1e-3f);
            Assert.AreEqual(0f, TashidakaRules.TotalSupplementCost(null), 1e-4f);
        }

        [Test]
        public void TierVersion_BridgesHereditaryAndOfficeRanks()
        {
            var s = PayScale.Default; // 基本俸10/ステップ0.5 → tier5=30, tier8=45
            Assert.AreEqual(45f, TashidakaRules.OfficeStipend(8, s), 1e-3f);
            Assert.AreEqual(30f, TashidakaRules.HereditaryStipend(5, s), 1e-3f);
            // 家禄tier5の人材を役高tier8の役職へ＝足し高15
            Assert.AreEqual(15f, TashidakaRules.SupplementForTier(5, 8, s), 1e-3f);
            // 家禄が役職以上なら足し高なし
            Assert.AreEqual(0f, TashidakaRules.SupplementForTier(8, 5, s), 1e-3f);
        }
    }
}
