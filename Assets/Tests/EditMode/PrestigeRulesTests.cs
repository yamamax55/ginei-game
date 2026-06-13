using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 声望モデル（KORY-1 #1406・項羽と劉邦＝陣営の声望が人材を引き寄せる磁力）の純ロジック検証。
    /// 既定 <see cref="PrestigeParams.Default"/>（徳望0.45/戦勝0.25/遇し方0.3・流入0.3/流出0.3・厚遇ゲイン0.2・
    /// 出来事時定数60・勢いブースト0.5・磁石閾値0.6）で期待値を固定する。
    /// </summary>
    public class PrestigeRulesTests
    {
        const float Eps = 1e-4f;
        static PrestigeParams P => PrestigeParams.Default;

        /// <summary>陣営声望＝徳望×戦勝×人材の遇し方の加重平均（徳望が最も重い＝武勇より度量）。</summary>
        [Test]
        public void FactionPrestige_重み付き平均で徳望が最も効く()
        {
            // 0.45*1 + 0.25*0 + 0.3*0 = 0.45 / 1.0 = 0.45
            float virtueOnly = PrestigeRules.FactionPrestige(1f, 0f, 0f, P);
            // 0.25*1 = 0.25
            float victoryOnly = PrestigeRules.FactionPrestige(0f, 1f, 0f, P);
            Assert.AreEqual(0.45f, virtueOnly, Eps);
            Assert.AreEqual(0.25f, victoryOnly, Eps);
            // 同じ値での戦勝より徳望の寄与が大きい＝劉邦型（人を遇する度量が声望を生む）
            Assert.Greater(virtueOnly, victoryOnly);
            // 全部1なら声望も1
            Assert.AreEqual(1f, PrestigeRules.FactionPrestige(1f, 1f, 1f, P), Eps);
        }

        /// <summary>人材の磁力＝声望×活躍の場の積（どちらか欠けると引き寄せられない）。</summary>
        [Test]
        public void TalentMagnetism_声望と機会の積()
        {
            Assert.AreEqual(0.48f, PrestigeRules.TalentMagnetism(0.8f, 0.6f), Eps);
            // 活躍の場が無ければ声望が高くても磁力0
            Assert.AreEqual(0f, PrestigeRules.TalentMagnetism(1f, 0f), Eps);
            // 声望が無ければ場があっても磁力0
            Assert.AreEqual(0f, PrestigeRules.TalentMagnetism(0f, 1f), Eps);
        }

        /// <summary>人材流入＝磁力が高く競合より魅力的なほど集まる（劉邦に人が集まる）。</summary>
        [Test]
        public void TalentInflux_競合より魅力的なほど流入()
        {
            // 0.6 * (1-0.2) * 0.3 * 2 = 0.288
            float vsWeakRival = PrestigeRules.TalentInflux(0.6f, 0.2f, 2f, P);
            Assert.AreEqual(0.288f, vsWeakRival, Eps);
            // 競合が魅力的（rivalPrestige 高）なら流入は鈍る
            float vsStrongRival = PrestigeRules.TalentInflux(0.6f, 0.9f, 2f, P);
            Assert.Less(vsStrongRival, vsWeakRival);
            // 競合が完全に魅力的なら流入0
            Assert.AreEqual(0f, PrestigeRules.TalentInflux(0.6f, 1f, 2f, P), Eps);
        }

        /// <summary>人材流出＝声望が落ち冷遇すると才人が去る（項羽から范増が去る）。</summary>
        [Test]
        public void TalentExodus_低声望と冷遇で流出()
        {
            // (1-0.3) * 0.8 * 0.3 * 2 = 0.336
            float low = PrestigeRules.TalentExodus(0.3f, 0.8f, 2f, P);
            Assert.AreEqual(0.336f, low, Eps);
            // 高声望なら冷遇しても流出は小さい
            float high = PrestigeRules.TalentExodus(0.9f, 0.8f, 2f, P);
            Assert.Less(high, low);
            // 冷遇しなければ流出0
            Assert.AreEqual(0f, PrestigeRules.TalentExodus(0.3f, 0f, 2f, P), Eps);
        }

        /// <summary>厚遇は声望を上げる好循環（人を活かすと評判が立つ）。</summary>
        [Test]
        public void PrestigeFromTreatment_厚遇で声望が上がる好循環()
        {
            // 0.8 * 0.9 * 0.2 = 0.144
            Assert.AreEqual(0.144f, PrestigeRules.PrestigeFromTreatment(0.8f, 0.9f, P), Eps);
            // 定着も寛大さも無ければ増分0
            Assert.AreEqual(0f, PrestigeRules.PrestigeFromTreatment(0f, 0.9f, P), Eps);
            Assert.AreEqual(0f, PrestigeRules.PrestigeFromTreatment(0.8f, 0f, P), Eps);
        }

        /// <summary>声望は出来事で上下する（寛大は上げ・裏切りは下げる）。</summary>
        [Test]
        public void PrestigeTick_出来事へ漸近して上下()
        {
            // t = 30/60 = 0.5; Lerp(0.4, 1.0, 0.5) = 0.7（好評で上昇）
            float up = PrestigeRules.PrestigeTick(0.4f, 1f, 30f, P);
            Assert.AreEqual(0.7f, up, Eps);
            // Lerp(0.8, 0.0, 0.5) = 0.4（悪評で下降）
            float down = PrestigeRules.PrestigeTick(0.8f, 0f, 30f, P);
            Assert.AreEqual(0.4f, down, Eps);
            // dt=0 なら据え置き
            Assert.AreEqual(0.5f, PrestigeRules.PrestigeTick(0.5f, 1f, 0f, P), Eps);
        }

        /// <summary>勢いに乗る陣営はさらに人材を集める（趨勢が磁力を増幅＝勝ち馬に乗る）。</summary>
        [Test]
        public void MomentumEffect_勢いが磁力を増幅()
        {
            // 0.5 * (1 + 1*0.5) = 0.75（勢い満点で1.5倍）
            float withMomentum = PrestigeRules.MomentumEffect(1f, 0.5f, P);
            Assert.AreEqual(0.75f, withMomentum, Eps);
            // 勢い0なら声望そのまま
            Assert.AreEqual(0.5f, PrestigeRules.MomentumEffect(0f, 0.5f, P), Eps);
            // 勢いがあるほど大きい
            Assert.Greater(withMomentum, PrestigeRules.MomentumEffect(0f, 0.5f, P));
        }

        /// <summary>人材磁石判定＝磁力が閾値以上で才人が自ら集まる陣営（劉邦陣営）。</summary>
        [Test]
        public void IsTalentMagnet_閾値で人材磁石を判定()
        {
            // 既定閾値0.6
            Assert.IsTrue(PrestigeRules.IsTalentMagnet(0.7f, -1f, P));
            Assert.IsFalse(PrestigeRules.IsTalentMagnet(0.5f, -1f, P));
            // 明示閾値
            Assert.IsTrue(PrestigeRules.IsTalentMagnet(0.5f, 0.4f, P));
            Assert.IsFalse(PrestigeRules.IsTalentMagnet(0.3f, 0.4f, P));
        }
    }
}
