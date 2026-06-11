using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 「空気」支配（山本七平型・#1371 SHP-1）の純ロジック EditMode テスト。
    /// 空気の形成・異論の封殺・論理の圧倒・撤退の麻痺・沈黙の多数派・空気を破る者・空気の増幅・支配判定を担保。
    /// </summary>
    public class AtmosphereRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>空気の形成＝情緒的合意と階層圧力で空気が固まる（論理でなく雰囲気）。</summary>
        [Test]
        public void AtmosphereFormation_情緒的合意と階層圧力で空気が固まる()
        {
            // 既定 consensus0.6/hierarchy0.4・momentumRate0.1。
            // target=(1.0*0.6+0.5*0.4)/1.0=0.8。圧力0.3→MoveTowards(0.3,0.8,0.1*1)=0.4。
            float v = AtmosphereRules.AtmosphereFormation(0.3f, 1.0f, 0.5f, 1.0f);
            Assert.AreEqual(0.4f, v, Eps);

            // 合意・階層が高いほど空気は濃く、低いほど薄れる（向かう先が下がる）。
            float low = AtmosphereRules.AtmosphereFormation(0.3f, 0.1f, 0.1f, 5.0f);
            Assert.Less(low, 0.3f);
        }

        /// <summary>異論の封殺＝空気の圧力が個人の勇気を無力化する（誰も反対できない）。</summary>
        [Test]
        public void DissentSuppression_空気が勇気を無力化する()
        {
            // 既定 suppressionGain0.8。圧力0.9・勇気0.5→0.9*(1-0.5*0.8)=0.9*0.6=0.54。
            float v = AtmosphereRules.DissentSuppression(0.9f, 0.5f);
            Assert.AreEqual(0.54f, v, Eps);

            // 勇気があっても完全には抗えない（圧力1・勇気1でも残る＝1*(1-0.8)=0.2）。
            float brave = AtmosphereRules.DissentSuppression(1.0f, 1.0f);
            Assert.AreEqual(0.2f, brave, Eps);

            // 圧力0なら封殺なし。
            Assert.AreEqual(0f, AtmosphereRules.DissentSuppression(0f, 0f), Eps);
        }

        /// <summary>論理が空気に圧倒される＝不利な事実があっても空気で決まる（大和特攻）。</summary>
        [Test]
        public void LogicOverriddenByMood_空気が客観的事実を圧倒する()
        {
            // 既定 overrideGain0.8。圧力1.0・証拠0.3→1.0*0.8-0.3=0.5。
            float v = AtmosphereRules.LogicOverriddenByMood(1.0f, 0.3f);
            Assert.AreEqual(0.5f, v, Eps);

            // 客観的証拠が十分なら論理が勝つ（圧倒度0）。
            float logicWins = AtmosphereRules.LogicOverriddenByMood(0.5f, 0.9f);
            Assert.AreEqual(0f, logicWins, Eps);
        }

        /// <summary>撤退の麻痺＝状況が悪化しても空気が撤退の決断を縛る（インパール）。</summary>
        [Test]
        public void RetreatParalysis_空気が撤退を麻痺させる()
        {
            // 既定 overrideGain0.8。圧力0.8・悪化1.0→0.8*(1-0.2)+0.8*1.0*0.2=0.64+0.16=0.8。
            float worst = AtmosphereRules.RetreatParalysis(0.8f, 1.0f);
            Assert.AreEqual(0.8f, worst, Eps);

            // 同じ圧力でも、状況が悪化するほど麻痺は深まる（撤退すべきなのに止められない逆説）。
            float mild = AtmosphereRules.RetreatParalysis(0.8f, 0.0f);
            Assert.Greater(worst, mild);

            // 空気が無ければ麻痺なし。
            Assert.AreEqual(0f, AtmosphereRules.RetreatParalysis(0f, 1.0f), Eps);
        }

        /// <summary>沈黙の多数派＝皆が内心の疑問を抱えても空気を読んで黙る。</summary>
        [Test]
        public void SilentMajority_皆が疑問でも沈黙する()
        {
            // 同調0.8・疑問0.7→0.56。
            float v = AtmosphereRules.SilentMajority(0.8f, 0.7f);
            Assert.AreEqual(0.56f, v, Eps);

            // 同調圧力が無ければ疑問は声になる（沈黙ゼロ）。
            Assert.AreEqual(0f, AtmosphereRules.SilentMajority(0f, 1.0f), Eps);
        }

        /// <summary>空気を破る者＝よそ者・空気を読まない権威者が論理を取り戻す（誰かが言えば崩れる）。</summary>
        [Test]
        public void AtmosphereBreaker_誰かが言えば空気が崩れる()
        {
            // よそ者0.5・権威0.5→1-(0.5*0.5)=0.75（どちらか一方でも効くOR合成）。
            float v = AtmosphereRules.AtmosphereBreaker(0.5f, 0.5f);
            Assert.AreEqual(0.75f, v, Eps);

            // どちらかが完全なら必ず破れる。
            Assert.AreEqual(1f, AtmosphereRules.AtmosphereBreaker(1.0f, 0f), Eps);
            // 誰も空気を破れないなら崩れない。
            Assert.AreEqual(0f, AtmosphereRules.AtmosphereBreaker(0f, 0f), Eps);
        }

        /// <summary>空気の増幅＝一度できた空気は時間で強化され引き返せなくなる（自己強化）。</summary>
        [Test]
        public void KutekiMomentum_空気が時間で増幅する()
        {
            // 既定 momentumRate0.1。圧力0.5→0.5+0.5*0.5*0.1*1=0.5+0.025=0.525。
            float v = AtmosphereRules.KutekiMomentum(0.5f, 1.0f);
            Assert.AreEqual(0.525f, v, Eps);

            // 自己強化＝増幅後は元より高い（引き返せなくなる）。
            Assert.Greater(v, 0.5f);

            // 空気が無ければ増幅もない（0は0のまま）／既に最大なら頭打ち。
            Assert.AreEqual(0f, AtmosphereRules.KutekiMomentum(0f, 10f), Eps);
            Assert.AreEqual(1f, AtmosphereRules.KutekiMomentum(1f, 10f), Eps);
        }

        /// <summary>空気支配の判定＝圧力が高く異論も封じられた組織は合理的判断を失う。</summary>
        [Test]
        public void IsRuledByAtmosphere_空気に支配され合理を失う()
        {
            // 既定 ruleThreshold0.6。圧力0.9*封殺0.8=0.72≥0.6→支配。
            Assert.IsTrue(AtmosphereRules.IsRuledByAtmosphere(0.9f, 0.8f));
            // 圧力0.5*封殺0.5=0.25<0.6→まだ合理が残る。
            Assert.IsFalse(AtmosphereRules.IsRuledByAtmosphere(0.5f, 0.5f));
            // 明示しきい値も機能する。
            Assert.IsTrue(AtmosphereRules.IsRuledByAtmosphere(0.5f, 0.5f, 0.25f));
        }
    }
}
