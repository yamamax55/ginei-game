using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 貴族特権の純ロジック（門閥の世襲特権と社会的歪み）のテスト。世襲位階×寵×領地の特権蓄積、平民への負担転嫁、
    /// 無能の温存、腐敗、社会的不満、軍の害、改革圧力、維持可否を既定Paramsの具体値で担保する。
    /// </summary>
    public class NoblePrivilegeRulesTests
    {
        const float Eps = 0.0001f;

        /// <summary>特権蓄積＝位階×寵×領地の加重和（既定0.4/0.3/0.3）。</summary>
        [Test]
        public void PrivilegeAccumulation_位階と寵と領地の加重和()
        {
            // 0.5*0.4 + 0.5*0.3 + 0.5*0.3 = 0.2 + 0.15 + 0.15 = 0.5
            Assert.AreEqual(0.5f, NoblePrivilegeRules.PrivilegeAccumulation(0.5f, 0.5f, 0.5f), Eps);
            // 全て最大＝重み合計1で1.0。
            Assert.AreEqual(1f, NoblePrivilegeRules.PrivilegeAccumulation(1f, 1f, 1f), Eps);
            // 全て無ければ特権なし。
            Assert.AreEqual(0f, NoblePrivilegeRules.PrivilegeAccumulation(0f, 0f, 0f), Eps);
        }

        /// <summary>平民への負担転嫁＝特権×免税で増幅（免税が進むほど平民が肩代わり）。</summary>
        [Test]
        public void CommonerBurden_特権と免税で平民へ転嫁()
        {
            // 0.5 * (1 + 0.5*0.6) = 0.5 * 1.3 = 0.65
            Assert.AreEqual(0.65f, NoblePrivilegeRules.CommonerBurden(0.5f, 0.5f), Eps);
            // 免税が無ければ特権そのまま。
            Assert.AreEqual(0.5f, NoblePrivilegeRules.CommonerBurden(0.5f, 0f), Eps);
            // 特権が無ければ負担転嫁0。
            Assert.AreEqual(0f, NoblePrivilegeRules.CommonerBurden(0f, 1f), Eps);
        }

        /// <summary>無能の温存＝特権×無能さで実力を問わせない（特権が無ければ淘汰）。</summary>
        [Test]
        public void IncompetenceShelter_特権で無能を温存()
        {
            // 0.6 * 0.5 * (1 + 0.7) = 0.3 * 1.7 = 0.51
            Assert.AreEqual(0.51f, NoblePrivilegeRules.IncompetenceShelter(0.6f, 0.5f), Eps);
            // 特権が無ければ無能は温存されない。
            Assert.AreEqual(0f, NoblePrivilegeRules.IncompetenceShelter(0f, 1f), Eps);
            // 無能さが無ければ温存対象なし。
            Assert.AreEqual(0f, NoblePrivilegeRules.IncompetenceShelter(1f, 0f), Eps);
        }

        /// <summary>腐敗＝特権/(1+説明責任)（説明責任が腐敗を抑える）。</summary>
        [Test]
        public void Corruption_説明責任で腐敗を抑える()
        {
            // 0.8 / (1 + 1) = 0.4
            Assert.AreEqual(0.4f, NoblePrivilegeRules.Corruption(0.8f, 1f), Eps);
            // 説明責任なしなら特権がそのまま腐敗に。
            Assert.AreEqual(0.6f, NoblePrivilegeRules.Corruption(0.6f, 0f), Eps);
            // 特権が無ければ腐敗なし。
            Assert.AreEqual(0f, NoblePrivilegeRules.Corruption(0f, 0f), Eps);
        }

        /// <summary>社会的不満＝負担×腐敗で蓄積（両方揃うと跳ね上がる）。</summary>
        [Test]
        public void SocialResentment_負担と腐敗で不満蓄積()
        {
            // (0.6+0.4)*0.5 + 0.6*0.4*0.5 = 0.5 + 0.12 = 0.62
            Assert.AreEqual(0.62f, NoblePrivilegeRules.SocialResentment(0.6f, 0.4f), Eps);
            // 両方最大で1.0（(2)*0.5 + 1*0.5 = 1.0、clamp）。
            Assert.AreEqual(1f, NoblePrivilegeRules.SocialResentment(1f, 1f), Eps);
            // 両方無ければ不満0。
            Assert.AreEqual(0f, NoblePrivilegeRules.SocialResentment(0f, 0f), Eps);
        }

        /// <summary>軍の害＝無能温存が指揮を損なう（貴族将校の害）。</summary>
        [Test]
        public void MilitaryHarm_無能温存が軍を損なう()
        {
            // 0.5 * 0.8 = 0.4
            Assert.AreEqual(0.4f, NoblePrivilegeRules.MilitaryHarm(0.5f), Eps);
            Assert.AreEqual(0.8f, NoblePrivilegeRules.MilitaryHarm(1f), Eps);
            Assert.AreEqual(0f, NoblePrivilegeRules.MilitaryHarm(0f), Eps);
        }

        /// <summary>改革圧力＝不満×改革派の力（両者が揃って初めて廃止圧力に）。</summary>
        [Test]
        public void ReformPressure_不満と改革派で廃止圧力()
        {
            // 0.6 * 0.5 * (1 + 0.7) = 0.3 * 1.7 = 0.51
            Assert.AreEqual(0.51f, NoblePrivilegeRules.ReformPressure(0.6f, 0.5f), Eps);
            // 改革派が居なければ不満があっても圧力にならない。
            Assert.AreEqual(0f, NoblePrivilegeRules.ReformPressure(1f, 0f), Eps);
            // 不満が無ければ圧力なし。
            Assert.AreEqual(0f, NoblePrivilegeRules.ReformPressure(0f, 1f), Eps);
        }

        /// <summary>維持可否＝正味圧力（圧力-後ろ盾）が閾値未満なら維持できる。</summary>
        [Test]
        public void IsPrivilegeSustainable_後ろ盾が圧力を相殺()
        {
            // 圧力0.5・後ろ盾0＝正味0.5 < 0.6 で維持可。
            Assert.IsTrue(NoblePrivilegeRules.IsPrivilegeSustainable(0.5f, 0f));
            // 圧力0.8・後ろ盾0.1＝正味0.7 ≥ 0.6 で維持不能（廃止へ）。
            Assert.IsFalse(NoblePrivilegeRules.IsPrivilegeSustainable(0.8f, 0.1f));
            // 強い圧力でも厚い後ろ盾があれば相殺して維持できる。
            Assert.IsTrue(NoblePrivilegeRules.IsPrivilegeSustainable(0.9f, 0.8f)); // 正味0.1
        }

        /// <summary>
        /// 物語＝門閥貴族の腐敗から改革の嵐へ。世襲位階・宮廷の寵・広大な領地で特権を蓄え、免税で平民へ重税を
        /// 転嫁し、説明責任なき特権で腐敗し無能な貴族将校が軍を損なう。平民の不満が改革派の力で廃止圧力に転じ、
        /// 主君の後ろ盾だけでは支えきれず特権は維持できなくなる。
        /// </summary>
        [Test]
        public void Story_門閥の腐敗が改革の嵐を招く()
        {
            var p = NoblePrivilegeParams.Default;

            // 高位の門閥貴族＝厚い特権を蓄積。
            float priv = NoblePrivilegeRules.PrivilegeAccumulation(0.9f, 0.8f, 0.9f, p);
            Assert.Greater(priv, 0.7f);

            // 免税で平民へ重い負担が転嫁される。
            float burden = NoblePrivilegeRules.CommonerBurden(priv, 0.9f, p);
            Assert.Greater(burden, priv); // 免税が転嫁を増幅

            // 特権が無能を温存し、説明責任なき腐敗が進む。
            float shelter = NoblePrivilegeRules.IncompetenceShelter(priv, 0.8f, p);
            float corrupt = NoblePrivilegeRules.Corruption(priv, 0.1f, p);
            Assert.Greater(shelter, 0.5f);
            Assert.Greater(corrupt, 0.5f);

            // 無能な貴族将校が軍を損なう。
            float harm = NoblePrivilegeRules.MilitaryHarm(shelter, p);
            Assert.Greater(harm, 0.4f);

            // 重い負担と腐敗が社会的不満を高め、改革派の力で廃止圧力に転じる。
            float resent = NoblePrivilegeRules.SocialResentment(burden, corrupt, p);
            float pressure = NoblePrivilegeRules.ReformPressure(resent, 0.8f, p);
            Assert.Greater(resent, 0.6f);
            Assert.Greater(pressure, 0.5f);

            // 弱い後ろ盾では特権を維持できない（改革の嵐＝廃止へ）。
            Assert.IsFalse(NoblePrivilegeRules.IsPrivilegeSustainable(pressure, 0.1f, p.sustainThreshold));
            // 一方、後ろ盾が厚ければ同じ圧力でも当面は維持できる。
            Assert.IsTrue(NoblePrivilegeRules.IsPrivilegeSustainable(pressure, 0.95f, p.sustainThreshold));
        }
    }
}
