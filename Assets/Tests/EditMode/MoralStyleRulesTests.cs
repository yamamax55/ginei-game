using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// MoralStyleRules（スミス三徳の統治スタイル・TMS-3 #1586）の EditMode テスト。各徳の効果・統合安定度修正子・
    /// 徳のバランス・正義の土台ゲート・支配的な徳・徳の過剰を既定 Params の具体値で固定する。
    /// </summary>
    public class MoralStyleRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>各徳の効果＝徳レベル×重み（慎慮0.25/仁愛0.25/正義0.5＝正義が土台で最大）。</summary>
        [Test]
        public void 各徳の効果は重みに比例し正義の重みが最大()
        {
            // 既定重み：慎慮0.25・仁愛0.25・正義0.5
            Assert.AreEqual(0.25f, MoralStyleRules.PrudenceEffect(1f), Eps);
            Assert.AreEqual(0.25f, MoralStyleRules.BenevolenceEffect(1f), Eps);
            Assert.AreEqual(0.5f, MoralStyleRules.JusticeEffect(1f), Eps);
            // 正義の効果は慎慮・仁愛の倍＝大黒柱の重み
            Assert.Greater(MoralStyleRules.JusticeEffect(1f), MoralStyleRules.PrudenceEffect(1f));
            // 0 は 0
            Assert.AreEqual(0f, MoralStyleRules.JusticeEffect(0f), Eps);
        }

        /// <summary>三徳満点なら統合安定度修正子は最大（正義効果0.5＋ゲート全開の他徳0.5＝1.0にクランプ）。</summary>
        [Test]
        public void 三徳満点で統合修正子は最大()
        {
            // pr=be=ju=1: ju効果0.5、他徳(pr+be)=0.5、正義1.0でゲート全開→0.5、合計1.0
            float mod = MoralStyleRules.StabilityModifier(1f, 1f, 1f);
            Assert.AreEqual(1f, mod, Eps);
        }

        /// <summary>正義ゼロでは慎慮・仁愛がいかに高くても安定が崩れる＝大黒柱が抜けると建物が倒れる。</summary>
        [Test]
        public void 正義ゼロでは他徳満点でも修正子が大きく落ちる()
        {
            // pr=be=1, ju=0: ju効果0、他徳0.5にゲート係数(1-0.7)+0.7*0=0.3 → 0.15、合計0.15
            float mod = MoralStyleRules.StabilityModifier(1f, 1f, 0f);
            Assert.AreEqual(0.15f, mod, Eps);
            // 正義が満点なら同じ他徳でも遥かに高い
            float withJustice = MoralStyleRules.StabilityModifier(1f, 1f, 1f);
            Assert.Greater(withJustice, mod);
        }

        /// <summary>正義の土台ゲート：正義1で全開（他徳そのまま）・正義0でgateStrength分だけ閉じる。</summary>
        [Test]
        public void 正義の土台ゲートは正義が低いほど他徳を殺す()
        {
            // 正義1.0：ゲート係数1.0 → 他徳0.8 そのまま
            Assert.AreEqual(0.8f, MoralStyleRules.JusticeAsFoundation(1f, 0.8f), Eps);
            // 正義0.0：ゲート係数(1-0.7)=0.3 → 0.8*0.3=0.24
            Assert.AreEqual(0.24f, MoralStyleRules.JusticeAsFoundation(0f, 0.8f), Eps);
            // 正義0.5：ゲート係数0.3+0.7*0.5=0.65 → 0.8*0.65=0.52
            Assert.AreEqual(0.52f, MoralStyleRules.JusticeAsFoundation(0.5f, 0.8f), Eps);
        }

        /// <summary>徳のバランス：三徳が等しいほど高い（均衡＝1.0）、一つに極端に偏ると低い。</summary>
        [Test]
        public void 徳のバランスは均衡で最大偏りで最小()
        {
            // 完全均衡（全て等しい）＝1.0
            Assert.AreEqual(1f, MoralStyleRules.VirtueBalance(0.5f, 0.5f, 0.5f), Eps);
            Assert.AreEqual(1f, MoralStyleRules.VirtueBalance(1f, 1f, 1f), Eps);
            // 一つだけ満点で他0＝最大の偏り＝0
            Assert.AreEqual(0f, MoralStyleRules.VirtueBalance(1f, 0f, 0f), Eps);
            // 中間の偏りはその間
            float partial = MoralStyleRules.VirtueBalance(1f, 0.5f, 0f);
            Assert.Greater(partial, 0f);
            Assert.Less(partial, 1f);
        }

        /// <summary>支配的な徳：最大の徳を返す。同値は慎慮→仁愛→正義の順。全0は土台の正義。</summary>
        [Test]
        public void 支配的な徳は最大の徳を返す()
        {
            Assert.AreEqual(MoralVirtue.正義, MoralStyleRules.DominantVirtue(0.2f, 0.3f, 0.9f));
            Assert.AreEqual(MoralVirtue.仁愛, MoralStyleRules.DominantVirtue(0.2f, 0.8f, 0.3f));
            Assert.AreEqual(MoralVirtue.慎慮, MoralStyleRules.DominantVirtue(0.9f, 0.3f, 0.3f));
            // すべて0＝土台の正義
            Assert.AreEqual(MoralVirtue.正義, MoralStyleRules.DominantVirtue(0f, 0f, 0f));
        }

        /// <summary>徳の過剰：閾値以下は弊害0（健全）、超えると弊害が増す＝中庸を是とする。</summary>
        [Test]
        public void 徳の過剰は閾値超過で弊害となる()
        {
            // 既定閾値0.85。閾値以下＝0
            Assert.AreEqual(0f, MoralStyleRules.ExcessOfVirtue(0.85f), Eps);
            Assert.AreEqual(0f, MoralStyleRules.ExcessOfVirtue(0.5f), Eps);
            // 満点＝最大の弊害（(1-0.85)/(1-0.85)=1.0）
            Assert.AreEqual(1f, MoralStyleRules.ExcessOfVirtue(1f), Eps);
            // 中間：threshold=0.8, level=0.9 → (0.9-0.8)/0.2=0.5
            Assert.AreEqual(0.5f, MoralStyleRules.ExcessOfVirtue(0.9f, 0.8f), Eps);
        }

        /// <summary>全入力クランプ：範囲外でも 0..1 に収まる（決定論・基準値非破壊）。</summary>
        [Test]
        public void 範囲外入力はクランプされる()
        {
            Assert.AreEqual(0.5f, MoralStyleRules.JusticeEffect(5f), Eps);   // 上クランプ
            Assert.AreEqual(0f, MoralStyleRules.BenevolenceEffect(-3f), Eps); // 下クランプ
            float mod = MoralStyleRules.StabilityModifier(-1f, 2f, 3f);
            Assert.GreaterOrEqual(mod, 0f);
            Assert.LessOrEqual(mod, 1f);
        }
    }
}
