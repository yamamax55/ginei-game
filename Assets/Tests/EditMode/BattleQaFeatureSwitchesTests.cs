using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>固定会戦QAの機能スイッチ（SPEED-08）：既定＝先行QAの状態・1項目だけ変える・援軍の接続（版2）・版違いの拒否・分類と記録。</summary>
    public class BattleQaFeatureSwitchesTests
    {
        private static readonly BattleQaFeature[] AllFeatures =
        {
            BattleQaFeature.自動包囲, BattleQaFeature.援軍, BattleQaFeature.戦況イベント,
        };

        [Test]
        public void FixedDefault_MatchesPriorFixedQa()
        {
            BattleQaFeatureSwitches d = BattleQaFeatureSwitches.FixedDefault;
            Assert.IsTrue(d.Get(BattleQaFeature.自動包囲), "先行QAは軍団長AIの判断（包囲含む）を止めていない");
            Assert.IsFalse(d.Get(BattleQaFeature.援軍));
            Assert.IsFalse(d.Get(BattleQaFeature.戦況イベント), "先行QAは会戦イベントを隔離している");
            Assert.AreEqual(0, d.ChangedCount);
            Assert.IsTrue(d.IsFixedComparable);
            Assert.AreEqual(BattleQaFeatureSwitches.CurrentVersion, d.version);
            Assert.AreEqual("既定", d.name);
            CollectionAssert.IsEmpty(d.Validate());
        }

        [Test]
        public void With_ChangesOnlyThatFeature()
        {
            BattleQaFeatureSwitches d = BattleQaFeatureSwitches.FixedDefault;
            foreach (BattleQaFeature target in AllFeatures)
            {
                BattleQaFeatureSwitches s = d.With(null, target, !d.Get(target));
                Assert.AreEqual(!d.Get(target), s.Get(target), target + " が変わっていない");
                foreach (BattleQaFeature other in AllFeatures)
                    if (other != target) Assert.AreEqual(d.Get(other), s.Get(other), target + " を変えたら " + other + " まで変わった");
                Assert.AreEqual(1, s.ChangedCount);
                Assert.IsFalse(s.IsFixedComparable);
                Assert.AreEqual(d.version, s.version);
            }
            // 元は不変（コピーを返す）。
            Assert.IsTrue(d.IsFixedComparable);
        }

        [Test]
        public void AutoName_ListsOnlyChangedFeatures()
        {
            Assert.AreEqual("既定", BattleQaFeatureSwitches.AutoName(true, false, false));
            Assert.AreEqual("自動包囲OFF", BattleQaFeatureSwitches.AutoName(false, false, false));
            Assert.AreEqual("戦況イベントON", BattleQaFeatureSwitches.AutoName(true, false, true));
            Assert.AreEqual("自動包囲OFF+援軍ON+戦況イベントON", BattleQaFeatureSwitches.AutoName(false, true, true));
            Assert.AreEqual("自動包囲OFF", BattleQaFeatureSwitches.FixedDefault.With("", BattleQaFeature.自動包囲, false).name);
            Assert.AreEqual("指定名", BattleQaFeatureSwitches.FixedDefault.With("指定名", BattleQaFeature.自動包囲, false).name);
        }

        [Test]
        public void Reinforcement_IsConnectedToBattleSetup_OnIsAccepted_AndClassifiedAsIsolation()
        {
            Assert.IsTrue(BattleQaFeatureSwitches.IsConnected(BattleQaFeature.援軍));
            Assert.IsTrue(BattleQaFeatureSwitches.IsConnected(BattleQaFeature.自動包囲));
            Assert.IsTrue(BattleQaFeatureSwitches.IsConnected(BattleQaFeature.戦況イベント));
            Assert.AreEqual(2, BattleQaFeatureSwitches.CurrentVersion, "援軍の接続で解釈が変わった＝版を上げる");

            var on = BattleQaFeatureSwitches.FixedDefault.With(null, BattleQaFeature.援軍, true);
            CollectionAssert.IsEmpty(on.Validate(), "接続済みの援軍ON を拒否した");
            StringAssert.StartsWith("切り分け", on.Classification, "援軍ON が固定合格の比較対象になった");
            StringAssert.Contains("BattleSetup", BattleQaFeatureSwitches.ReinforcementConnectionNote);
            StringAssert.Contains("StrategySession.Reinforcements は触らない", BattleQaFeatureSwitches.ReinforcementConnectionNote);

            CollectionAssert.IsEmpty(BattleQaFeatureSwitches.FixedDefault.With(null, BattleQaFeature.自動包囲, false).Validate());
            CollectionAssert.IsEmpty(BattleQaFeatureSwitches.FixedDefault.With(null, BattleQaFeature.戦況イベント, true).Validate());
            // 版違いは引き続き拒否（援軍ON でも）。
            var old = new BattleQaFeatureSwitches("旧版", 1, true, true, false);
            Assert.AreEqual(1, old.Validate().Count);
        }

        [Test]
        public void Validate_RejectsVersionMismatch()
        {
            var old = new BattleQaFeatureSwitches("旧版", BattleQaFeatureSwitches.CurrentVersion + 1, true, false, false);
            var errors = old.Validate();
            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("版が違う", errors[0]);
        }

        [Test]
        public void Classification_SeparatesFixedIsolationAndNaturalEvents()
        {
            StringAssert.StartsWith("固定", BattleQaFeatureSwitches.FixedDefault.Classification);
            StringAssert.StartsWith("切り分け", BattleQaFeatureSwitches.FixedDefault.With(null, BattleQaFeature.自動包囲, false).Classification);
            var evt = BattleQaFeatureSwitches.FixedDefault.With(null, BattleQaFeature.戦況イベント, true);
            StringAssert.StartsWith("自然イベントONモード", evt.Classification);
            StringAssert.Contains("比較対象外", evt.Classification);
            // 自動包囲OFFと併用しても自然イベントON扱い（比較対象外のまま）。
            StringAssert.StartsWith("自然イベントONモード", evt.With(null, BattleQaFeature.自動包囲, false).Classification);
        }

        [Test]
        public void Describe_RecordsNameVersionEachSwitchAndClassification()
        {
            string s = BattleQaFeatureSwitches.FixedDefault.With("比較A", BattleQaFeature.自動包囲, false).Describe();
            StringAssert.Contains("機能スイッチ「比較A」版" + BattleQaFeatureSwitches.CurrentVersion, s);
            StringAssert.Contains("自動包囲=OFF（既定から変更）", s);
            StringAssert.Contains("援軍=OFF", s);
            StringAssert.Contains("戦況イベント=OFF", s);
            StringAssert.DoesNotContain("援軍=OFF（既定から変更）", s);
            StringAssert.Contains("分類＝切り分け", s);
        }

        [Test]
        public void TargetName_NamesRealComponent()
        {
            StringAssert.Contains("BattlefieldCommandManager.autoEnvelopment", BattleQaFeatureSwitches.TargetName(BattleQaFeature.自動包囲));
            StringAssert.Contains("BattleEventManager", BattleQaFeatureSwitches.TargetName(BattleQaFeature.戦況イベント));
            StringAssert.Contains("BattleSetup の時限増援", BattleQaFeatureSwitches.TargetName(BattleQaFeature.援軍));
            StringAssert.DoesNotContain("未接続", BattleQaFeatureSwitches.TargetName(BattleQaFeature.援軍));
            StringAssert.DoesNotContain("joinEncirclement", BattleQaFeatureSwitches.TargetName(BattleQaFeature.自動包囲));
        }
    }
}
