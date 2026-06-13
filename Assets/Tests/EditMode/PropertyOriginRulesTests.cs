using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 労働財産論と先占権（#1447 LOCK-1 ロックの労働所有論）を固定する：自然はコモンズだが労働を加えたものは私有になり
    /// （労働の混合→請求権）、他人に十分残し（但し書き）腐らせない限り（腐敗の制限）正当、貨幣がその制限を超えて
    /// 無制限蓄積を可能にする。境界・クランプ・決定論。
    /// </summary>
    public class PropertyOriginRulesTests
    {
        private static PropertyOriginParams P => PropertyOriginParams.Default;

        // --- LaborMixing：労働の混合が財産の源泉 ---

        [Test]
        public void LaborMixing_LaborAddsValueToNature()
        {
            // 自然物0.8に労働0.6を混ぜると価値0.48＝労働の混合が私有の素を生む
            Assert.AreEqual(0.48f, PropertyOriginRules.LaborMixing(0.6f, 0.8f), 1e-4f);
            // 労働ゼロなら手つかずの自然＝私有の根拠なし
            Assert.AreEqual(0f, PropertyOriginRules.LaborMixing(0f, 1f), 1e-4f);
        }

        // --- ClaimStrength：耕した者が持つ ---

        [Test]
        public void ClaimStrength_LaborTimesOccupation()
        {
            // 労働0.5×占有継続1.0：0.5*0.6 + 0.5*1.0*0.4 = 0.5（耕し続けてこそ強い請求権）
            Assert.AreEqual(0.5f, PropertyOriginRules.ClaimStrength(0.5f, 1f, P), 1e-4f);
            // 占有が無ければ労働分だけの弱い請求権：0.5*0.6 = 0.3
            Assert.AreEqual(0.3f, PropertyOriginRules.ClaimStrength(0.5f, 0f, P), 1e-4f);
        }

        // --- LockeanProviso：他人にも十分残されている限り ---

        [Test]
        public void LockeanProviso_EnoughLeftForOthers_Legitimate_HoardingUnjust()
        {
            // 取り分0.2・残余0.5（十分閾値）＝1.0*(1-0.2)=0.8 正当
            Assert.AreEqual(0.8f, PropertyOriginRules.LockeanProviso(0.2f, 0.5f, P), 1e-4f);
            // 独り占め（取り分0.8・残余0.1僅少）＝0.2*(1-0.8)=0.04 不当
            Assert.AreEqual(0.04f, PropertyOriginRules.LockeanProviso(0.8f, 0.1f, P), 1e-4f);
        }

        // --- SpoilageLimit：腐らせない限り ---

        [Test]
        public void SpoilageLimit_HoardingBeyondUseIsUnjust()
        {
            // 蓄積0.8を使用0.3でしか消費できない＝0.5腐らせる→正当性0.5
            Assert.AreEqual(0.5f, PropertyOriginRules.SpoilageLimit(0.8f, 0.3f), 1e-4f);
            // 使い切れる範囲（蓄積0.3<使用0.8）なら腐らせず満点
            Assert.AreEqual(1f, PropertyOriginRules.SpoilageLimit(0.3f, 0.8f), 1e-4f);
        }

        // --- CommonsToPrivate：労働による囲い込み ---

        [Test]
        public void CommonsToPrivate_LaborEnclosesCommons()
        {
            // コモンズ1.0・労働1.0・dt1.0で 1*1*0.5*1=0.5 が私有へ転化→残るコモンズ0.5
            Assert.AreEqual(0.5f, PropertyOriginRules.CommonsToPrivate(1f, 1f, 1f, P), 1e-4f);
            // dtゼロなら転化なし（決定論・非破壊）
            Assert.AreEqual(1f, PropertyOriginRules.CommonsToPrivate(1f, 1f, 0f, P), 1e-4f);
        }

        // --- FirstOccupancyRight：先に来て開墾した者 ---

        [Test]
        public void FirstOccupancyRight_EarlyArrivalPlusImprovement()
        {
            // 到着順1.0（最早）×改良0.5：1*0.5 + 0.5*0.5 = 0.75（早い者勝ち＋改良）
            Assert.AreEqual(0.75f, PropertyOriginRules.FirstOccupancyRight(1f, 0.5f, P), 1e-4f);
            // 遅参でも改良1.0なら改良分だけ先占権：0 + 1.0*0.5 = 0.5
            Assert.AreEqual(0.5f, PropertyOriginRules.FirstOccupancyRight(0f, 1f, P), 1e-4f);
        }

        // --- MoneyTranscendsSpoilage：貨幣が無制限蓄積を正当化 ---

        [Test]
        public void MoneyTranscendsSpoilage_MoneyUnlocksUnlimitedAccumulation()
        {
            // 貨幣ゼロ＝腐敗の制限内（最大1）に収まる
            Assert.AreEqual(1f, PropertyOriginRules.MoneyTranscendsSpoilage(0f, 5f), 1e-4f);
            // 貨幣導入＝腐る制限を超えて蓄積5がそのまま正当化（ロック＝貨幣が不平等を正当化）
            Assert.AreEqual(5f, PropertyOriginRules.MoneyTranscendsSpoilage(1f, 5f), 1e-4f);
        }

        // --- IsLegitimateAppropriation：労働と但し書きの両立 ---

        [Test]
        public void IsLegitimateAppropriation_RequiresBothLaborAndProviso()
        {
            // 請求権0.6・但し書き0.7とも閾値0.5以上＝正当な私有化
            Assert.IsTrue(PropertyOriginRules.IsLegitimateAppropriation(0.6f, 0.7f, 0.5f));
            // 但し書きを満たしても労働（請求権0.4）が閾値未満なら不当＝両立が必要
            Assert.IsFalse(PropertyOriginRules.IsLegitimateAppropriation(0.4f, 0.7f, 0.5f));
        }
    }
}
