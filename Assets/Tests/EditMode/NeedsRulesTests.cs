using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// マズロー欲求段階（#403）を固定する：下位（生理→…→自己超越）が満たされて初めて上位が
    /// 動機になる。DominantNeed は下位優先で未充足層を返し、Motivation は前提が揃うほど強くなり、
    /// MoraleContribution は下層欠乏を重く罰する。境界・クランプ・各分岐を決定論で検証。
    /// </summary>
    public class NeedsRulesTests
    {
        private static readonly NeedsParams P = NeedsParams.Default; // 充足閾値0.7 / 最大動機1 / 士気1 / 欠乏係数0.5

        private static float[] All(float v) => new[] { v, v, v, v, v, v };

        // --- DominantNeed ---

        [Test]
        public void DominantNeed_ReturnsLowestUnsatisfied()
        {
            // 生理・安全は満たされ、所属が欠ける → 所属が支配的（下位優先）
            var sat = new[] { 0.9f, 0.8f, 0.3f, 0.9f, 0.9f, 0.9f };
            Assert.AreEqual(NeedLevel.所属, NeedsRules.DominantNeed(sat, P));
        }

        [Test]
        public void DominantNeed_LowerLayerWins_OverHigherDeficit()
        {
            // 生理が欠ければ、上位がもっと欠けていても生理が支配的（下位優先）
            var sat = new[] { 0.4f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f };
            Assert.AreEqual(NeedLevel.生理, NeedsRules.DominantNeed(sat, P));
        }

        [Test]
        public void DominantNeed_AllSatisfied_ReturnsTopLevel()
        {
            // 全層が閾値以上 → 最上層（自己超越）が支配的
            Assert.AreEqual(NeedLevel.自己超越, NeedsRules.DominantNeed(All(1f), P));
        }

        [Test]
        public void DominantNeed_NullOrEmpty_ReturnsBaseLayer()
        {
            Assert.AreEqual(NeedLevel.生理, NeedsRules.DominantNeed(null, P));
            Assert.AreEqual(NeedLevel.生理, NeedsRules.DominantNeed(new float[0], P));
        }

        [Test]
        public void DominantNeed_ThresholdBoundary_IsSatisfiedAtThreshold()
        {
            // ちょうど閾値0.7 は「満たされた」＝未充足ではない → 次層が支配的
            var sat = new[] { 0.7f, 0.5f, 0.9f, 0.9f, 0.9f, 0.9f };
            Assert.AreEqual(NeedLevel.安全, NeedsRules.DominantNeed(sat, P));
        }

        // --- Motivation ---

        [Test]
        public void Motivation_StrongWhenPrerequisitesMet_AndOwnDeficit()
        {
            // 下位（生理・安全）満点、所属が欠乏 → 前提1.0×欠乏0.8×最大1 = 0.8
            var sat = new[] { 1f, 1f, 0.2f, 0.5f, 0.5f, 0.5f };
            Assert.AreEqual(0.8f, NeedsRules.Motivation(sat, P), 1e-4f);
        }

        [Test]
        public void Motivation_WeakWhenLowerLayerMissing()
        {
            // 生理が欠ける（dominant=生理、前提層なし＝1）→ 欠乏0.7 → 0.7
            var sat = new[] { 0.3f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f };
            Assert.AreEqual(0.7f, NeedsRules.Motivation(sat, P), 1e-4f);
        }

        [Test]
        public void Motivation_AllSatisfied_TopHasLowMotivation()
        {
            // 全層満点 → dominant=自己超越、欠乏0 → 動機0
            Assert.AreEqual(0f, NeedsRules.Motivation(All(1f), P), 1e-4f);
        }

        [Test]
        public void Motivation_PrerequisiteDampensMotivation()
        {
            // 生理0.5（満たされず）→ dominant=生理、前提なし→欠乏0.5=0.5。所属が dominant でないので前提積は効かない
            var low = new[] { 0.5f, 1f, 0.0f, 0.0f, 0.0f, 0.0f };
            Assert.AreEqual(0.5f, NeedsRules.Motivation(low, P), 1e-4f);

            // 生理・安全が0.8（満たされる）で所属欠乏 → 前提0.8×0.8=0.64×欠乏1.0=0.64
            var high = new[] { 0.8f, 0.8f, 0.0f, 0.0f, 0.0f, 0.0f };
            Assert.AreEqual(0.64f, NeedsRules.Motivation(high, P), 1e-4f);
        }

        [Test]
        public void Motivation_NullOrEmpty_IsZero()
        {
            Assert.AreEqual(0f, NeedsRules.Motivation(null, P), 1e-4f);
            Assert.AreEqual(0f, NeedsRules.Motivation(new float[0], P), 1e-4f);
        }

        // --- MoraleContribution ---

        [Test]
        public void MoraleContribution_FullSatisfaction_IsMax()
        {
            // 全層満点 → 平均1・欠乏0 → 寄与＝moraleScale=1
            Assert.AreEqual(1f, NeedsRules.MoraleContribution(All(1f), P), 1e-4f);
        }

        [Test]
        public void MoraleContribution_ZeroSatisfaction_IsZero()
        {
            // 全層0 → 平均0・大きなペナルティ → クランプで0下限
            Assert.AreEqual(0f, NeedsRules.MoraleContribution(All(0f), P), 1e-4f);
        }

        [Test]
        public void MoraleContribution_LowerDeficitHurtsMore()
        {
            // 同じ「1層だけ欠乏」でも、下層が欠けるほど士気は低い
            var lowerMissing = new[] { 0f, 1f, 1f, 1f, 1f, 1f };  // 生理欠乏
            var upperMissing = new[] { 1f, 1f, 1f, 1f, 1f, 0f };  // 自己超越欠乏
            float lower = NeedsRules.MoraleContribution(lowerMissing, P);
            float upper = NeedsRules.MoraleContribution(upperMissing, P);
            Assert.Less(lower, upper);
        }

        [Test]
        public void MoraleContribution_ClampedWithinScale()
        {
            // どんな入力でも 0..moraleScale に収まる（範囲外値も Clamp01 で吸収）
            var weird = new[] { -5f, 2f, 0.5f, 3f, -1f, 0.9f };
            float m = NeedsRules.MoraleContribution(weird, P);
            Assert.GreaterOrEqual(m, 0f);
            Assert.LessOrEqual(m, P.moraleScale);
        }

        [Test]
        public void MoraleContribution_NullOrEmpty_IsZero()
        {
            Assert.AreEqual(0f, NeedsRules.MoraleContribution(null, P), 1e-4f);
            Assert.AreEqual(0f, NeedsRules.MoraleContribution(new float[0], P), 1e-4f);
        }

        // --- Default オーバーロード（既定パラメータ経路） ---

        [Test]
        public void DefaultOverloads_MatchExplicitParams()
        {
            var sat = new[] { 1f, 0.8f, 0.3f, 0.5f, 0.5f, 0.5f };
            Assert.AreEqual(NeedsRules.DominantNeed(sat, P), NeedsRules.DominantNeed(sat));
            Assert.AreEqual(NeedsRules.Motivation(sat, P), NeedsRules.Motivation(sat), 1e-4f);
            Assert.AreEqual(NeedsRules.MoraleContribution(sat, P), NeedsRules.MoraleContribution(sat), 1e-4f);
        }
    }
}
