using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>縦深抵抗（ロシア戦役型・#1413）の純ロジックテスト。既定Paramsで期待値を固定。</summary>
    public class HomelandResistanceRulesTests
    {
        const float Tol = 0.0005f;

        /// <summary>補給線の負担＝侵攻が深いほど（地形が険しいほど）非線形に増す。</summary>
        [Test]
        public void SupplyLineStrain_深いほど険しいほど補給負担が増す()
        {
            // 深度0.5^1.5=0.353553、地形0.4×重み0.5で×1.2 → 0.424264
            float strain = HomelandResistanceRules.SupplyLineStrain(0.5f, 0.4f);
            Assert.AreEqual(0.424264f, strain, Tol);

            // 本国直上（深度0）は負担なし
            Assert.AreEqual(0f, HomelandResistanceRules.SupplyLineStrain(0f, 1f), Tol);
            // より深いほど負担は単調増加
            Assert.Greater(HomelandResistanceRules.SupplyLineStrain(0.9f, 0.4f),
                HomelandResistanceRules.SupplyLineStrain(0.5f, 0.4f));
        }

        /// <summary>抵抗の増幅＝深く入るほど（抗戦心が高いほど）住民の抵抗が増幅する。</summary>
        [Test]
        public void ResistanceAmplification_深いほど抗戦心が高いほど抵抗が増幅()
        {
            // 深度0.6^2=0.36、抗戦0.5×重み0.6で×1.3 → 0.468
            float r = HomelandResistanceRules.ResistanceAmplification(0.6f, 0.5f);
            Assert.AreEqual(0.468f, r, Tol);

            // 深度0なら抵抗なし
            Assert.AreEqual(0f, HomelandResistanceRules.ResistanceAmplification(0f, 1f), Tol);
            // 祖国防衛の意志が高いほど抵抗が燃える
            Assert.Greater(HomelandResistanceRules.ResistanceAmplification(0.6f, 1f),
                HomelandResistanceRules.ResistanceAmplification(0.6f, 0f));
        }

        /// <summary>パルチザン圧力＝占領地の敵意が時間で組織化し外部支援が後押しする。</summary>
        [Test]
        public void PartisanPressure_時間と外部支援で敵意が育つ()
        {
            // 既存0.5＋成長0.1×0.5×(1+0.5×0.4)×1=0.06 → 0.56
            float next = HomelandResistanceRules.PartisanPressure(0.5f, 0.5f, 1.0f);
            Assert.AreEqual(0.56f, next, Tol);

            // 外部支援が多いほど成長が速い
            Assert.Greater(HomelandResistanceRules.PartisanPressure(0.5f, 1f, 1f),
                HomelandResistanceRules.PartisanPressure(0.5f, 0f, 1f));
            // dt=0なら変化なし
            Assert.AreEqual(0.5f, HomelandResistanceRules.PartisanPressure(0.5f, 1f, 0f), Tol);
        }

        /// <summary>侵攻継続コスト＝深度とともに非線形に増し補給難が乗算で増幅する。</summary>
        [Test]
        public void DepthAdvanceCost_深追いの代償が非線形に増す()
        {
            // 深度0.6^2=0.36、補給負担0.4で×1.4 → 0.504
            float cost = HomelandResistanceRules.DepthAdvanceCost(0.6f, 0.4f);
            Assert.AreEqual(0.504f, cost, Tol);

            // 補給が細るほど同じ前進が高くつく
            Assert.Greater(HomelandResistanceRules.DepthAdvanceCost(0.6f, 1f),
                HomelandResistanceRules.DepthAdvanceCost(0.6f, 0f));
        }

        /// <summary>過伸張ペナルティ＝持続可能深度を超えると一気に不利になる（攻勢終末点）。</summary>
        [Test]
        public void OverreachPenalty_持続可能深度の超過で破綻()
        {
            // 持続可能深度内なら無傷
            Assert.AreEqual(0f, HomelandResistanceRules.OverreachPenalty(0.5f, 0.5f), Tol);

            // 深度0.8 超過0.3、伸びしろ0.5で正規化0.6、0.6^2=0.36
            float pen = HomelandResistanceRules.OverreachPenalty(0.8f, 0.5f);
            Assert.AreEqual(0.36f, pen, Tol);

            // 上限0.9でクランプ（持続可能0でも最深部）
            Assert.AreEqual(0.9f, HomelandResistanceRules.OverreachPenalty(1f, 0f), Tol);
        }

        /// <summary>縦深防御の利＝広大な国土と空間を譲る覚悟の両方が揃って侵攻軍を呑み込む。</summary>
        [Test]
        public void DefenderDepthAdvantage_広い国土と空間譲渡の覚悟の積()
        {
            // 国土0.8×覚悟0.5=0.4
            Assert.AreEqual(0.4f, HomelandResistanceRules.DefenderDepthAdvantage(0.8f, 0.5f), Tol);

            // 広い国土でも譲る覚悟がゼロなら縦深は活きない（積）
            Assert.AreEqual(0f, HomelandResistanceRules.DefenderDepthAdvantage(1f, 0f), Tol);
            Assert.AreEqual(0f, HomelandResistanceRules.DefenderDepthAdvantage(0f, 1f), Tol);
        }

        /// <summary>侵攻の頓挫＝深度・補給難・抵抗の三者が揃うと攻勢が頓挫する（積）。</summary>
        [Test]
        public void InvasionCulmination_三拍子揃うと攻勢が頓挫()
        {
            // 0.8×0.5×0.5=0.2
            Assert.AreEqual(0.2f, HomelandResistanceRules.InvasionCulmination(0.8f, 0.5f, 0.5f), Tol);

            // どれか一つでもゼロなら頓挫しない（深く入っても補給が保てば呑まれない）
            Assert.AreEqual(0f, HomelandResistanceRules.InvasionCulmination(0.9f, 0f, 0.9f), Tol);
            // 三拍子揃うほど頓挫度が上がる
            Assert.Greater(HomelandResistanceRules.InvasionCulmination(0.9f, 0.9f, 0.9f),
                HomelandResistanceRules.InvasionCulmination(0.5f, 0.5f, 0.5f));
        }

        /// <summary>泥沼判定＝侵攻深度×抵抗の増幅が閾値を超えたら奥地で身動きが取れない。</summary>
        [Test]
        public void IsBoggedDownInHomeland_深度と抵抗の積で泥沼判定()
        {
            // 0.8×0.6=0.48 >= 既定閾値0.4 → 泥沼
            Assert.IsTrue(HomelandResistanceRules.IsBoggedDownInHomeland(0.8f, 0.6f));
            // 浅い侵攻は呑まれない
            Assert.IsFalse(HomelandResistanceRules.IsBoggedDownInHomeland(0.3f, 0.5f));
            // 抵抗が弱ければ深くても泥沼にならない
            Assert.IsFalse(HomelandResistanceRules.IsBoggedDownInHomeland(0.9f, 0.2f));
        }

        /// <summary>InvasionDepthState はコンストラクタで全フィールドを 0..1 にクランプする。</summary>
        [Test]
        public void InvasionDepthState_全フィールドをクランプ()
        {
            var s = new InvasionDepthState(1.5f, -0.2f, 0.5f);
            Assert.AreEqual(1f, s.penetrationDepth, Tol);
            Assert.AreEqual(0f, s.supplyLineLength, Tol);
            Assert.AreEqual(0.5f, s.occupiedHostility, Tol);
        }
    }
}
