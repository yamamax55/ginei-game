using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>政体規模適合（ルソー『社会契約論』・ROUS-3 #1466）の純ロジック検証。</summary>
    public class PolityScaleRulesTests
    {
        private const float Eps = 0.0001f;

        /// <summary>国家規模＝版図×人口（既定は等重み）。両方最大で1・最小で0・半々で0.5。</summary>
        [Test]
        public void StateScale_版図と人口を等重みで合成する()
        {
            Assert.AreEqual(1f, PolityScaleRules.StateScale(1f, 1f), Eps);
            Assert.AreEqual(0f, PolityScaleRules.StateScale(0f, 0f), Eps);
            // (0.8*0.5 + 0.2*0.5) / 1.0 = 0.5
            Assert.AreEqual(0.5f, PolityScaleRules.StateScale(0.8f, 0.2f), Eps);
        }

        /// <summary>規模に適した政体＝小国は民主政・中規模は貴族政・大国は君主政（ルソー）。</summary>
        [Test]
        public void OptimalFormForScale_規模に応じて民主貴族君主を選ぶ()
        {
            Assert.AreEqual(PolityForm.民主政, PolityScaleRules.OptimalFormForScale(0.1f));
            Assert.AreEqual(PolityForm.貴族政, PolityScaleRules.OptimalFormForScale(0.5f));
            Assert.AreEqual(PolityForm.君主政, PolityScaleRules.OptimalFormForScale(0.9f));
        }

        /// <summary>適合度＝政体の最適規模との距離が近いほど高い。民主政は小国で1・大国で低い。君主政は逆。</summary>
        [Test]
        public void ScaleFormFit_政体と規模の適合を距離で測る()
        {
            // 民主政の最適規模0：小国0でぴったり=1
            Assert.AreEqual(1f, PolityScaleRules.ScaleFormFit(PolityForm.民主政, 0f), Eps);
            // 民主政で大国1.0：距離1、1-1^2=0
            Assert.AreEqual(0f, PolityScaleRules.ScaleFormFit(PolityForm.民主政, 1f), Eps);
            // 君主政の最適規模1.0：大国1.0でぴったり=1
            Assert.AreEqual(1f, PolityScaleRules.ScaleFormFit(PolityForm.君主政, 1f), Eps);
            // 君主政で小国0：距離1、適合0
            Assert.AreEqual(0f, PolityScaleRules.ScaleFormFit(PolityForm.君主政, 0f), Eps);
            // 民主政で規模0.5：距離0.5、1-0.25=0.75
            Assert.AreEqual(0.75f, PolityScaleRules.ScaleFormFit(PolityForm.民主政, 0.5f), Eps);
        }

        /// <summary>ミスマッチペナルティ＝(1-適合度)×上限0.6。適合1.0なら無罰・適合0なら最大。</summary>
        [Test]
        public void MismatchPenalty_適合が低いほど統治効率を下げる()
        {
            Assert.AreEqual(0f, PolityScaleRules.MismatchPenalty(1f), Eps);
            Assert.AreEqual(0.6f, PolityScaleRules.MismatchPenalty(0f), Eps);
            // (1-0.75)*0.6 = 0.15
            Assert.AreEqual(0.15f, PolityScaleRules.MismatchPenalty(0.75f), Eps);
        }

        /// <summary>一般意志の希薄化＝規模×上限0.8。小国は希薄化なし・大国ほど市民が遠い。</summary>
        [Test]
        public void GeneralWillDilution_大国ほど一般意志が薄れる()
        {
            Assert.AreEqual(0f, PolityScaleRules.GeneralWillDilution(0f), Eps);
            Assert.AreEqual(0.8f, PolityScaleRules.GeneralWillDilution(1f), Eps);
            Assert.AreEqual(0.4f, PolityScaleRules.GeneralWillDilution(0.5f), Eps);
        }

        /// <summary>直接参加の実現性＝1-規模。小国ほど直接民主が可能・大国は代表制が要る。</summary>
        [Test]
        public void DirectParticipationFeasibility_小国ほど直接参加できる()
        {
            Assert.AreEqual(1f, PolityScaleRules.DirectParticipationFeasibility(0f), Eps);
            Assert.AreEqual(0f, PolityScaleRules.DirectParticipationFeasibility(1f), Eps);
            Assert.AreEqual(0.3f, PolityScaleRules.DirectParticipationFeasibility(0.7f), Eps);
        }

        /// <summary>防衛力＝下限0.2＋規模×0.8。小国は弱い（ルソーのジレンマ）・大国は強い。</summary>
        [Test]
        public void DefensiveStrength_大国ほど防衛に強く小国は弱い()
        {
            // 小国0：下限0.2
            Assert.AreEqual(0.2f, PolityScaleRules.DefensiveStrength(0f), Eps);
            // 大国1.0：0.2 + 1*0.8 = 1.0
            Assert.AreEqual(1f, PolityScaleRules.DefensiveStrength(1f), Eps);
            // 規模0.5：0.2 + 0.5*0.8 = 0.6
            Assert.AreEqual(0.6f, PolityScaleRules.DefensiveStrength(0.5f), Eps);
            // 小国の防衛 < 大国の防衛（ジレンマ）
            Assert.Less(PolityScaleRules.DefensiveStrength(0.1f), PolityScaleRules.DefensiveStrength(0.9f));
        }

        /// <summary>専制化リスク＝規模×希薄化×上限0.7。大国かつ一般意志が薄いとき高い・小国は0。</summary>
        [Test]
        public void ScaleDespotismRisk_大国で希薄化すると専制に傾く()
        {
            // 小国0：規模0でリスク0
            Assert.AreEqual(0f, PolityScaleRules.ScaleDespotismRisk(0f, 0.8f), Eps);
            // 希薄化0：市民が近ければリスク0
            Assert.AreEqual(0f, PolityScaleRules.ScaleDespotismRisk(1f, 0f), Eps);
            // 大国1.0×希薄化0.8×0.7 = 0.56
            Assert.AreEqual(0.56f, PolityScaleRules.ScaleDespotismRisk(1f, 0.8f), Eps);
        }

        /// <summary>規模適合判定＝適合度が閾値以上なら健全。ちぐはぐな政体は非効率と判定。</summary>
        [Test]
        public void IsScaleAppropriate_適合度が閾値以上で健全()
        {
            Assert.IsTrue(PolityScaleRules.IsScaleAppropriate(0.8f, 0.6f));
            Assert.IsFalse(PolityScaleRules.IsScaleAppropriate(0.4f, 0.6f));
            // 君主政の大国はぴったり＝適合1.0で健全
            float fit = PolityScaleRules.ScaleFormFit(PolityForm.君主政, 1f);
            Assert.IsTrue(PolityScaleRules.IsScaleAppropriate(fit, 0.6f));
            // 民主政の大国はちぐはぐ＝適合0で不健全
            float mismatchFit = PolityScaleRules.ScaleFormFit(PolityForm.民主政, 1f);
            Assert.IsFalse(PolityScaleRules.IsScaleAppropriate(mismatchFit, 0.6f));
        }
    }
}
