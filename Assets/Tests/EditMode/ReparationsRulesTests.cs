using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 賠償金（ヴェルサイユの罠）を固定する：絞殺度は賠償率の冪で非線形、取り分はラッファー曲線
    /// （全力取り立て＝鶏が死んで取り分ゼロ）、復讐主義は重賠償で育ち寛大なら風化、
    /// 支払不能は不健全経済×重賠償、持続可能上限は解析解。クランプを担保。
    /// </summary>
    public class ReparationsRulesTests
    {
        private static readonly ReparationsParams P = ReparationsParams.Default;
        // 絞殺上限1.0/冪指数2/復讐成長0.1/風化0.02/寛大閾値0.1/支払不能係数1.5

        [Test]
        public void EconomicStrangulation_NonlinearInBurden()
        {
            Assert.AreEqual(0f, ReparationsRules.EconomicStrangulation(0f, P), 1e-5f);    // 賠償なし＝無害
            Assert.AreEqual(0.25f, ReparationsRules.EconomicStrangulation(0.5f, P), 1e-5f); // 0.5^2＝半分よりずっと軽い
            Assert.AreEqual(1f, ReparationsRules.EconomicStrangulation(1f, P), 1e-5f);    // 全力＝経済全滅
            // 非線形＝半分の賠償は最大の半分よりはるかに優しい
            Assert.Less(ReparationsRules.EconomicStrangulation(0.5f, P),
                        0.5f * ReparationsRules.EconomicStrangulation(1f, P));
            // 入力クランプ
            Assert.AreEqual(1f, ReparationsRules.EconomicStrangulation(2f, P), 1e-5f);
        }

        [Test]
        public void AnnualPayment_LafferCurve_FullBurdenYieldsNothing()
        {
            // 中庸の賠償＝100×0.5×(1−0.25)=37.5 が取れる
            Assert.AreEqual(37.5f, ReparationsRules.AnnualPayment(0.5f, 100f, P), 1e-4f);
            // 全力取り立て＝鶏を殺して取り分ゼロ
            Assert.AreEqual(0f, ReparationsRules.AnnualPayment(1f, 100f, P), 1e-4f);
            // 賠償なし＝ゼロ
            Assert.AreEqual(0f, ReparationsRules.AnnualPayment(0f, 100f, P), 1e-4f);
            // 経済マイナスはゼロ扱い
            Assert.AreEqual(0f, ReparationsRules.AnnualPayment(0.5f, -100f, P), 1e-4f);
        }

        [Test]
        public void SustainableBurden_IsLafferPeak()
        {
            // 既定（m=1,k=2）＝(1/3)^(1/2)≈0.57735
            float sb = ReparationsRules.SustainableBurden(P);
            Assert.AreEqual(0.57735f, sb, 1e-4f);
            // 頂点＝前後どちらにずらしても取り分は減る
            float atPeak = ReparationsRules.AnnualPayment(sb, 100f, P);
            Assert.Greater(atPeak, ReparationsRules.AnnualPayment(sb - 0.1f, 100f, P));
            Assert.Greater(atPeak, ReparationsRules.AnnualPayment(sb + 0.1f, 100f, P));
            // 絞殺が無効なら全部取れる
            var noStrangle = new ReparationsParams(0f, 2f, 0.1f, 0.02f, 0.1f, 1.5f);
            Assert.AreEqual(1f, ReparationsRules.SustainableBurden(noStrangle), 1e-5f);
        }

        [Test]
        public void RevanchismTick_HeavyGrows_LightFades()
        {
            // 重賠償＝0.1×1×1=0.1 育つ
            Assert.AreEqual(0.1f, ReparationsRules.RevanchismTick(0f, 1f, 1f, P), 1e-5f);
            // 重いほど速く育つ
            Assert.Greater(ReparationsRules.RevanchismTick(0f, 1f, 1f, P),
                           ReparationsRules.RevanchismTick(0f, 0.5f, 1f, P));
            // 寛大閾値以下＝風化（0.02×10=0.2 減る）
            Assert.AreEqual(0.3f, ReparationsRules.RevanchismTick(0.5f, 0.1f, 10f, P), 1e-5f);
            // 風化はゼロで止まる
            Assert.AreEqual(0f, ReparationsRules.RevanchismTick(0.05f, 0f, 100f, P), 1e-5f);
            // 上限1
            Assert.AreEqual(1f, ReparationsRules.RevanchismTick(0.95f, 1f, 10f, P), 1e-5f);
        }

        [Test]
        public void DefaultRisk_BurdenTimesUnhealth()
        {
            // 健全経済なら重賠償も払える
            Assert.AreEqual(0f, ReparationsRules.DefaultRisk(1f, 1f, P), 1e-5f);
            // 死にかけ経済への全力賠償＝確実に破綻（1×1×1.5→クランプ1）
            Assert.AreEqual(1f, ReparationsRules.DefaultRisk(1f, 0f, P), 1e-5f);
            Assert.AreEqual(0.375f, ReparationsRules.DefaultRisk(0.5f, 0.5f, P), 1e-5f);
            // 賠償が重いほどリスク単調増
            Assert.Greater(ReparationsRules.DefaultRisk(0.8f, 0.5f, P),
                           ReparationsRules.DefaultRisk(0.4f, 0.5f, P));
        }

        [Test]
        public void Params_CtorClamps()
        {
            var p = new ReparationsParams(2f, 0.5f, -1f, -1f, 2f, -1f);
            Assert.AreEqual(1f, p.maxStrangulation, 1e-5f);       // 0..1
            Assert.AreEqual(1f, p.strangulationExponent, 1e-5f);  // 1以上
            Assert.AreEqual(0f, p.revanchismGrowthRate, 1e-5f);   // 非負
            Assert.AreEqual(0f, p.revanchismDecayRate, 1e-5f);    // 非負
            Assert.AreEqual(1f, p.lightBurdenThreshold, 1e-5f);   // 0..1
            Assert.AreEqual(0f, p.defaultRiskScale, 1e-5f);       // 非負
        }

        [Test]
        public void LongRunStory_VersaillesTrap()
        {
            // 過酷な講和 vs 持続可能な講和を同じ期間回す（決定論のシミュレート）
            float sustainable = ReparationsRules.SustainableBurden(P);
            const float harsh = 0.95f;
            float harshTotal = 0f, fairTotal = 0f;
            float harshRev = 0f, fairRev = 0f;
            float midHarshRev = 0f, midFairRev = 0f;
            for (int year = 0; year < 20; year++)
            {
                harshTotal += ReparationsRules.AnnualPayment(harsh, 100f, P);
                fairTotal += ReparationsRules.AnnualPayment(sustainable, 100f, P);
                harshRev = ReparationsRules.RevanchismTick(harshRev, harsh, 1f, P);
                fairRev = ReparationsRules.RevanchismTick(fairRev, sustainable, 1f, P);
                if (year == 9) { midHarshRev = harshRev; midFairRev = fairRev; } // 10年目の途中経過
            }
            // 取り立てすぎは儲からず、恨みは過酷側が速く積み上がる（超長期では双方とも上限に達しうる）
            Assert.Greater(fairTotal, harshTotal);
            Assert.Greater(midHarshRev, midFairRev);
            Assert.AreEqual(1f, harshRev, 1e-5f); // 過酷賠償20年＝復讐主義は満タン＝次の戦争の種
        }
    }
}
