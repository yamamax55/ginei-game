using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 複合共和制と二層主権（FED-3 #1481・フェデラリスト第51篇）の純ロジック検証。
    /// 委譲権限・保留権限・垂直抑制の強さ・二重の安全保障・専制リスク低下・越権抵抗・
    /// 主権の争い・連邦均衡判定を既定 <see cref="CompoundRepublicParams.Default"/> の具体値で固定する。
    /// </summary>
    public class CompoundRepublicRulesTests
    {
        private const float Eps = 0.0005f;

        /// <summary>委譲権限＝中央権威×列挙範囲。範囲が狭ければ権威があっても委譲は痩せる。</summary>
        [Test]
        public void DelegatedPower_列挙範囲に限定される()
        {
            // 権威0.8×範囲0.5＝0.4
            Assert.That(CompoundRepublicRules.DelegatedPower(0.8f, 0.5f), Is.EqualTo(0.4f).Within(Eps));
            // 列挙範囲0＝委譲なし
            Assert.That(CompoundRepublicRules.DelegatedPower(1f, 0f), Is.EqualTo(0f).Within(Eps));
            // 範囲全開・権威全開＝完全委譲
            Assert.That(CompoundRepublicRules.DelegatedPower(1f, 1f), Is.EqualTo(1f).Within(Eps));
        }

        /// <summary>保留権限＝1−委譲（修正第10条の残余）。</summary>
        [Test]
        public void ReservedPower_委譲されなかった残余()
        {
            Assert.That(CompoundRepublicRules.ReservedPower(0.4f), Is.EqualTo(0.6f).Within(Eps));
            Assert.That(CompoundRepublicRules.ReservedPower(0f), Is.EqualTo(1f).Within(Eps)); // 全部州に残る
            Assert.That(CompoundRepublicRules.ReservedPower(1f), Is.EqualTo(0f).Within(Eps)); // 全部中央へ
        }

        /// <summary>垂直抑制＝両者がともに実質を持つほど強く、片方が空なら消える。</summary>
        [Test]
        public void VerticalCheckStrength_半々で最大_一極で消失()
        {
            // 委譲0.5/保留0.5＝4×0.25=1.0（既定鋭さ1.0）
            Assert.That(CompoundRepublicRules.VerticalCheckStrength(0.5f, 0.5f), Is.EqualTo(1f).Within(Eps));
            // 完全集中（委譲1/保留0）＝抑制0
            Assert.That(CompoundRepublicRules.VerticalCheckStrength(1f, 0f), Is.EqualTo(0f).Within(Eps));
            // 完全分権（委譲0/保留1）＝抑制0
            Assert.That(CompoundRepublicRules.VerticalCheckStrength(0f, 1f), Is.EqualTo(0f).Within(Eps));
            // 委譲0.8/保留0.2＝4×0.16=0.64
            Assert.That(CompoundRepublicRules.VerticalCheckStrength(0.8f, 0.2f), Is.EqualTo(0.64f).Within(Eps));
        }

        /// <summary>二重の安全保障＝垂直×水平の相乗平均。片方が空なら脆い。</summary>
        [Test]
        public void DoubleSecurity_垂直と水平の相乗平均()
        {
            // 垂直0.64×水平0.81=0.5184→√=0.72
            Assert.That(CompoundRepublicRules.DoubleSecurity(0.64f, 0.81f), Is.EqualTo(0.72f).Within(Eps));
            // 水平が0＝二重の守りは成立しない
            Assert.That(CompoundRepublicRules.DoubleSecurity(1f, 0f), Is.EqualTo(0f).Within(Eps));
            // 両方満点＝1.0
            Assert.That(CompoundRepublicRules.DoubleSecurity(1f, 1f), Is.EqualTo(1f).Within(Eps));
        }

        /// <summary>専制リスク低下＝二重の安全保障×上限0.8。</summary>
        [Test]
        public void TyrannyRiskReduction_上限で割り引く()
        {
            // 0.72×0.8=0.576
            Assert.That(CompoundRepublicRules.TyrannyRiskReduction(0.72f), Is.EqualTo(0.576f).Within(Eps));
            // 安全保障満点でも上限0.8まで
            Assert.That(CompoundRepublicRules.TyrannyRiskReduction(1f), Is.EqualTo(0.8f).Within(Eps));
            Assert.That(CompoundRepublicRules.TyrannyRiskReduction(0f), Is.EqualTo(0f).Within(Eps));
        }

        /// <summary>越権抵抗＝保留権限×越権の強さ。州に権限が残るほど砦になる。</summary>
        [Test]
        public void EncroachmentResistance_州が砦になる()
        {
            // 保留0.6×越権0.5=0.3
            Assert.That(CompoundRepublicRules.EncroachmentResistance(0.6f, 0.5f), Is.EqualTo(0.3f).Within(Eps));
            // 州に権限が残らない＝無抵抗
            Assert.That(CompoundRepublicRules.EncroachmentResistance(0f, 1f), Is.EqualTo(0f).Within(Eps));
        }

        /// <summary>主権の争い＝重複×両者の実権。明確な一極なら争う相手が無く0。</summary>
        [Test]
        public void SovereigntyContest_重複領域で緊張()
        {
            // 委譲0.5/保留0.5（両者実権1.0）×重複0.6=0.6
            Assert.That(CompoundRepublicRules.SovereigntyContest(0.5f, 0.5f, 0.6f), Is.EqualTo(0.6f).Within(Eps));
            // 一極（委譲1/保留0）＝争いなし
            Assert.That(CompoundRepublicRules.SovereigntyContest(1f, 0f, 1f), Is.EqualTo(0f).Within(Eps));
            // 重複0＝境界紛争なし
            Assert.That(CompoundRepublicRules.SovereigntyContest(0.5f, 0.5f, 0f), Is.EqualTo(0f).Within(Eps));
        }

        /// <summary>連邦均衡判定＝垂直抑制が既定しきい値0.5以上で均衡＝専制に強い。</summary>
        [Test]
        public void IsBalancedFederalism_しきい値判定()
        {
            Assert.That(CompoundRepublicRules.IsBalancedFederalism(0.64f), Is.True);  // 0.5以上
            Assert.That(CompoundRepublicRules.IsBalancedFederalism(0.5f), Is.True);   // ちょうど
            Assert.That(CompoundRepublicRules.IsBalancedFederalism(0.3f), Is.False);  // 未満
            // 一極集中（垂直抑制0）は均衡しない
            Assert.That(CompoundRepublicRules.IsBalancedFederalism(0f), Is.False);
        }

        /// <summary>入力は全てクランプされる（範囲外でも破綻しない）。</summary>
        [Test]
        public void 全API_入力クランプ()
        {
            Assert.That(CompoundRepublicRules.DelegatedPower(5f, 5f), Is.EqualTo(1f).Within(Eps));
            Assert.That(CompoundRepublicRules.ReservedPower(-3f), Is.EqualTo(1f).Within(Eps));
            Assert.That(CompoundRepublicRules.VerticalCheckStrength(-1f, 2f), Is.EqualTo(0f).Within(Eps));
            Assert.That(CompoundRepublicRules.DoubleSecurity(2f, 2f), Is.EqualTo(1f).Within(Eps));
            Assert.That(CompoundRepublicRules.EncroachmentResistance(2f, -1f), Is.EqualTo(0f).Within(Eps));
        }
    }
}
