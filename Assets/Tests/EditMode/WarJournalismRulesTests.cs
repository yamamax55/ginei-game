using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>戦争報道（戦況報道が銃後の世論に与える影響）の純ロジックを既定Paramsの具体値で固定するテスト。</summary>
    public class WarJournalismRulesTests
    {
        const float Eps = 0.0001f;

        /// <summary>戦争報道＝報道の自由度で戦況を色づける（検閲下は勝ち寄りに歪む）。</summary>
        [Test]
        public void WarReporting_報道の自由度で戦況を色づける()
        {
            // 自由1は戦況そのまま：大勝1→1、大敗-1→-1
            Assert.AreEqual(1f, WarJournalismRules.WarReporting(1f, 1f), Eps);
            Assert.AreEqual(-1f, WarJournalismRules.WarReporting(-1f, 1f), Eps);
            // 自由0は完全検閲＝戦況に関わらず勝ち寄り1：Lerp(1, 0.5, 0)=1
            Assert.AreEqual(1f, WarJournalismRules.WarReporting(0.5f, 0f), Eps);
            // 敗北を半自由で報道：Lerp(1, -0.6, 0.5)=0.2（実態より勝ち寄り）
            Assert.AreEqual(0.2f, WarJournalismRules.WarReporting(-0.6f, 0.5f), Eps);
            // 検閲が薄れるほど敗報が表に出る（単調に下がる）
            Assert.Greater(WarJournalismRules.WarReporting(-1f, 0.2f),
                           WarJournalismRules.WarReporting(-1f, 0.8f), "自由ほど敗報を伝える");
        }

        /// <summary>戦勝報道の盛り上げ＝実際の戦果×誇張（戦果0なら沸かない）。</summary>
        [Test]
        public void VictoryHype_戦果を誇張で盛り上げる()
        {
            // 戦果1・誇張0：1*1*0.7=0.7
            Assert.AreEqual(0.7f, WarJournalismRules.VictoryHype(1f, 0f), Eps);
            // 戦果1・誇張1：clamp01(1*2)=1 *0.7=0.7（上限に張り付く）
            Assert.AreEqual(0.7f, WarJournalismRules.VictoryHype(1f, 1f), Eps);
            // 戦果0.5・誇張0.4：clamp01(0.5*1.4=0.7)*0.7=0.49
            Assert.AreEqual(0.49f, WarJournalismRules.VictoryHype(0.5f, 0.4f), Eps);
            // 戦果が無ければ盛り上げも無い
            Assert.AreEqual(0f, WarJournalismRules.VictoryHype(0f, 1f), Eps);
        }

        /// <summary>報道と実態の乖離＝差の絶対値の半分（検閲で勝ちを盛り実態が負なら大きく開く）。</summary>
        [Test]
        public void CensorshipGap_報道と実態の差を測る()
        {
            // 勝ち報道1・実態-1：|2|*0.5=1（最大の乖離）
            Assert.AreEqual(1f, WarJournalismRules.CensorshipGap(1f, -1f), Eps);
            // 報道=実態：乖離なし
            Assert.AreEqual(0f, WarJournalismRules.CensorshipGap(0.5f, 0.5f), Eps);
            // 報道0.8・実態-0.2：|1.0|*0.5=0.5
            Assert.AreEqual(0.5f, WarJournalismRules.CensorshipGap(0.8f, -0.2f), Eps);
        }

        /// <summary>誇大報道の発覚反動＝盛り上げ×乖離（どちらか小なら反動も小＝積）。</summary>
        [Test]
        public void HypeBacklash_盛り上げと乖離が揃うと嘘がばれる()
        {
            // 0.6*0.5*0.6=0.18
            Assert.AreEqual(0.18f, WarJournalismRules.HypeBacklash(0.6f, 0.5f), Eps);
            // 乖離が無ければ反動も無い（嘘が無いのでばれない）
            Assert.AreEqual(0f, WarJournalismRules.HypeBacklash(1f, 0f), Eps);
            // 盛り上げが無ければ反動も無い
            Assert.AreEqual(0f, WarJournalismRules.HypeBacklash(0f, 1f), Eps);
        }

        /// <summary>敗北報道の士気打撃＝深刻さ×係数を報道の自由が増幅（検閲は和らげる）。</summary>
        [Test]
        public void DefeatMoraleImpact_敗北を報道の自由が増幅する()
        {
            // 深刻1・自由0：1*0.8*(1+0)=0.8
            Assert.AreEqual(0.8f, WarJournalismRules.DefeatMoraleImpact(1f, 0f), Eps);
            // 深刻1・自由1：1*0.8*1.5=1.2→clamp01=1
            Assert.AreEqual(1f, WarJournalismRules.DefeatMoraleImpact(1f, 1f), Eps);
            // 深刻0.5・自由0：0.5*0.8=0.4
            Assert.AreEqual(0.4f, WarJournalismRules.DefeatMoraleImpact(0.5f, 0f), Eps);
            // 検閲（自由低）は打撃を和らげる
            Assert.Less(WarJournalismRules.DefeatMoraleImpact(0.6f, 0.2f),
                        WarJournalismRules.DefeatMoraleImpact(0.6f, 0.9f), "自由ほど敗報が銃後に響く");
        }

        /// <summary>銃後の世論＝高揚−敗北打撃−反動（沸けば主戦・敗報と嘘で厭戦）。</summary>
        [Test]
        public void HomeFrontSentiment_高揚と打撃と反動の綱引き()
        {
            // 0.7-0.2-0.1=0.4（主戦寄り）
            Assert.AreEqual(0.4f, WarJournalismRules.HomeFrontSentiment(0.7f, 0.2f, 0.1f), Eps);
            // 高揚なし・大きな打撃と反動：0-0.8-0.2=-1.0→clamp=-1（厭戦）
            Assert.AreEqual(-1f, WarJournalismRules.HomeFrontSentiment(0f, 0.8f, 0.2f), Eps);
            // 高揚のみ：0.6-0-0=0.6
            Assert.AreEqual(0.6f, WarJournalismRules.HomeFrontSentiment(0.6f, 0f, 0f), Eps);
        }

        /// <summary>戦争支持＝世論−犠牲（主戦でも犠牲が嵩めば削られる）。</summary>
        [Test]
        public void WarSupport_犠牲が支持を削る()
        {
            // 0.4 - 0.2*0.5 = 0.3
            Assert.AreEqual(0.3f, WarJournalismRules.WarSupport(0.4f, 0.2f), Eps);
            // 0.5 - 1*0.5 = 0（犠牲で支持が拮抗まで落ちる）
            Assert.AreEqual(0f, WarJournalismRules.WarSupport(0.5f, 1f), Eps);
            // 犠牲が無ければ世論そのまま
            Assert.AreEqual(0.4f, WarJournalismRules.WarSupport(0.4f, 0f), Eps);
        }

        /// <summary>戦争支持の判定＝既定閾値0で支持が正なら支持されている。</summary>
        [Test]
        public void IsWarSupported_閾値で支持判定()
        {
            Assert.IsTrue(WarJournalismRules.IsWarSupported(0.3f));
            Assert.IsFalse(WarJournalismRules.IsWarSupported(-0.1f));
            Assert.IsFalse(WarJournalismRules.IsWarSupported(0f), "拮抗は支持とみなさない");
            // 明示閾値版
            Assert.IsTrue(WarJournalismRules.IsWarSupported(0.5f, 0.4f));
            Assert.IsFalse(WarJournalismRules.IsWarSupported(0.3f, 0.4f));
        }

        /// <summary>物語：検閲で大敗を勝報に偽装し誇張で沸かせるが、実態との乖離が露見して世論が裏返り、
        /// 犠牲も嵩んで戦争支持を失う＝大本営発表の破綻。</summary>
        [Test]
        public void 物語_誇大報道の破綻で戦争支持を失う()
        {
            // 大敗(-0.8)を強い検閲(自由0.1)で報道＝勝ち寄りに歪む
            float reported = WarJournalismRules.WarReporting(-0.8f, 0.1f);
            Assert.Greater(reported, 0f, "検閲で大敗が勝報に化ける");

            // 実態は乏しい戦果(0.1)を強い誇張(0.9)で大々的に盛る
            float hype = WarJournalismRules.VictoryHype(0.1f, 0.9f);
            Assert.Greater(hype, 0f, "誇張で乏しい戦果を盛り上げる");

            // 報じた勝報と実際の大敗の乖離は大きい
            float gap = WarJournalismRules.CensorshipGap(reported, -0.8f);
            Assert.Greater(gap, 0.5f, "報道と実態が大きく開く");

            // 盛り上げと乖離が揃って嘘がばれる
            float backlash = WarJournalismRules.HypeBacklash(hype, gap);
            Assert.Greater(backlash, 0f, "誇大報道が露見し反動が起きる");

            // 検閲下でも敗北の打撃はある（深刻0.8・自由0.1）
            float impact = WarJournalismRules.DefeatMoraleImpact(0.8f, 0.1f);

            // 銃後の世論は反動と打撃で沈む
            float sentiment = WarJournalismRules.HomeFrontSentiment(hype, impact, backlash);
            Assert.Less(sentiment, 0f, "反動と敗報で世論が厭戦へ裏返る");

            // 大きな犠牲(0.9)が支持をさらに削る
            float support = WarJournalismRules.WarSupport(sentiment, 0.9f);
            Assert.Less(support, sentiment, "犠牲が支持をさらに削る");
            Assert.IsFalse(WarJournalismRules.IsWarSupported(support), "戦争支持を失う");
        }
    }
}
