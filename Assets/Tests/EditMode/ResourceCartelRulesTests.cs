using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>資源カルテル（戦略資源の供給支配と価格操作）純ロジックの EditMode テスト（既定 Params 期待値固定）。</summary>
    public class ResourceCartelRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void 市場支配は重要度で増幅_重要度ゼロは半減()
        {
            // critFactor=0.5+0.5*criticality。重要度1.0→1.0倍、重要度0.0→0.5倍。
            Assert.AreEqual(0.6f, ResourceCartelRules.MarketControl(0.6f, 1.0f), Eps);
            Assert.AreEqual(0.3f, ResourceCartelRules.MarketControl(0.6f, 0.0f), Eps);
        }

        [Test]
        public void 価格つり上げは支配と減産の積_支配なしは効かない()
        {
            // 0.6*0.5*1.5 = 0.45。
            Assert.AreEqual(0.45f, ResourceCartelRules.PriceManipulation(0.6f, 0.5f), Eps);
            // 支配ゼロなら減産しても無視されて効かない。
            Assert.AreEqual(0f, ResourceCartelRules.PriceManipulation(0.0f, 0.5f), Eps);
        }

        [Test]
        public void 抜け駆けの誘惑は高価格と小割当_満杯割当は誘惑ゼロ()
        {
            // 0.8*(1-0.25)=0.6。
            Assert.AreEqual(0.6f, ResourceCartelRules.CheatingIncentive(0.8f, 0.25f), Eps);
            // 割当満杯なら増産の旨味なし＝誘惑ゼロ。
            Assert.AreEqual(0f, ResourceCartelRules.CheatingIncentive(0.8f, 1.0f), Eps);
        }

        [Test]
        public void 結束は加盟が増えるほど崩れる()
        {
            // 加盟1：sizeFactor=1.0 → 0.8*0.9 = 0.72。
            Assert.AreEqual(0.72f, ResourceCartelRules.CartelCohesion(1, 0.8f, 0.9f), Eps);
            // 加盟5：sizeFactor=1-0.1*4=0.6 → 0.72*0.6 = 0.432。
            Assert.AreEqual(0.432f, ResourceCartelRules.CartelCohesion(5, 0.8f, 0.9f), Eps);
        }

        [Test]
        public void 裏切りリスクは誘惑と結束の弱さの積_完璧な結束はリスクゼロ()
        {
            // 0.6*(1-0.72)=0.168。
            Assert.AreEqual(0.168f, ResourceCartelRules.CheatingRisk(0.6f, 0.72f), Eps);
            // 結束完璧なら誘惑があってもリスクゼロ。
            Assert.AreEqual(0f, ResourceCartelRules.CheatingRisk(0.6f, 1.0f), Eps);
        }

        [Test]
        public void 武器化は支配と政治意志の積()
        {
            // 0.6*0.5*1.0 = 0.3。
            Assert.AreEqual(0.3f, ResourceCartelRules.ResourceWeaponization(0.6f, 0.5f), Eps);
            // 意志なき支配は禁輸を使えない。
            Assert.AreEqual(0f, ResourceCartelRules.ResourceWeaponization(0.6f, 0.0f), Eps);
        }

        [Test]
        public void 需要側対抗は代替と備蓄の加重平均()
        {
            // 代替0.8*0.6 + 備蓄0.5*0.4 = 0.48+0.2 = 0.68。
            Assert.AreEqual(0.68f, ResourceCartelRules.DemandResponse(0.5f, 0.8f), Eps);
        }

        [Test]
        public void 実効力は支配から裏切りと需要対抗を引く_符号付き()
        {
            // 0.6-0.168-0.68 = -0.248（裏切りと需要対抗に押し負け＝機能不全）。
            Assert.AreEqual(-0.248f, ResourceCartelRules.CartelPower(0.6f, 0.168f, 0.68f), Eps);
            // 裏切りも需要対抗も小さければ正＝供給を握れる。
            Assert.AreEqual(0.4f, ResourceCartelRules.CartelPower(0.6f, 0.1f, 0.1f), Eps);
        }

        [Test]
        public void 入力クランプと符号付き出力クランプ()
        {
            // 過大入力でも市場支配は1.0で頭打ち。
            Assert.AreEqual(1.0f, ResourceCartelRules.MarketControl(5f, 5f), Eps);
            // 実効力は-1..1にクランプ（支配0で裏切り・需要が満ちると-1へ）。
            Assert.AreEqual(-1.0f, ResourceCartelRules.CartelPower(0f, 1f, 1f), Eps);
            Assert.AreEqual(1.0f, ResourceCartelRules.CartelPower(1f, 0f, 0f), Eps);
        }

        [Test]
        public void 物語_大所帯カルテルが裏切りと代替技術で瓦解する()
        {
            var p = ResourceCartelParams.Default;
            // 8カ国の大カルテル：シェア0.8・不可欠資源1.0。
            float control = ResourceCartelRules.MarketControl(0.8f, 1.0f, p);
            Assert.AreEqual(0.8f, control, Eps);
            // 減産で価格つり上げ。
            float price = ResourceCartelRules.PriceManipulation(control, 0.5f, p);
            Assert.AreEqual(0.6f, price, Eps); // 0.8*0.5*1.5
            // 小割当の加盟は高値で抜け駆けしたい。
            float incentive = ResourceCartelRules.CheatingIncentive(price, 0.2f, p);
            Assert.AreEqual(0.48f, incentive, Eps); // 0.6*0.8
            // 8カ国は結束が脆い：sizeFactor=1-0.1*7=0.3 → 0.6*0.5*0.3=0.09。
            float cohesion = ResourceCartelRules.CartelCohesion(8, 0.6f, 0.5f, p);
            Assert.AreEqual(0.09f, cohesion, Eps);
            // 誘惑大×結束弱＝高い裏切りリスク。
            float risk = ResourceCartelRules.CheatingRisk(incentive, cohesion, p);
            Assert.AreEqual(0.4368f, risk, Eps); // 0.48*0.91
            // 需要側は備蓄と代替技術で対抗。
            float demand = ResourceCartelRules.DemandResponse(0.7f, 0.9f, p);
            Assert.AreEqual(0.82f, demand, Eps); // 0.9*0.6+0.7*0.4
            // 実効力は負＝カルテルは瓦解（裏切りと代替に押し負ける）。
            float power = ResourceCartelRules.CartelPower(control, risk, demand);
            Assert.AreEqual(-0.4568f, power, Eps); // 0.8-0.4368-0.82
            Assert.Less(power, 0f);
        }
    }
}
