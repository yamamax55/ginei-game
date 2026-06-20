using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>民衆蜂起（大衆の武装蜂起と政権の対応）の純ロジックを EditMode で担保する。</summary>
    public class UprisingRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>発火：不満を土台に引き金事件が点火し、不満が無ければ火が点かない。</summary>
        [Test]
        public void UprisingTrigger_IgnitesFromGrievanceAndSpark()
        {
            // grievance 0.6, spark 0.8*0.5=0.4 → 0.6*0.4 = 0.24
            float trig = UprisingRules.UprisingTrigger(0.6f, 0.8f);
            Assert.AreEqual(0.24f, trig, Eps);

            // 不満0＝乾いた薪が無く点火しない
            float noGrievance = UprisingRules.UprisingTrigger(0f, 1f);
            Assert.AreEqual(0f, noGrievance, Eps);

            // 引き金0＝不満は燻ったまま
            float noSpark = UprisingRules.UprisingTrigger(1f, 0f);
            Assert.AreEqual(0f, noSpark, Eps);
        }

        /// <summary>参加規模：発火を窮状が上乗せで膨らませ、発火0なら参加0。</summary>
        [Test]
        public void PopularParticipation_ScalesWithDesperation()
        {
            // boost = 1+0.5*0.4 = 1.2 → 0.5*1.2 = 0.6
            float part = UprisingRules.PopularParticipation(0.5f, 0.5f);
            Assert.AreEqual(0.6f, part, Eps);

            // 発火0＝街頭へ出る火種が無い
            float noTrig = UprisingRules.PopularParticipation(0f, 1f);
            Assert.AreEqual(0f, noTrig, Eps);

            // 窮状0＝上乗せなし（発火そのまま）
            float noDesp = UprisingRules.PopularParticipation(0.5f, 0f);
            Assert.AreEqual(0.5f, noDesp, Eps);
        }

        /// <summary>治安部隊の離反：参加×兵の共感×非正統を増幅倍率で膨らませる。</summary>
        [Test]
        public void SecurityForceDefection_RisesWithSympathyAndIllegitimacy()
        {
            // illegit = 1-0.4 = 0.6 ; core = 0.5*0.6*0.6 = 0.18 ; *2.0 = 0.36
            float def = UprisingRules.SecurityForceDefection(0.5f, 0.6f, 0.4f);
            Assert.AreEqual(0.36f, def, Eps);

            // 政権が完全正統＝同胞射撃をためらわず離反0
            float legit = UprisingRules.SecurityForceDefection(1f, 1f, 1f);
            Assert.AreEqual(0f, legit, Eps);

            // 兵の共感0＝離反0
            float noSympathy = UprisingRules.SecurityForceDefection(1f, 0f, 1f);
            Assert.AreEqual(0f, noSympathy, Eps);
        }

        /// <summary>鎮圧能力：忠誠部隊×発砲意志。意志が無いと地力ぶんに目減りする。</summary>
        [Test]
        public void RegimeSuppressionCapacity_NeedsWillToKill()
        {
            // willFactor = 1-0.6+0.6*0.5 = 0.7 → 0.8*0.7 = 0.56
            float cap = UprisingRules.RegimeSuppressionCapacity(0.8f, 0.5f);
            Assert.AreEqual(0.56f, cap, Eps);

            // 発砲意志0＝地力(1-0.6)=0.4 ぶんのみ → 0.8*0.4 = 0.32
            float noWill = UprisingRules.RegimeSuppressionCapacity(0.8f, 0f);
            Assert.AreEqual(0.32f, noWill, Eps);

            // 満額＝部隊×意志ともに1
            float full = UprisingRules.RegimeSuppressionCapacity(1f, 1f);
            Assert.AreEqual(1f, full, Eps);
        }

        /// <summary>蜂起の波及：参加を通信網が地方へ伝播させ、参加0なら波及0。</summary>
        [Test]
        public void UprisingSpread_PropagatesViaCommunications()
        {
            // reach = 1+0.5*(2-1) = 1.5 → 0.5*1.5 = 0.75
            float spread = UprisingRules.UprisingSpread(0.5f, 0.5f);
            Assert.AreEqual(0.75f, spread, Eps);

            // 通信網0＝伝播せず参加のまま
            float noComms = UprisingRules.UprisingSpread(0.5f, 0f);
            Assert.AreEqual(0.5f, noComms, Eps);

            // 参加0＝広がる火が無い
            float noPart = UprisingRules.UprisingSpread(0f, 1f);
            Assert.AreEqual(0f, noPart, Eps);
        }

        /// <summary>政権の結束：エリート結束×(1-離反)。離反が結束の地盤を削る。</summary>
        [Test]
        public void RegimeCohesion_ErodedByDefection()
        {
            // baseUnity = 1-0.7+0.7*0.8 = 0.86 ; held = 1-0.3 = 0.7 → 0.86*0.7 = 0.602
            float coh = UprisingRules.RegimeCohesion(0.8f, 0.3f);
            Assert.AreEqual(0.602f, coh, Eps);

            // 離反1＝結束が崩れる
            float collapsed = UprisingRules.RegimeCohesion(1f, 1f);
            Assert.AreEqual(0f, collapsed, Eps);
        }

        /// <summary>革命の成否：波及×離反の革命圧から鎮圧を引く（-1..1）。鎮圧優位なら負。</summary>
        [Test]
        public void RevolutionarySuccess_RevoltMinusSuppression()
        {
            // revolt = 0.75*0.36 = 0.27 ; suppress = 0.56*0.8 = 0.448 → 0.27-0.448 = -0.178
            float s = UprisingRules.RevolutionarySuccess(0.75f, 0.36f, 0.56f);
            Assert.AreEqual(-0.178f, s, Eps);

            // 鎮圧0で波及・離反満額＝革命優位（正）
            float win = UprisingRules.RevolutionarySuccess(1f, 1f, 0f);
            Assert.AreEqual(1f, win, Eps);

            // 波及0＝革命圧が立たず鎮圧ぶん負へ
            float crushed = UprisingRules.RevolutionarySuccess(0f, 1f, 1f);
            Assert.AreEqual(-0.8f, crushed, Eps);
        }

        /// <summary>打倒判定：成否が閾値を超えれば政権打倒。既定委譲は閾値0.2。</summary>
        [Test]
        public void IsRegimeOverthrown_ThresholdGate()
        {
            Assert.IsTrue(UprisingRules.IsRegimeOverthrown(0.5f, 0.2f));
            Assert.IsFalse(UprisingRules.IsRegimeOverthrown(0.1f, 0.2f));
            // 既定委譲版（閾値0.2）
            Assert.IsTrue(UprisingRules.IsRegimeOverthrown(0.3f));
            Assert.IsFalse(UprisingRules.IsRegimeOverthrown(-0.178f));
        }

        /// <summary>
        /// 物語テスト：圧政下で物価高騰の引き金が街頭を燃やし、徴集兵が同胞へ銃を向けず離反、
        /// 通信網で蜂起が全土へ波及する一方、発砲を渋る政権の鎮圧は空回り＝革命が成り政権が打倒される。
        /// </summary>
        [Test]
        public void Narrative_FoodRiotTopplesIllegitimateRegime()
        {
            var p = UprisingParams.Default;

            // 高い不満（0.9）に物価高騰の引き金（0.9）＝広場が点火
            float trig = UprisingRules.UprisingTrigger(0.9f, 0.9f, p);
            // 困窮した大衆（0.9）が大挙して参加
            float part = UprisingRules.PopularParticipation(trig, 0.9f, p);
            // 正統性を失った政権（0.1）に共感する徴集兵（0.8）が離反
            float def = UprisingRules.SecurityForceDefection(part, 0.8f, 0.1f, p);
            // 通信網（0.9）で全土へ波及
            float spread = UprisingRules.UprisingSpread(part, 0.9f, p);
            // 忠誠部隊は薄く（0.3）発砲意志も低い（0.2）＝鎮圧は弱い
            float cap = UprisingRules.RegimeSuppressionCapacity(0.3f, 0.2f, p);

            float success = UprisingRules.RevolutionarySuccess(spread, def, cap, p);
            Assert.Greater(success, 0f, "革命圧が鎮圧を上回るはず");
            Assert.IsTrue(UprisingRules.IsRegimeOverthrown(success), "弱い非正統政権は打倒される");

            // 対照：エリートが結束し（0.9）兵が離反しなければ（0.0）政権は踏みとどまる
            float cohesion = UprisingRules.RegimeCohesion(0.9f, 0f, p);
            Assert.Greater(cohesion, 0.9f, "離反なし＋高い結束で政権は固い");
        }
    }
}
