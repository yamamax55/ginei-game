using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// CarrierStrikeRules（空母打撃＝艦載機/スパルタニアン）の純ロジック検証。
    /// 既定 Params で期待値を固定（Pow を使わないので厳密一致・許容 1e-4）。
    /// </summary>
    public class CarrierStrikeRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void SortieCapacity_CarriersTimesDeck()
        {
            // 空母3隻×甲板40＝120機を一度に出せる。
            Assert.AreEqual(120, CarrierStrikeRules.SortieCapacity(3, 40));
            // 負入力はクランプして0。
            Assert.AreEqual(0, CarrierStrikeRules.SortieCapacity(-2, 40));
            Assert.AreEqual(0, CarrierStrikeRules.SortieCapacity(3, -5));
        }

        [Test]
        public void StrikeReach_StandoffPlusFighterRange()
        {
            // 空母離隔50＋艦載機航続30＝打撃距離80。
            Assert.AreEqual(80f, CarrierStrikeRules.StrikeReach(50f, 30f), Eps);
        }

        [Test]
        public void StrikeDamage_CutByEnemyPointDefense()
        {
            // 対空ゼロ＝素通し：100機×2×技量1＝200。
            Assert.AreEqual(200f, CarrierStrikeRules.StrikeDamage(100, 1f, 0f), Eps);
            // 対空1：cut=0.8*(1/2)=0.4、raw=100*2*1.5=300 → 300*0.6=180。
            Assert.AreEqual(180f, CarrierStrikeRules.StrikeDamage(100, 1.5f, 1f), Eps);
        }

        [Test]
        public void FighterAttrition_RisesWithPointDefense()
        {
            // 対空ゼロ＝撃墜なし。
            Assert.AreEqual(0, CarrierStrikeRules.FighterAttrition(100, 0f));
            // 対空1：ratio=0.6*(1/2)=0.3 → floor(100*0.3)=30。
            Assert.AreEqual(30, CarrierStrikeRules.FighterAttrition(100, 1f));
            // 対空3：ratio=0.6*(3/4)=0.45 → floor(45)=45。
            Assert.AreEqual(45, CarrierStrikeRules.FighterAttrition(100, 3f));
        }

        [Test]
        public void SortieCycleTime_DeckLoadAndEfficiency()
        {
            // 甲板40：deckLoad=1+40*0.05=3、効率1 → 30*3/1=90秒。
            Assert.AreEqual(90f, CarrierStrikeRules.SortieCycleTime(40, 1f), Eps);
            // 効率2倍で半減 → 45秒。
            Assert.AreEqual(45f, CarrierStrikeRules.SortieCycleTime(40, 2f), Eps);
        }

        [Test]
        public void CarrierVulnerability_RawOverRawPlusEscort()
        {
            // 100機×0.01=1、護衛0：1/(1+1+0)=0.5。
            Assert.AreEqual(0.5f, CarrierStrikeRules.CarrierVulnerability(100, 0f), Eps);
            // 護衛8で手薄が補われる：1/(1+1+8)=0.1。
            Assert.AreEqual(0.1f, CarrierStrikeRules.CarrierVulnerability(100, 8f), Eps);
            // 発艦ゼロなら脆さなし。
            Assert.AreEqual(0f, CarrierStrikeRules.CarrierVulnerability(0, 5f), Eps);
        }

        [Test]
        public void StandoffAdvantageAndDominance()
        {
            float adv = CarrierStrikeRules.StandoffAdvantage(80f, 50f);
            Assert.AreEqual(30f, adv, Eps);
            Assert.IsTrue(CarrierStrikeRules.IsCarrierDominant(adv, 20f));
            Assert.IsFalse(CarrierStrikeRules.IsCarrierDominant(adv, 40f));
            // 相手が射程で上回ればアウトレンジ優位は負。
            Assert.AreEqual(-20f, CarrierStrikeRules.StandoffAdvantage(50f, 70f), Eps);
        }

        [Test]
        public void Story_OutrangeStrikeButFightersBleedAndCarrierGoesThin()
        {
            // 物語：空母は本艦射程外から艦載機で一方的に叩くが、艦載機は損耗し、空母自身は手薄になる。
            float reach = CarrierStrikeRules.StrikeReach(50f, 30f);   // 80
            float enemyReach = 50f;                                   // 敵の砲射程
            float adv = CarrierStrikeRules.StandoffAdvantage(reach, enemyReach); // 30
            Assert.IsTrue(CarrierStrikeRules.IsCarrierDominant(adv, 20f), "射程外から安全に叩ける＝支配的");

            int launched = CarrierStrikeRules.SortieCapacity(3, 40);  // 120機発艦
            Assert.AreEqual(120, launched);

            // 敵対空1で叩く：技量1.2、cut=0.4、raw=120*2*1.2=288 → 288*0.6=172.8。
            float dmg = CarrierStrikeRules.StrikeDamage(launched, 1.2f, 1f);
            Assert.AreEqual(172.8f, dmg, Eps);

            // それでも艦載機は損耗する：floor(120*0.3)=36機喪失。
            int lost = CarrierStrikeRules.FighterAttrition(launched, 1f);
            Assert.AreEqual(36, lost);
            Assert.Greater(lost, 0, "一方的でも消耗はある");

            // 全機出すと空母は手薄。護衛が無いより有る方が脆さは下がる。
            float thinNoEscort = CarrierStrikeRules.CarrierVulnerability(launched, 0f);
            float thinWithEscort = CarrierStrikeRules.CarrierVulnerability(launched, 10f);
            Assert.Greater(thinNoEscort, thinWithEscort, "護衛で空母の脆さを補える");
        }
    }
}
