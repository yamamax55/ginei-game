using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>動員ドクトリン（命令型vs市場型）の純ロジック検証（MCN-6 #1395）。既定Params具体値で固定。</summary>
    public class MobilizationDoctrineRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>動員速度＝命令型は立ち上がりが速く、市場型は自発ゆえ遅い（混合型は素のまま）。</summary>
        [Test]
        public void MobilizationSpeed_命令型は速く市場型は遅い()
        {
            // stateCapacity=0.5：命令型 0.5+0.3=0.8／市場型 0.5−0.25=0.25／混合型 0.5
            Assert.AreEqual(0.8f, MobilizationDoctrineRules.MobilizationSpeed(MobilizationDoctrine.命令型, 0.5f), Eps);
            Assert.AreEqual(0.25f, MobilizationDoctrineRules.MobilizationSpeed(MobilizationDoctrine.市場型, 0.5f), Eps);
            Assert.AreEqual(0.5f, MobilizationDoctrineRules.MobilizationSpeed(MobilizationDoctrine.混合型, 0.5f), Eps);
            // 命令型 > 混合型 > 市場型
            Assert.Greater(MobilizationDoctrineRules.MobilizationSpeed(MobilizationDoctrine.命令型, 0.5f),
                           MobilizationDoctrineRules.MobilizationSpeed(MobilizationDoctrine.市場型, 0.5f));
        }

        /// <summary>持久力＝市場型は長期戦に伸び、命令型は統制疲れで息切れする。</summary>
        [Test]
        public void SustainedCapacity_市場型は伸び命令型は息切れ()
        {
            // economicDepth=0.5：市場型 0.5+0.3=0.8／命令型 0.5−0.25=0.25／混合型 0.5
            Assert.AreEqual(0.8f, MobilizationDoctrineRules.SustainedCapacity(MobilizationDoctrine.市場型, 0.5f), Eps);
            Assert.AreEqual(0.25f, MobilizationDoctrineRules.SustainedCapacity(MobilizationDoctrine.命令型, 0.5f), Eps);
            Assert.AreEqual(0.5f, MobilizationDoctrineRules.SustainedCapacity(MobilizationDoctrine.混合型, 0.5f), Eps);
            Assert.Greater(MobilizationDoctrineRules.SustainedCapacity(MobilizationDoctrine.市場型, 0.5f),
                           MobilizationDoctrineRules.SustainedCapacity(MobilizationDoctrine.命令型, 0.5f));
        }

        /// <summary>速度と持久力のトレードオフ＝命令型は正（短期向き）・市場型は負（長期向き）・混合型は0。</summary>
        [Test]
        public void SpeedVsSustainability_命令型は短期向き市場型は長期向き()
        {
            // 命令型 0.3+0.25=0.55／市場型 −(0.25+0.3)=−0.55／混合型 0
            Assert.AreEqual(0.55f, MobilizationDoctrineRules.SpeedVsSustainability(MobilizationDoctrine.命令型), Eps);
            Assert.AreEqual(-0.55f, MobilizationDoctrineRules.SpeedVsSustainability(MobilizationDoctrine.市場型), Eps);
            Assert.AreEqual(0f, MobilizationDoctrineRules.SpeedVsSustainability(MobilizationDoctrine.混合型), Eps);
        }

        /// <summary>強制コスト＝命令型のみ強度比例で嵩み、市場型は自発ゆえゼロ・混合型は半分。</summary>
        [Test]
        public void CoercionCost_命令型だけが強制コストを払う()
        {
            // intensity=0.8：命令型 0.8×0.5=0.4／市場型 0／混合型 0.8×0.5×0.5=0.2
            Assert.AreEqual(0.4f, MobilizationDoctrineRules.CoercionCost(MobilizationDoctrine.命令型, 0.8f), Eps);
            Assert.AreEqual(0f, MobilizationDoctrineRules.CoercionCost(MobilizationDoctrine.市場型, 0.8f), Eps);
            Assert.AreEqual(0.2f, MobilizationDoctrineRules.CoercionCost(MobilizationDoctrine.混合型, 0.8f), Eps);
        }

        /// <summary>市場インセンティブ効率＝市場型は価格シグナルで効率が伸び、命令型は基礎効率止まり。</summary>
        [Test]
        public void MarketIncentiveEfficiency_市場型は価格で効率を引き出す()
        {
            // floor=1−0.5=0.5。priceSignals=1：市場型 0.5+1×0.5=1.0／命令型 0.5／混合型 0.5+1×0.25=0.75
            Assert.AreEqual(1.0f, MobilizationDoctrineRules.MarketIncentiveEfficiency(MobilizationDoctrine.市場型, 1f), Eps);
            Assert.AreEqual(0.5f, MobilizationDoctrineRules.MarketIncentiveEfficiency(MobilizationDoctrine.命令型, 1f), Eps);
            Assert.AreEqual(0.75f, MobilizationDoctrineRules.MarketIncentiveEfficiency(MobilizationDoctrine.混合型, 1f), Eps);
            // 価格シグナル0なら市場型でも基礎効率0.5止まり
            Assert.AreEqual(0.5f, MobilizationDoctrineRules.MarketIncentiveEfficiency(MobilizationDoctrine.市場型, 0f), Eps);
        }

        /// <summary>政体適合＝専制は命令型・民主は市場型が向く（混合型は中庸の政体で最大）。</summary>
        [Test]
        public void DoctrineFitForPolity_専制は命令型民主は市場型()
        {
            // 専制度0.9：命令型 0.9／市場型 0.1／混合型 1−|0.9−0.5|=0.6
            Assert.AreEqual(0.9f, MobilizationDoctrineRules.DoctrineFitForPolity(MobilizationDoctrine.命令型, 0.9f), Eps);
            Assert.AreEqual(0.1f, MobilizationDoctrineRules.DoctrineFitForPolity(MobilizationDoctrine.市場型, 0.9f), Eps);
            Assert.AreEqual(0.6f, MobilizationDoctrineRules.DoctrineFitForPolity(MobilizationDoctrine.混合型, 0.9f), Eps);
            // 民主寄り（専制度0.2）では市場型が命令型を上回る
            Assert.Greater(MobilizationDoctrineRules.DoctrineFitForPolity(MobilizationDoctrine.市場型, 0.2f),
                           MobilizationDoctrineRules.DoctrineFitForPolity(MobilizationDoctrine.命令型, 0.2f));
            // 混合型は中庸の政体（0.5）で最大1.0
            Assert.AreEqual(1.0f, MobilizationDoctrineRules.DoctrineFitForPolity(MobilizationDoctrine.混合型, 0.5f), Eps);
        }

        /// <summary>総動員出力＝短期は速度・長期は持久力が効き、ドクトリンの優劣が戦争の長さで逆転する。</summary>
        [Test]
        public void MobilizationOutput_短期は速度長期は持久が効く()
        {
            // 命令型 speed=0.8 sustain=0.25／市場型 speed=0.25 sustain=0.8（stateCapacity=economicDepth=0.5）
            float cmdSpeed = MobilizationDoctrineRules.MobilizationSpeed(MobilizationDoctrine.命令型, 0.5f);
            float cmdSus = MobilizationDoctrineRules.SustainedCapacity(MobilizationDoctrine.命令型, 0.5f);
            float mktSpeed = MobilizationDoctrineRules.MobilizationSpeed(MobilizationDoctrine.市場型, 0.5f);
            float mktSus = MobilizationDoctrineRules.SustainedCapacity(MobilizationDoctrine.市場型, 0.5f);

            // 短期決戦(warDuration=0)＝速度がそのまま出力：命令型0.8 > 市場型0.25
            float cmdShort = MobilizationDoctrineRules.MobilizationOutput(cmdSpeed, cmdSus, 0f);
            float mktShort = MobilizationDoctrineRules.MobilizationOutput(mktSpeed, mktSus, 0f);
            Assert.AreEqual(0.8f, cmdShort, Eps);
            Assert.Greater(cmdShort, mktShort);

            // 長期持久戦(warDuration=1)＝持久力がそのまま出力：市場型0.8 > 命令型0.25＝優劣が逆転
            float cmdLong = MobilizationDoctrineRules.MobilizationOutput(cmdSpeed, cmdSus, 1f);
            float mktLong = MobilizationDoctrineRules.MobilizationOutput(mktSpeed, mktSus, 1f);
            Assert.AreEqual(0.8f, mktLong, Eps);
            Assert.Greater(mktLong, cmdLong);

            // 中間(warDuration=0.5)＝速度と持久の平均：命令型 (0.8+0.25)/2=0.525
            Assert.AreEqual(0.525f, MobilizationDoctrineRules.MobilizationOutput(cmdSpeed, cmdSus, 0.5f), Eps);
        }

        /// <summary>命令型判定＝速度に振り持久を犠牲にした体制（命令型のみ閾値超え）。</summary>
        [Test]
        public void IsCommandMobilization_命令型のみ閾値を超える()
        {
            // 閾値0.3：命令型0.55>0.3=true／市場型−0.55=false／混合型0=false
            Assert.IsTrue(MobilizationDoctrineRules.IsCommandMobilization(MobilizationDoctrine.命令型, 0.3f));
            Assert.IsFalse(MobilizationDoctrineRules.IsCommandMobilization(MobilizationDoctrine.市場型, 0.3f));
            Assert.IsFalse(MobilizationDoctrineRules.IsCommandMobilization(MobilizationDoctrine.混合型, 0.3f));
        }

        /// <summary>入力クランプ＝範囲外の stateCapacity/intensity でも 0..1 に収まる。</summary>
        [Test]
        public void 入力クランプ_範囲外でも破綻しない()
        {
            // stateCapacity 過大でも命令型は1.0で頭打ち
            Assert.AreEqual(1.0f, MobilizationDoctrineRules.MobilizationSpeed(MobilizationDoctrine.命令型, 5f), Eps);
            // 負入力も0扱い：市場型 0−0.25 はクランプで0
            Assert.AreEqual(0f, MobilizationDoctrineRules.MobilizationSpeed(MobilizationDoctrine.市場型, -1f), Eps);
            // intensity 過大でも命令型コストは coercionCostMax=0.5 で頭打ち
            Assert.AreEqual(0.5f, MobilizationDoctrineRules.CoercionCost(MobilizationDoctrine.命令型, 9f), Eps);
        }
    }
}
