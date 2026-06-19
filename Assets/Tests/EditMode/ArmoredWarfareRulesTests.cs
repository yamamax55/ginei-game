using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    public class ArmoredWarfareRulesTests
    {
        const float Tol = 1e-4f;
        const float TolPow = 1e-3f; // Sqrt を含む箇所

        [Test]
        public void 突破力は装甲と火力の幾何平均()
        {
            // √(0.64*1.0)=0.8
            Assert.AreEqual(0.8f, ArmoredWarfareRules.BreakthroughPower(0.64f, 1f), TolPow);
            // どちらか0なら突破力0（√0）
            Assert.AreEqual(0f, ArmoredWarfareRules.BreakthroughPower(0f, 1f), TolPow);
            Assert.AreEqual(0f, ArmoredWarfareRules.BreakthroughPower(1f, 0f), TolPow);
        }

        [Test]
        public void 機動性は機動力と地形通過性の積()
        {
            Assert.AreEqual(0.4f, ArmoredWarfareRules.Maneuverability(0.8f, 0.5f), Tol);
            // 悪地形（通過性0）では機動が殺される
            Assert.AreEqual(0f, ArmoredWarfareRules.Maneuverability(1f, 0f), Tol);
        }

        [Test]
        public void 包囲能力は機動性と行動半径の積()
        {
            Assert.AreEqual(0.3f, ArmoredWarfareRules.EnvelopmentCapacity(0.4f, 0.75f), Tol);
        }

        [Test]
        public void 対戦車防御は敵兵器と防御縦深の積()
        {
            Assert.AreEqual(0.4f, ArmoredWarfareRules.AntiTankResistance(0.5f, 0.8f), Tol);
        }

        [Test]
        public void 突破の成否は突破力と対戦車防御の比()
        {
            // 0.8/(0.8+0.4)=0.66667
            Assert.AreEqual(0.66667f, ArmoredWarfareRules.BreakthroughSuccess(0.8f, 0.4f), Tol);
            // 双方0は膠着0.5
            Assert.AreEqual(0.5f, ArmoredWarfareRules.BreakthroughSuccess(0f, 0f), Tol);
        }

        [Test]
        public void 諸兵科連合は歩兵と航空の支援で積み増す()
        {
            // 1 + 1*0.3 + 1*0.2 = 1.5（上限ちょうど）
            Assert.AreEqual(1.5f, ArmoredWarfareRules.CombinedArmsBonus(1f, 1f), Tol);
            // 1 + 0.5*0.3 + 0.5*0.2 = 1.25
            Assert.AreEqual(1.25f, ArmoredWarfareRules.CombinedArmsBonus(0.5f, 0.5f), Tol);
            // 支援なしは倍率1
            Assert.AreEqual(1f, ArmoredWarfareRules.CombinedArmsBonus(0f, 0f), Tol);
        }

        [Test]
        public void 補給制約は突出するほど止まる()
        {
            // 1/(1+1*1)=0.5
            Assert.AreEqual(0.5f, ArmoredWarfareRules.LogisticsConstraint(1f, 1f), Tol);
            // 0.9/(1+3)=0.225（遠く突出＝燃料切れで減衰）
            Assert.AreEqual(0.225f, ArmoredWarfareRules.LogisticsConstraint(0.9f, 3f), Tol);
            // 距離0なら燃料そのまま
            Assert.AreEqual(0.8f, ArmoredWarfareRules.LogisticsConstraint(0.8f, 0f), Tol);
        }

        [Test]
        public void 攻勢価値は突破と包囲と補給の積()
        {
            // 0.5*0.4*0.5=0.1
            Assert.AreEqual(0.1f, ArmoredWarfareRules.ArmoredOffensiveValue(0.5f, 0.4f, 0.5f), Tol);
            // どれか0なら攻勢不成立（最小律）
            Assert.AreEqual(0f, ArmoredWarfareRules.ArmoredOffensiveValue(0.9f, 0.9f, 0f), Tol);
        }

        [Test]
        public void 入力はクランプされる()
        {
            // 範囲外入力でも例外なくクランプ
            Assert.AreEqual(0.8f, ArmoredWarfareRules.BreakthroughPower(2f, 0.64f), TolPow); // armor→1,fire→0.64
            Assert.AreEqual(1f, ArmoredWarfareRules.Maneuverability(5f, 3f), Tol); // 共に1
            Assert.AreEqual(0f, ArmoredWarfareRules.LogisticsConstraint(-1f, 2f), Tol); // 燃料→0
        }

        [Test]
        public void 厚い装甲と火力で破り機動で包むが突出すれば燃料切れで止まる()
        {
            // 重装甲（0.81）と高火力（1.0）で強い突破力 √0.81=0.9
            float bp = ArmoredWarfareRules.BreakthroughPower(0.81f, 1f);
            Assert.AreEqual(0.9f, bp, TolPow);

            // 浅い対戦車防御（兵器0.5×縦深0.4=0.2）相手なら突破は高確率
            float atr = ArmoredWarfareRules.AntiTankResistance(0.5f, 0.4f); // 0.2
            float success = ArmoredWarfareRules.BreakthroughSuccess(bp, atr); // 0.9/1.1=0.81818
            Assert.AreEqual(0.81818f, success, Tol);

            // 高機動（0.9）×良地形（1.0）→機動性0.9、行動半径0.5で包囲能力0.45
            float man = ArmoredWarfareRules.Maneuverability(0.9f, 1f); // 0.9
            float env = ArmoredWarfareRules.EnvelopmentCapacity(man, 0.5f); // 0.45
            Assert.AreEqual(0.45f, env, Tol);

            // 補給潤沢で突出が浅い（燃料1.0・距離0）なら補給制約は満点1.0＝高い攻勢価値
            float lcNear = ArmoredWarfareRules.LogisticsConstraint(1f, 0f); // 1.0
            float valNear = ArmoredWarfareRules.ArmoredOffensiveValue(success, env, lcNear);
            Assert.AreEqual(0.81818f * 0.45f, valNear, Tol); // 0.36818

            // 同じ突破・包囲でも深く突出（距離4）すれば燃料が薄まり攻勢が萎む
            float lcFar = ArmoredWarfareRules.LogisticsConstraint(1f, 4f); // 1/5=0.2
            float valFar = ArmoredWarfareRules.ArmoredOffensiveValue(success, env, lcFar);
            Assert.AreEqual(0.81818f * 0.45f * 0.2f, valFar, Tol); // 0.07364

            Assert.Greater(valNear, valFar); // 突出は攻勢価値を削る
        }
    }
}
