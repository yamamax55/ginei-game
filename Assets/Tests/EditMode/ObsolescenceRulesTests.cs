using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>艦隊陳腐化（技術世代遅れ・#1385）の純ロジック検証。既定Params具体値で期待値を固定する。</summary>
    public class ObsolescenceRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>世代差＝自艦−敵艦。敵が新しいほど負（陳腐化）、自艦が新しいほど正、同世代は0。</summary>
        [Test]
        public void GenerationGap_敵が新しいほど負()
        {
            // 自0.3 / 敵0.8 ＝ -0.5（自艦が遅れている）
            Assert.AreEqual(-0.5f, ObsolescenceRules.GenerationGap(0.3f, 0.8f), Eps);
            // 自0.9 / 敵0.4 ＝ +0.5（自艦が優位）
            Assert.AreEqual(0.5f, ObsolescenceRules.GenerationGap(0.9f, 0.4f), Eps);
            // 同世代＝0
            Assert.AreEqual(0f, ObsolescenceRules.GenerationGap(0.5f, 0.5f), Eps);
        }

        /// <summary>陳腐化ペナルティ＝世代遅れぶん×0.8で目減り、優位は1.0、下限0.2でクランプ。</summary>
        [Test]
        public void ObsolescencePenalty_世代遅れで目減りし下限で止まる()
        {
            var p = ObsolescenceParams.Default;
            // gap=-0.5 ＝ 1 - 0.5*0.8 = 0.6
            Assert.AreEqual(0.6f, ObsolescenceRules.ObsolescencePenalty(-0.5f, p), Eps);
            // gap>=0（同世代・優位）は陳腐化なし＝1.0
            Assert.AreEqual(1f, ObsolescenceRules.ObsolescencePenalty(0f, p), Eps);
            Assert.AreEqual(1f, ObsolescenceRules.ObsolescencePenalty(0.7f, p), Eps);
            // gap=-1 ＝ 1 - 1*0.8 = 0.2（ちょうど下限）
            Assert.AreEqual(0.2f, ObsolescenceRules.ObsolescencePenalty(-1f, p), Eps);
        }

        /// <summary>相対戦闘力＝生の戦力×陳腐化ペナルティ。性能健在でも時代遅れで目減りする。</summary>
        [Test]
        public void RelativeCombatPower_陳腐化で戦力が目減りする()
        {
            // 生戦力0.8・ペナルティ0.6 ＝ 0.48（性能は健在でも実効はこれだけ）
            Assert.AreEqual(0.48f, ObsolescenceRules.RelativeCombatPower(0.8f, 0.6f), Eps);
            // ペナルティ1.0（同世代）なら目減りなし
            Assert.AreEqual(0.8f, ObsolescenceRules.RelativeCombatPower(0.8f, 1.0f), Eps);
        }

        /// <summary>破壊的飛躍＝飛躍の大きさ×1.5で非連続に効き、世代差は1で頭打ち。</summary>
        [Test]
        public void DisruptiveLeap_大きな飛躍は一挙に世代差を生む()
        {
            var p = ObsolescenceParams.Default;
            // 0.4*1.5 = 0.6
            Assert.AreEqual(0.6f, ObsolescenceRules.DisruptiveLeap(0.4f, p), Eps);
            // 大きな飛躍は1で頭打ち（0.8*1.5=1.2→1.0＝一挙に旧式化）
            Assert.AreEqual(1f, ObsolescenceRules.DisruptiveLeap(0.8f, p), Eps);
        }

        /// <summary>更新圧力＝陳腐化ぶん×艦隊価値×0.6。深い陳腐化×大艦隊ほど一新の必要が高い。</summary>
        [Test]
        public void UpgradePressure_陳腐化と艦隊価値で更新圧力が立つ()
        {
            var p = ObsolescenceParams.Default;
            // (1-0.6)*1.0*0.6 = 0.24
            Assert.AreEqual(0.24f, ObsolescenceRules.UpgradePressure(0.6f, 1.0f, p), Eps);
            // 陳腐化なし（ペナルティ1.0）なら圧力ゼロ
            Assert.AreEqual(0f, ObsolescenceRules.UpgradePressure(1.0f, 1.0f, p), Eps);
        }

        /// <summary>一斉陳腐化＝飛躍が艦隊全体を旧式化。世代がばらつくほど打撃が薄まる。</summary>
        [Test]
        public void MassObsolescence_技術飛躍が艦隊全体を陳腐化させる()
        {
            var p = ObsolescenceParams.Default;
            // 飛躍0.5・広がり0 ＝ effectiveLeap=0.5 → ObsolescencePenalty(-0.5)=0.6
            Assert.AreEqual(0.6f, ObsolescenceRules.MassObsolescence(0.5f, 0f, p), Eps);
            // 同じ飛躍でも広がり1.0なら effectiveLeap=0.5*0.5=0.25 → 1-0.25*0.8=0.8（一部が生き残り緩む）
            Assert.AreEqual(0.8f, ObsolescenceRules.MassObsolescence(0.5f, 1.0f, p), Eps);
        }

        /// <summary>埋没投資のジレンマ＝艦隊価値×陳腐化ぶん。投じた費用が大きく陳腐化が深いほど重い。</summary>
        [Test]
        public void SunkInvestmentDilemma_投じた費用が無駄になる板挟み()
        {
            // 価値0.8・ペナルティ0.5 ＝ 0.8*(1-0.5)=0.4
            Assert.AreEqual(0.4f, ObsolescenceRules.SunkInvestmentDilemma(0.8f, 0.5f), Eps);
            // 陳腐化なしならジレンマなし
            Assert.AreEqual(0f, ObsolescenceRules.SunkInvestmentDilemma(0.8f, 1.0f), Eps);
        }

        /// <summary>陳腐化艦隊判定＝ペナルティが既定閾値0.5以下なら戦力にならない。</summary>
        [Test]
        public void IsObsoleteFleet_閾値以下は戦力にならない()
        {
            // 既定閾値0.5：0.4は陳腐化艦隊
            Assert.IsTrue(ObsolescenceRules.IsObsoleteFleet(0.4f));
            // ちょうど閾値0.5も陳腐化（以下）
            Assert.IsTrue(ObsolescenceRules.IsObsoleteFleet(0.5f));
            // 0.7は健在＝戦力になる
            Assert.IsFalse(ObsolescenceRules.IsObsoleteFleet(0.7f));
        }
    }
}
