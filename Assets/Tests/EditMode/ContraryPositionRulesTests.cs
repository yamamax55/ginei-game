using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>逆張り迫害・勝利構造（MNIA-3 #1624）の純ロジック検証。</summary>
    public class ContraryPositionRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>迫害コスト＝熱狂×可視性×強度（ピークで目立つ逆張りが最も叩かれる）。</summary>
        [Test]
        public void PersecutionCost_熱狂と可視性の積で増える()
        {
            // 0.9 * 0.8 * 1.0 = 0.72
            Assert.AreEqual(0.72f, ContraryPositionRules.PersecutionCost(0.9f, 0.8f), Eps);
            // 熱狂0＝群集がいない＝迫害なし
            Assert.AreEqual(0f, ContraryPositionRules.PersecutionCost(0f, 1f), Eps);
            // 可視性0＝隠れて逆らう＝コストなし
            Assert.AreEqual(0f, ContraryPositionRules.PersecutionCost(1f, 0f), Eps);
        }

        /// <summary>名声逆転利得は崩壊後にのみ生じ、逆らった熱狂が高いほど跳ねる。</summary>
        [Test]
        public void VindicationGain_崩壊後に高い熱狂への逆張りが報われる()
        {
            // 崩壊前はまだ群集が正しいと信じる＝利得0
            Assert.AreEqual(0f, ContraryPositionRules.VindicationGain(0.9f, false), Eps);
            // 崩壊後：0.5*1.2 = 0.6
            Assert.AreEqual(0.6f, ContraryPositionRules.VindicationGain(0.5f, true), Eps);
            // 崩壊後：0.9*1.2=1.08 → 1.0 にクランプ
            Assert.AreEqual(1.0f, ContraryPositionRules.VindicationGain(0.9f, true), Eps);
        }

        /// <summary>純損益＝生き残れば逆転純益、潰されれば迫害損のみ（早すぎる正しさは破滅と紙一重）。</summary>
        [Test]
        public void NetPayoff_生存で逆転_死で迫害損のみ()
        {
            // 生存：1.0 - 0.72 = 0.28
            Assert.AreEqual(0.28f, ContraryPositionRules.NetPayoff(0.72f, 1.0f, true), Eps);
            // 潰された：利得を回収できず迫害損のみ -0.72
            Assert.AreEqual(-0.72f, ContraryPositionRules.NetPayoff(0.72f, 1.0f, false), Eps);
        }

        /// <summary>生存判定＝決定論 roll。低 roll で生存、高 roll で潰される。</summary>
        [Test]
        public void SurvivalChance_迫害をrollで生き延びる()
        {
            // chance = 0.8*0.6 + (1-0.72)*0.4 - 0.72*(1-0.8) = 0.48 + 0.112 - 0.144 = 0.448
            Assert.IsTrue(ContraryPositionRules.SurvivalChance(0.8f, 0.72f, 0.4f));
            Assert.IsFalse(ContraryPositionRules.SurvivalChance(0.8f, 0.72f, 0.5f));
        }

        /// <summary>レジリエンス0かつ重い迫害なら生存しづらい。</summary>
        [Test]
        public void SurvivalChance_脆弱で重い迫害は潰れやすい()
        {
            // chance = 0*0.6 + (1-1)*0.4 - 1*(1-0)=−1 → clamp01 → 0
            Assert.IsFalse(ContraryPositionRules.SurvivalChance(0f, 1f, 0f));
            // レジリエンス満点なら迫害が重くても生き残れる（chance=0.6 + 0 - 0 = 0.6）
            Assert.IsTrue(ContraryPositionRules.SurvivalChance(1f, 1f, 0.5f));
        }

        /// <summary>タイミング品質＝ピーク近接ほど高い（ピーク直撃で最大）。</summary>
        [Test]
        public void TimingQuality_ピーク近接で高い()
        {
            // closeness = 1-0=1 → 1^1.5 = 1（ピーク直撃）
            Assert.AreEqual(1f, ContraryPositionRules.TimingQuality(1f, 1f), Eps);
            // closeness = 1-0.1=0.9 → 0.9^1.5 ≈ 0.8538
            Assert.AreEqual(0.8538f, ContraryPositionRules.TimingQuality(0.9f, 1.0f), 1e-3f);
            // 遠い：closeness = 1-0.9=0.1 → 0.1^1.5 ≈ 0.0316（早すぎ＝迫害だけ食らう側）
            Assert.AreEqual(0.0316f, ContraryPositionRules.TimingQuality(0.1f, 1.0f), 1e-3f);
        }

        /// <summary>予言者認知＝逆転利得がしきい値以上で成立。</summary>
        [Test]
        public void IsProphet_十分な逆転利得で予言者()
        {
            // 既定しきい値0.6
            Assert.IsTrue(ContraryPositionRules.IsProphet(1.0f));
            Assert.IsFalse(ContraryPositionRules.IsProphet(0.5f));
            // 明示しきい値
            Assert.IsTrue(ContraryPositionRules.IsProphet(0.6f, 0.6f));
        }

        /// <summary>パラメータ既定値の確認。</summary>
        [Test]
        public void Params_既定値()
        {
            var p = ContraryPositionParams.Default;
            Assert.AreEqual(1f, p.persecutionScale, Eps);
            Assert.AreEqual(1.2f, p.vindicationScale, Eps);
            Assert.AreEqual(0.6f, p.resilienceWeight, Eps);
            Assert.AreEqual(1.5f, p.timingSharpness, Eps);
            Assert.AreEqual(0.6f, p.prophetThreshold, Eps);
        }
    }
}
