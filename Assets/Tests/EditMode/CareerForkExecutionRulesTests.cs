using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 岐路の実行振り分け（TKO-7・<see cref="CareerForkExecutionRules"/>）を固定する：各 CareerFork→マクロ窓口の対応と、
    /// 前提（亡命=敵の受け皿／独立=拠点／政界転身=政界の空き）の充足判定。忠勤/下野は常に実行可。
    /// </summary>
    public class CareerForkExecutionRulesTests
    {
        [Test]
        public void ExecutorFor_Mapping()
        {
            Assert.AreEqual(ForkExecutor.なし, CareerForkExecutionRules.ExecutorFor(CareerFork.忠勤));
            Assert.AreEqual(ForkExecutor.退役, CareerForkExecutionRules.ExecutorFor(CareerFork.下野));
            Assert.AreEqual(ForkExecutor.亡命受入, CareerForkExecutionRules.ExecutorFor(CareerFork.亡命));
            Assert.AreEqual(ForkExecutor.独立樹立, CareerForkExecutionRules.ExecutorFor(CareerFork.独立));
            Assert.AreEqual(ForkExecutor.政界入り, CareerForkExecutionRules.ExecutorFor(CareerFork.政界転身));
        }

        [Test]
        public void CanExecute_ServeAndResign_AlwaysTrue()
        {
            Assert.IsTrue(CareerForkExecutionRules.CanExecute(CareerFork.忠勤, false, false, false));
            Assert.IsTrue(CareerForkExecutionRules.CanExecute(CareerFork.下野, false, false, false));
        }

        [Test]
        public void CanExecute_Defection_NeedsEnemyHaven()
        {
            Assert.IsFalse(CareerForkExecutionRules.CanExecute(CareerFork.亡命, hasEnemyHaven: false, false, false));
            Assert.IsTrue(CareerForkExecutionRules.CanExecute(CareerFork.亡命, hasEnemyHaven: true, false, false));
        }

        [Test]
        public void CanExecute_Independence_NeedsBase()
        {
            Assert.IsFalse(CareerForkExecutionRules.CanExecute(CareerFork.独立, false, hasIndependenceBase: false, false));
            Assert.IsTrue(CareerForkExecutionRules.CanExecute(CareerFork.独立, false, hasIndependenceBase: true, false));
        }

        [Test]
        public void CanExecute_Politics_NeedsOpening()
        {
            Assert.IsFalse(CareerForkExecutionRules.CanExecute(CareerFork.政界転身, false, false, hasPoliticalOpening: false));
            Assert.IsTrue(CareerForkExecutionRules.CanExecute(CareerFork.政界転身, false, false, hasPoliticalOpening: true));
        }
    }
}
