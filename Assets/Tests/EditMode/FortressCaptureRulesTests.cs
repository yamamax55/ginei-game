using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 回廊要塞は「基本は占領」（#40）。通常の会戦で施設は破壊されず、守備を潰したうえで
    /// 制圧線へ到達したときに<b>所属だけ</b>が移る、という切り分けを固定する。
    /// 「守備艦隊を全滅させた」＝「要塞が落ちた」ではないこと（表示の出し分けを含む）を担保する。
    /// </summary>
    public class FortressCaptureRulesTests
    {
        private static FortressSiegeState State(int core, int turrets, int fleets, bool atLine,
                                                bool destroyed = false)
            => new FortressSiegeState(core, turrets, fleets, atLine, destroyed);

        // ── 施設の守備が沈黙したか（守備艦隊とは別勘定）──

        [Test]
        public void 中枢が残っていれば施設守備は沈黙していない()
        {
            Assert.IsFalse(FortressCaptureRules.IsFacilityGarrisonSilenced(State(100, 0, 0, false)));
        }

        [Test]
        public void 砲台が残っていれば施設守備は沈黙していない()
        {
            Assert.IsFalse(FortressCaptureRules.IsFacilityGarrisonSilenced(State(0, 3, 0, false)));
        }

        [Test]
        public void 中枢も砲台も尽きたら施設守備は沈黙()
        {
            Assert.IsTrue(FortressCaptureRules.IsFacilityGarrisonSilenced(State(0, 0, 5, false)));
        }

        // ── 守備が尽きた（占領の前提）──

        [Test]
        public void 既定では守備艦隊が残っている限り守備は尽きていない()
        {
            Assert.IsFalse(FortressCaptureRules.IsGarrisonSuppressed(State(0, 0, 2, false)));
        }

        [Test]
        public void 守備艦隊まで排除したら守備が尽きる()
        {
            Assert.IsTrue(FortressCaptureRules.IsGarrisonSuppressed(State(0, 0, 0, false)));
        }

        [Test]
        public void 緩い設定なら施設守備の沈黙だけで守備が尽きたとみなす()
        {
            var loose = new FortressCaptureParams(false);
            Assert.IsTrue(FortressCaptureRules.IsGarrisonSuppressed(State(0, 0, 3, false), loose));
        }

        // ── 占領の成立条件（守備が尽きた状態＋制圧線への到達）──

        [Test]
        public void 守備が尽きても制圧線へ届かなければ占領しない()
        {
            Assert.IsFalse(FortressCaptureRules.CanCapture(State(0, 0, 0, false)));
            Assert.AreEqual(FortressControl.守備制圧, FortressCaptureRules.Resolve(State(0, 0, 0, false)));
        }

        [Test]
        public void 制圧線へ届いても守備が残っていれば占領しない()
        {
            Assert.IsFalse(FortressCaptureRules.CanCapture(State(500, 4, 1, true)));
            Assert.AreEqual(FortressControl.守備健在, FortressCaptureRules.Resolve(State(500, 4, 1, true)));
        }

        [Test]
        public void 守備が尽きた状態で制圧線へ到達すると占領が成立する()
        {
            Assert.IsTrue(FortressCaptureRules.CanCapture(State(0, 0, 0, true)));
            Assert.AreEqual(FortressControl.占領, FortressCaptureRules.Resolve(State(0, 0, 0, true)));
        }

        [Test]
        public void 守備艦隊だけ全滅させても占領にはならない()
        {
            // 守備艦隊0でも施設の中枢・砲台が生きていれば「守備健在」＝封鎖は続く。
            var s = State(9000, 8, 0, true);
            Assert.IsFalse(FortressCaptureRules.CanCapture(s));
            Assert.AreEqual(FortressControl.守備健在, FortressCaptureRules.Resolve(s));
            Assert.IsTrue(FortressCaptureRules.BlocksPassage(FortressCaptureRules.Resolve(s)));
        }

        // ── 破壊は特殊手段のみ ──

        [Test]
        public void 通常戦闘では施設は破壊できない()
        {
            Assert.IsFalse(FortressCaptureRules.AllowsDestruction(FortressDamageSource.通常戦闘));
            Assert.IsFalse(FortressCaptureRules.DestructibleByNormalAttack);
        }

        [Test]
        public void 特殊破壊だけが施設を失わせうる()
        {
            Assert.IsTrue(FortressCaptureRules.AllowsDestruction(FortressDamageSource.特殊破壊));
        }

        [Test]
        public void 破壊済みなら占領できず状態は破壊()
        {
            var s = State(0, 0, 0, true, destroyed: true);
            Assert.IsFalse(FortressCaptureRules.CanCapture(s));
            Assert.AreEqual(FortressControl.破壊, FortressCaptureRules.Resolve(s));
            Assert.IsFalse(FortressCaptureRules.FacilitySurvives(FortressControl.破壊));
        }

        [Test]
        public void 占領と守備制圧では施設が残る()
        {
            Assert.IsTrue(FortressCaptureRules.FacilitySurvives(FortressControl.占領));
            Assert.IsTrue(FortressCaptureRules.FacilitySurvives(FortressControl.守備制圧));
            Assert.IsTrue(FortressCaptureRules.FacilitySurvives(FortressControl.守備健在));
        }

        // ── 通行・所属 ──

        [Test]
        public void 封鎖が続くのは守備健在のときだけ()
        {
            Assert.IsTrue(FortressCaptureRules.BlocksPassage(FortressControl.守備健在));
            Assert.IsFalse(FortressCaptureRules.BlocksPassage(FortressControl.守備制圧));
            Assert.IsFalse(FortressCaptureRules.BlocksPassage(FortressControl.占領));
            Assert.IsFalse(FortressCaptureRules.BlocksPassage(FortressControl.破壊));
        }

        [Test]
        public void 所属が移るのは占領のときだけ()
        {
            Assert.IsTrue(FortressCaptureRules.TransfersOwnership(FortressControl.占領));
            Assert.IsFalse(FortressCaptureRules.TransfersOwnership(FortressControl.守備制圧));
            Assert.IsFalse(FortressCaptureRules.TransfersOwnership(FortressControl.守備健在));
            Assert.IsFalse(FortressCaptureRules.TransfersOwnership(FortressControl.破壊));
        }

        // ── 表示（誤表示の防止）──

        [Test]
        public void 守備制圧の文言に陥落や破壊を含めない()
        {
            string s = FortressCaptureRules.DescribeControl(FortressControl.守備制圧, "イゼルローン要塞");
            StringAssert.Contains("イゼルローン要塞", s);
            StringAssert.Contains("守備を制圧", s);
            StringAssert.Contains("施設は健在", s);
            Assert.IsFalse(s.Contains("陥落"), s);
            Assert.IsFalse(s.Contains("破壊"), s);
        }

        [Test]
        public void 占領の文言は攻撃側の名前を含み破壊とは言わない()
        {
            string s = FortressCaptureRules.DescribeControl(FortressControl.占領, "イゼルローン要塞", "同盟");
            StringAssert.Contains("同盟", s);
            StringAssert.Contains("占領", s);
            Assert.IsFalse(s.Contains("破壊"), s);
        }

        [Test]
        public void 名前が空でも要塞と表示する()
        {
            StringAssert.Contains("要塞", FortressCaptureRules.DescribeControl(FortressControl.守備健在, null));
        }

        // ── 戦略側への書き戻し（既存窓口へ委譲）──

        [Test]
        public void 占領の書き戻しは所有移転と守備残置を行う()
        {
            var f = new Fortress(500f, 100f, 1f, true) { owner = Faction.帝国, fortressName = "イゼルローン要塞" };
            Assert.IsTrue(FortressCaptureRules.ApplyToStrategy(f, FortressControl.占領, Faction.同盟, 120f, 0.6f));
            Assert.AreEqual(Faction.同盟, f.owner);
            Assert.AreEqual(120f, f.garrisonStrength, 0.001f);
            Assert.AreEqual(0.6f, f.shieldIntegrity, 0.001f);
            Assert.IsTrue(f.controlsCorridor); // 守備を置けたので今度は元の持ち主が通れない
        }

        [Test]
        public void 守備制圧の書き戻しは所有を動かさず封鎖だけ解く()
        {
            var f = new Fortress(500f, 100f, 1f, true) { owner = Faction.帝国 };
            Assert.IsTrue(FortressCaptureRules.ApplyToStrategy(f, FortressControl.守備制圧, Faction.同盟, 300f));
            Assert.AreEqual(Faction.帝国, f.owner);      // 所属は移らない
            Assert.AreEqual(0f, f.garrisonStrength, 0.001f);
            Assert.IsFalse(f.controlsCorridor);
            Assert.IsFalse(FortressRules.BlocksPassage(f));
        }

        [Test]
        public void 守備健在なら書き戻しても何も変えない()
        {
            var f = new Fortress(500f, 100f, 1f, true) { owner = Faction.帝国 };
            Assert.IsFalse(FortressCaptureRules.ApplyToStrategy(f, FortressControl.守備健在, Faction.同盟, 300f));
            Assert.AreEqual(Faction.帝国, f.owner);
            Assert.AreEqual(500f, f.garrisonStrength, 0.001f);
            Assert.IsTrue(f.controlsCorridor);
        }

        [Test]
        public void null要塞でも落ちない()
        {
            Assert.IsFalse(FortressCaptureRules.ApplyToStrategy(null, FortressControl.占領, Faction.同盟, 100f));
        }

        // ── 一連の流れ（守備健在→守備制圧→占領）──

        [Test]
        public void 段階を追って守備健在から占領まで進む()
        {
            var p = FortressCaptureParams.Default;
            Assert.AreEqual(FortressControl.守備健在, FortressCaptureRules.Resolve(State(9000, 8, 2, false), p));
            Assert.AreEqual(FortressControl.守備健在, FortressCaptureRules.Resolve(State(0, 2, 2, false), p)); // 砲台が残る
            Assert.AreEqual(FortressControl.守備健在, FortressCaptureRules.Resolve(State(0, 0, 2, false), p)); // 守備艦隊が残る
            Assert.AreEqual(FortressControl.守備制圧, FortressCaptureRules.Resolve(State(0, 0, 0, false), p));
            Assert.AreEqual(FortressControl.占領, FortressCaptureRules.Resolve(State(0, 0, 0, true), p));
        }
    }
}
