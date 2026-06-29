using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>艦隊決戦主義ドクトリン純ロジックの EditMode テスト。既定 Params の具体値で期待値を固定。</summary>
    public class DecisiveBattleDoctrineRulesTests
    {
        const float Eps = 1e-4f;
        const float PowEps = 1e-3f;

        // 決戦を求める誘因＝戦力優位×時間圧力の重み付き平均（既定 0.6/0.4）。
        [TestCase(0.8f, 0.6f, 0.72f)]   // (0.6*0.8 + 0.4*0.6)/1.0 = 0.72
        [TestCase(1f, 1f, 1f)]          // 完全優位＋最大圧力
        [TestCase(0f, 0f, 0f)]          // 優位なし・圧力なし
        public void DecisiveBattleSeek_WeightedAverage(float adv, float pres, float expected)
        {
            Assert.AreEqual(expected, DecisiveBattleDoctrineRules.DecisiveBattleSeek(adv, pres), Eps);
        }

        [Test]
        public void DecisiveBattleSeek_ClampsInputs()
        {
            // 範囲外は 0..1 にクランプ＝(0.6*1 + 0.4*1)/1 = 1
            Assert.AreEqual(1f, DecisiveBattleDoctrineRules.DecisiveBattleSeek(5f, 9f), Eps);
            Assert.AreEqual(0f, DecisiveBattleDoctrineRules.DecisiveBattleSeek(-3f, -1f), Eps);
        }

        // 主力の集結＝集中度×動員完了の積。どちらか 0 で 0。
        [TestCase(0.9f, 0.8f, 0.72f)]
        [TestCase(1f, 0f, 0f)]          // 集中していても動員未了なら主力たり得ない
        [TestCase(0f, 1f, 0f)]
        public void ForceMassing_Product(float conc, float mob, float expected)
        {
            Assert.AreEqual(expected, DecisiveBattleDoctrineRules.ForceMassing(conc, mob), Eps);
        }

        // 決戦の機会＝敵の隙×自軍の即応の積。
        [TestCase(0.7f, 0.6f, 0.42f)]
        [TestCase(0f, 1f, 0f)]          // 隙があっても即応できねば会戦は起きない
        public void BattleOpportunity_Product(float exp, float rdy, float expected)
        {
            Assert.AreEqual(expected, DecisiveBattleDoctrineRules.BattleOpportunity(exp, rdy), Eps);
        }

        // 漸減作戦の妥当性＝敵の増勢×自軍の持久の幾何平均。
        [TestCase(0.64f, 0.81f, 0.72f)] // sqrt(0.64*0.81)=sqrt(0.5184)=0.72
        [TestCase(1f, 0f, 0f)]          // 持久できねば漸減は破綻
        [TestCase(1f, 1f, 1f)]
        public void AttritionAlternative_GeometricMean(float grow, float stay, float expected)
        {
            Assert.AreEqual(expected, DecisiveBattleDoctrineRules.AttritionAlternative(grow, stay), PowEps);
        }

        // 決戦の博打性＝(戦力投入×不確実)^博打鋭さ(既定1.5)。
        [Test]
        public void BattleGambleRisk_SharpenedProduct()
        {
            // pow(0.5*0.5, 1.5) = pow(0.25, 1.5) = 0.125
            Assert.AreEqual(0.125f, DecisiveBattleDoctrineRules.BattleGambleRisk(0.5f, 0.5f), PowEps);
            // 投入なし＝賭けではない
            Assert.AreEqual(0f, DecisiveBattleDoctrineRules.BattleGambleRisk(0f, 1f), PowEps);
            // 全投入＋全不確実＝最大の賭け
            Assert.AreEqual(1f, DecisiveBattleDoctrineRules.BattleGambleRisk(1f, 1f), PowEps);
        }

        // 決戦勝利の戦略効果＝敵主力撃滅×戦争疲弊×係数(既定1.0)。
        [TestCase(0.8f, 0.75f, 0.6f)]   // 0.8*0.75*1.0 = 0.6
        [TestCase(1f, 0f, 0f)]          // 敵に余力があれば戦略的決着に至らない
        public void DecisiveVictoryPayoff_Product(float destroyed, float exhaust, float expected)
        {
            Assert.AreEqual(expected, DecisiveBattleDoctrineRules.DecisiveVictoryPayoff(destroyed, exhaust), Eps);
        }

        // 壊滅的敗北リスク＝博打性×代替不能の積。
        [TestCase(0.5f, 0.6f, 0.3f)]
        [TestCase(0.9f, 0f, 0f)]        // いくらでも補充できれば壊滅には至らない
        public void CatastrophicDefeatRisk_Product(float gamble, float irr, float expected)
        {
            Assert.AreEqual(expected, DecisiveBattleDoctrineRules.CatastrophicDefeatRisk(gamble, irr), Eps);
        }

        // ドクトリンの妥当性＝機会×勝利効果 − 壊滅リスク×ペナルティ重み(既定1.0)。-1..1。
        [Test]
        public void DoctrineSoundness_UpsideMinusDownside()
        {
            // 0.42*0.6 - 0.3*1.0 = 0.252 - 0.3 = -0.048（機会が乏しくリスクが上回る＝決戦は割に合わぬ）
            Assert.AreEqual(-0.048f, DecisiveBattleDoctrineRules.DoctrineSoundness(0.42f, 0.6f, 0.3f), Eps);
            // 好機十分・勝てば決定的・リスク皆無＝強く決戦を是とする
            Assert.AreEqual(0.81f, DecisiveBattleDoctrineRules.DoctrineSoundness(0.9f, 0.9f, 0f), Eps);
            // 機会なし・壊滅リスク最大＝下限へクランプ
            Assert.AreEqual(-1f, DecisiveBattleDoctrineRules.DoctrineSoundness(0f, 0f, 1f), Eps);
        }

        [Test]
        public void DoctrineSoundness_OutputIsSigned()
        {
            float s = DecisiveBattleDoctrineRules.DoctrineSoundness(0.5f, 0.5f, 0.9f);
            // 0.25 - 0.9 = -0.65（負＝決戦を避けるべき）
            Assert.AreEqual(-0.65f, s, Eps);
            Assert.GreaterOrEqual(s, -1f);
            Assert.LessOrEqual(s, 1f);
        }

        // 物語テスト：イゼルローン回廊での一大会戦シナリオ。
        // 戦力優位を握り（速戦を求める時間圧力もある）→主力を集結→敵が隙を見せ即応できる好機が訪れる。
        // 主力を賭ける博打だが、敵主力を撃滅でき相手が疲弊していれば戦争を一気に決する。
        // 戦力が代替不能（補充困難）でも、機会と勝利効果が壊滅リスクを上回れば決戦は妥当となる。
        [Test]
        public void Narrative_GrandFleetEngagement_FavorsDecisiveBattle()
        {
            var p = DecisiveBattleDoctrineParams.Default;

            // 戦力優位 0.8・時間圧力 0.6 で強く決戦を求める。
            float seek = DecisiveBattleDoctrineRules.DecisiveBattleSeek(0.8f, 0.6f, p);
            Assert.Greater(seek, 0.6f);

            // 主力を集結（集中 0.9・動員 0.85）。
            float massing = DecisiveBattleDoctrineRules.ForceMassing(0.9f, 0.85f, p);
            Assert.Greater(massing, 0.7f);

            // 敵が隙を見せ（0.75）・自軍は即応（0.8）＝決戦の機会が開く。
            float opp = DecisiveBattleDoctrineRules.BattleOpportunity(0.75f, 0.8f, p);
            Assert.Greater(opp, 0.5f);

            // 漸減作戦の妥当性は低い（敵の増勢 0.3・持久 0.4）＝決戦の方が理に適う。
            float attrition = DecisiveBattleDoctrineRules.AttritionAlternative(0.3f, 0.4f, p);
            Assert.Less(attrition, opp);

            // 主力を大きく投入（0.9）だが結果はやや不確実（0.5）＝博打性は中程度。
            float gamble = DecisiveBattleDoctrineRules.BattleGambleRisk(0.9f, 0.5f, p);

            // 勝てば敵主力撃滅（0.85）＋戦争疲弊（0.8）で戦略的に決定的。
            float payoff = DecisiveBattleDoctrineRules.DecisiveVictoryPayoff(0.85f, 0.8f, p);
            Assert.Greater(payoff, 0.6f);

            // 戦力は代替困難（0.5）。壊滅リスクは存在する。
            float defeat = DecisiveBattleDoctrineRules.CatastrophicDefeatRisk(gamble, 0.5f, p);
            Assert.Greater(defeat, 0f);

            // 総合＝機会×勝利効果 − 壊滅リスク。好機と利得がリスクを上回り、決戦は妥当（正）。
            float sound = DecisiveBattleDoctrineRules.DoctrineSoundness(opp, payoff, defeat, p);
            Assert.Greater(sound, 0f);

            // 同じ会戦でも戦力が完全に代替不能（1.0）なら壊滅リスクが跳ね上がり妥当性は下がる。
            float defeatIrr = DecisiveBattleDoctrineRules.CatastrophicDefeatRisk(gamble, 1f, p);
            float soundIrr = DecisiveBattleDoctrineRules.DoctrineSoundness(opp, payoff, defeatIrr, p);
            Assert.Less(soundIrr, sound);
        }
    }
}
