using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 暗号解読を固定する：解析は傍受量×解析力で積む、先読み確率は進捗比例（上限あり）、
    /// 使用率が発覚を招く（読めても動かなければ源泉は守られる＝ウルトラのジレンマ）、暗号更新で振り出し。
    /// 境界・決定論を担保。
    /// </summary>
    public class CodebreakingRulesTests
    {
        private static readonly CodebreakingParams P = CodebreakingParams.Default;
        // 解析0.05/読み上限0.8/露見感度0.5

        [Test]
        public void AnalysisTick_VolumeTimesSkill()
        {
            // 多弁な敵×優秀な解析班＝0.05/dt
            Assert.AreEqual(0.05f, CodebreakingRules.AnalysisTick(0f, 1f, 1f, 1f, P), 1e-5f);
            // 無線封止の敵（traffic=0）からは何も学べない
            Assert.AreEqual(0.5f, CodebreakingRules.AnalysisTick(0.5f, 0f, 1f, 1f, P), 1e-5f);
            // 上限1
            Assert.AreEqual(1f, CodebreakingRules.AnalysisTick(0.99f, 1f, 1f, 10f, P), 1e-5f);
        }

        [Test]
        public void ReadChance_CappedBelowCertainty()
        {
            Assert.AreEqual(0.8f, CodebreakingRules.ReadChance(1f, P), 1e-5f);  // 完全解読でも8割
            Assert.AreEqual(0.4f, CodebreakingRules.ReadChance(0.5f, P), 1e-5f);
            Assert.AreEqual(0f, CodebreakingRules.ReadChance(0f, P), 1e-5f);
        }

        [Test]
        public void ReadsIntent_DeterministicByRoll()
        {
            Assert.IsTrue(CodebreakingRules.ReadsIntent(1f, 0.79f, P));
            Assert.IsFalse(CodebreakingRules.ReadsIntent(1f, 0.81f, P));
        }

        [Test]
        public void UltraDilemma_UsageInvitesDetection()
        {
            // 読めても動かない＝源泉は安全
            Assert.AreEqual(0f, CodebreakingRules.ExploitationDetectionChance(0f, P), 1e-5f);
            // フル活用＝0.5 の発覚率
            Assert.AreEqual(0.5f, CodebreakingRules.ExploitationDetectionChance(1f, P), 1e-5f);
            Assert.IsTrue(CodebreakingRules.EnemyNotices(1f, 0.49f, P));
            Assert.IsFalse(CodebreakingRules.EnemyNotices(1f, 0.51f, P));
        }

        [Test]
        public void CipherReset_BackToZero()
        {
            Assert.AreEqual(0f, CodebreakingRules.CipherReset(), 1e-5f);
        }
    }
}
