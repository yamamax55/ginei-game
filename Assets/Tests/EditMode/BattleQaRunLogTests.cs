using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>固定会戦QAの結果ログ（run_id・必須項目・上限と記録喪失の明示・未判定の扱い）。</summary>
    public class BattleQaRunLogTests
    {
        [Test]
        public void FormatRunId_ContainsPresetSeedSerialAndSuffix()
        {
            Assert.AreEqual("退却-s20260913-n003-ab12cd34", BattleQaRunLog.FormatRunId("退却", 20260913, 3, "ab12cd34"));
            Assert.AreEqual("陣形変更-s7-n012", BattleQaRunLog.FormatRunId("陣形変更", 7, 12, null));
            Assert.AreNotEqual(BattleQaRunLog.FormatRunId("退却", 1, 1, ""), BattleQaRunLog.FormatRunId("退却", 2, 1, ""),
                "seed が違えば run_id も違う");
        }

        [Test]
        public void EveryLine_CarriesRunIdPresetSeedAndGameTime()
        {
            var log = new BattleQaRunLog("run-1", "不退転", 42);
            log.Add(12.345f, "発動");
            string line = log.Lines[0];
            StringAssert.Contains("run-1", line);
            StringAssert.Contains("不退転", line);
            StringAssert.Contains("seed=42", line);
            StringAssert.Contains("t=12.35", line);
            StringAssert.EndsWith("発動", line);
        }

        [Test]
        public void OverCapacity_DropsOldestAndCounts()
        {
            var log = new BattleQaRunLog("r", "p", 1);
            for (int i = 0; i < BattleQaRunLog.Capacity + 5; i++) log.Add(i, "#" + i);
            Assert.AreEqual(BattleQaRunLog.Capacity, log.Count);
            Assert.AreEqual(5, log.Dropped);
            StringAssert.EndsWith("#5", log.Lines[0], "古い行から捨てる");
            Assert.AreEqual(MoraleAuditLog.Capacity, BattleQaRunLog.Capacity, "既存QAと同じ上限");
        }

        [Test]
        public void Overall_NoChecksOrAnyUndecided_IsUndecided()
        {
            var log = new BattleQaRunLog("r", "p", 1);
            Assert.AreEqual(BattleQaVerdict.未判定, log.Overall, "判定0件は未判定（合格にしない）");

            log.AddCheck(new BattleQaCheck("a", BattleQaVerdict.合格, "", 0f));
            Assert.AreEqual(BattleQaVerdict.合格, log.Overall);

            log.AddCheck(new BattleQaCheck("b", BattleQaVerdict.不合格, "", 0f));
            Assert.AreEqual(BattleQaVerdict.不合格, log.Overall);

            log.AddCheck(new BattleQaCheck("c", BattleQaVerdict.未判定, "", 0f));
            Assert.AreEqual(BattleQaVerdict.未判定, log.Overall, "未判定が混じれば全体も未判定");
            Assert.AreEqual(1, log.CountVerdict(BattleQaVerdict.合格));
            Assert.AreEqual(3, log.Checks.Count);
            Assert.AreEqual(3, log.Count, "判定はログ行にも出る");
        }

        [Test]
        public void Checks_AreKeptEvenWhenLinesOverflow()
        {
            var log = new BattleQaRunLog("r", "p", 1);
            log.AddCheck(new BattleQaCheck("最初の判定", BattleQaVerdict.合格, "", 0f));
            for (int i = 0; i < BattleQaRunLog.Capacity + 1; i++) log.Add(i, "x");
            Assert.AreEqual(1, log.Checks.Count);
            StringAssert.Contains("最初の判定", log.Dump(""));
        }

        [Test]
        public void LossNotice_EmptyWhenNothingLost_ElseNamesEachSource()
        {
            Assert.AreEqual("", BattleQaRunLog.LossNotice(0, 0, false));
            string all = BattleQaRunLog.LossNotice(2, 3, true);
            StringAssert.Contains("結果ログ", all);
            StringAssert.Contains("2 行", all);
            StringAssert.Contains("士気の原因台帳", all);
            StringAssert.Contains("3 件", all);
            StringAssert.Contains("陣形保持記録", all);
        }

        [Test]
        public void Dump_HasHeaderSummaryAndLines()
        {
            var log = new BattleQaRunLog("run-x", "退却", 9);
            log.Add(1f, "開始");
            log.AddCheck(new BattleQaCheck("配下の退却", BattleQaVerdict.合格, "変位=3.0", 2f));
            string d = log.Dump("[見出し]");
            StringAssert.StartsWith("[見出し]", d);
            StringAssert.Contains("run_id=run-x", d);
            StringAssert.Contains("全体の結論=合格", d);
            StringAssert.Contains("［合格］配下の退却：変位=3.0", d);
            StringAssert.Contains("開始", d);
        }

        [Test]
        public void NullInputs_AreSafe()
        {
            var log = new BattleQaRunLog(null, null, 0);
            log.Add(0f, null);
            Assert.AreEqual(1, log.Count);
            var c = new BattleQaCheck(null, BattleQaVerdict.合格, null, 0f);
            Assert.AreEqual("", c.name);
            Assert.AreEqual("", c.detail);
        }
    }
}
