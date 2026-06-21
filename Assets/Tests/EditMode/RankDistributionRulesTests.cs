using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 階級ピラミッド／定員（TKO-13・<see cref="RankDistributionRules"/>・#2490）を固定する：階級区分、士官tier橋
    /// （`RankSystem` 整合）、ピラミッド目標（下広上狭・単調非増加・総数保存）、定員空き、将校過多、集計。
    /// </summary>
    public class RankDistributionRulesTests
    {
        private static RankDistributionRules.PyramidParams P => RankDistributionRules.PyramidParams.Default;

        [Test]
        public void Classification_ByGrade()
        {
            Assert.IsTrue(RankDistributionRules.IsEnlisted(MilitaryGrade.二等兵));
            Assert.IsTrue(RankDistributionRules.IsEnlisted(MilitaryGrade.兵長));
            Assert.IsTrue(RankDistributionRules.IsNco(MilitaryGrade.軍曹));
            Assert.IsTrue(RankDistributionRules.IsWarrant(MilitaryGrade.准尉));
            Assert.IsTrue(RankDistributionRules.IsOfficer(MilitaryGrade.少尉));
            Assert.IsTrue(RankDistributionRules.IsFlag(MilitaryGrade.准将));
            Assert.IsFalse(RankDistributionRules.IsOfficer(MilitaryGrade.准尉));
            Assert.IsFalse(RankDistributionRules.IsFlag(MilitaryGrade.大佐));
        }

        [Test]
        public void ToOfficerTier_BridgesToRankSystem()
        {
            Assert.AreEqual(1, RankDistributionRules.ToOfficerTier(MilitaryGrade.少尉));
            Assert.AreEqual(4, RankDistributionRules.ToOfficerTier(MilitaryGrade.大佐));
            Assert.AreEqual(5, RankDistributionRules.ToOfficerTier(MilitaryGrade.准将));
            Assert.AreEqual(10, RankDistributionRules.ToOfficerTier(MilitaryGrade.元帥));
            Assert.AreEqual(0, RankDistributionRules.ToOfficerTier(MilitaryGrade.二等兵), "兵卒は士官tierを持たない");
            Assert.AreEqual(0, RankDistributionRules.ToOfficerTier(MilitaryGrade.准尉), "准尉は任官前");
            // RankSystem の既定ラダーと一致（准将5/元帥10）
            Assert.AreEqual("准将", RankSystem.DefaultRankName(RankDistributionRules.ToOfficerTier(MilitaryGrade.准将)));
            Assert.AreEqual("元帥", RankSystem.DefaultRankName(RankDistributionRules.ToOfficerTier(MilitaryGrade.元帥)));
        }

        [Test]
        public void PyramidTarget_WideAtBottomNarrowAtTop_AndConservesTotal()
        {
            int total = 100000;
            int[] target = RankDistributionRules.PyramidTarget(total, P);
            Assert.AreEqual(RankDistributionRules.GradeCount, target.Length);

            // 単調非増加（下＝index0 が最大、上ほど少ない）
            for (int i = 1; i < target.Length; i++)
                Assert.LessOrEqual(target[i], target[i - 1], $"grade {i} は一つ下以下");
            Assert.Greater(target[0], target[target.Length - 1], "二等兵 ＞ 元帥");

            // 総数は丸め誤差内で保存
            int sum = 0; for (int i = 0; i < target.Length; i++) sum += target[i];
            Assert.LessOrEqual(System.Math.Abs(sum - total), target.Length, "丸め誤差の範囲で総数保存");

            // 兵卒が士官より圧倒的に多い
            Assert.Greater(target[(int)MilitaryGrade.二等兵], target[(int)MilitaryGrade.少尉]);
        }

        [Test]
        public void PyramidTarget_ZeroForce_AllZero()
        {
            int[] t = RankDistributionRules.PyramidTarget(0, P);
            for (int i = 0; i < t.Length; i++) Assert.AreEqual(0, t[i]);
        }

        [Test]
        public void Vacancy_TargetMinusCurrentClampedZero()
        {
            var d = new RankDistribution();
            d.Set(MilitaryGrade.少尉, 30);
            int[] target = RankDistributionRules.PyramidTarget(100000, P);
            int idx = (int)MilitaryGrade.少尉;
            Assert.AreEqual(Mathf.Max(0, target[idx] - 30), RankDistributionRules.Vacancy(d, MilitaryGrade.少尉, target));

            // 現員が目標超過なら空き0
            d.Set(MilitaryGrade.元帥, 9999);
            Assert.AreEqual(0, RankDistributionRules.Vacancy(d, MilitaryGrade.元帥, target));
        }

        [Test]
        public void TotalAndOfficerRatio()
        {
            var d = new RankDistribution();
            d.Set(MilitaryGrade.二等兵, 800);
            d.Set(MilitaryGrade.少尉, 150);
            d.Set(MilitaryGrade.准将, 50);
            Assert.AreEqual(1000, RankDistributionRules.TotalForce(d));
            Assert.AreEqual(200, RankDistributionRules.OfficerCount(d));
            Assert.AreEqual(0.2f, RankDistributionRules.OfficerRatio(d), 1e-4);
        }

        [Test]
        public void OverlayNamedOfficers_RaisesTopGradesToNamedRoster()
        {
            // 幾何ピラミッドだと頂点が 0 に丸まる（120000・既定比）
            int[] target = RankDistributionRules.PyramidTarget(120000, P);
            Assert.AreEqual(0, target[(int)MilitaryGrade.元帥], "幾何分布では元帥は0に丸まる");
            Assert.AreEqual(0, target[(int)MilitaryGrade.大将], "幾何分布では大将は0に丸まる");

            var d = new RankDistribution();
            for (int i = 0; i < target.Length; i++) d.Set((MilitaryGrade)i, target[i]);

            // 実在の提督：元帥1・大将3・大尉2（tier 10/8/2）
            var named = new int[11];
            named[10] = 1; named[8] = 3; named[2] = 2;
            RankDistributionRules.OverlayNamedOfficers(d, named);

            Assert.AreEqual(1, d.Get(MilitaryGrade.元帥), "実在元帥が頂点に反映される");
            Assert.AreEqual(3, d.Get(MilitaryGrade.大将), "実在大将が反映される");
            // 大尉は幾何目標(50)＞実在(2)なので下げない＝max下限保証
            Assert.AreEqual(target[(int)MilitaryGrade.大尉], d.Get(MilitaryGrade.大尉), "既存が多い段は据え置き");
            Assert.GreaterOrEqual(d.Get(MilitaryGrade.大尉), 2, "実在数を下回らない");
        }

        [Test]
        public void OverlayNamedOfficers_NeverLowersExistingCount()
        {
            var d = new RankDistribution();
            d.Set(MilitaryGrade.少尉, 100); // 既存が多い段
            var named = new int[11];
            named[1] = 5; // 少尉(tier1)の実在は5
            RankDistributionRules.OverlayNamedOfficers(d, named);
            Assert.AreEqual(100, d.Get(MilitaryGrade.少尉), "max下限＝既存を下げない");

            // null/空は無変更（後方互換）
            RankDistributionRules.OverlayNamedOfficers(d, null);
            Assert.AreEqual(100, d.Get(MilitaryGrade.少尉));
        }

        [Test]
        public void IsTopHeavy_FlagsOfficerBloat()
        {
            // 全員士官＝極端な頭でっかち
            var bloated = new RankDistribution();
            bloated.Set(MilitaryGrade.少尉, 500);
            bloated.Set(MilitaryGrade.大佐, 500);
            Assert.IsTrue(RankDistributionRules.IsTopHeavy(bloated, P, 0.1f));

            // ピラミッド通りの分布＝頭でっかちでない
            int[] target = RankDistributionRules.PyramidTarget(100000, P);
            var healthy = new RankDistribution();
            for (int i = 0; i < target.Length; i++) healthy.Set((MilitaryGrade)i, target[i]);
            Assert.IsFalse(RankDistributionRules.IsTopHeavy(healthy, P, 0.05f));
        }
    }
}
