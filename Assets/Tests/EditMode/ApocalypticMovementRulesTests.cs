using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>終末思想運動（黙示録的カルト）の純ロジックを既定Paramsの具体値で固定するテスト。</summary>
    public class ApocalypticMovementRulesTests
    {
        const float Eps = 0.0001f;
        const float PowEps = 0.001f; // Pow/Sqrt 箇所の緩めの許容

        /// <summary>終末予言の訴求＝危機×鮮烈を訴求鋭さ1.0でPow（既定は素の積）。</summary>
        [Test]
        public void PropheticAppeal_危機と鮮烈さの積で予言が人を引く()
        {
            // 0.8*0.5 = 0.4, Pow(0.4, 1/1) = 0.4
            Assert.AreEqual(0.4f, ApocalypticMovementRules.PropheticAppeal(0.8f, 0.5f), PowEps);
            // 鮮烈さ0なら訴求なし
            Assert.AreEqual(0f, ApocalypticMovementRules.PropheticAppeal(1f, 0f), Eps);
            // どちらも最大なら最大
            Assert.AreEqual(1f, ApocalypticMovementRules.PropheticAppeal(1f, 1f), PowEps);
            // 危機が深いほど訴求は強い（単調）
            Assert.Greater(ApocalypticMovementRules.PropheticAppeal(0.9f, 0.6f),
                           ApocalypticMovementRules.PropheticAppeal(0.3f, 0.6f), "危機が深いほど予言が効く");
        }

        /// <summary>信徒の急増＝訴求に社会的絶望が動員力として乗る（危機の時代に増える）。</summary>
        [Test]
        public void ConvertSurge_絶望が信徒急増を後押しする()
        {
            // 0.5 * (1 + 1*0.7) = 0.85
            Assert.AreEqual(0.85f, ApocalypticMovementRules.ConvertSurge(0.5f, 1f), Eps);
            // 絶望0なら訴求そのまま
            Assert.AreEqual(0.6f, ApocalypticMovementRules.ConvertSurge(0.6f, 0f), Eps);
            // 絶望が深いほど信徒が増える（単調）
            Assert.Greater(ApocalypticMovementRules.ConvertSurge(0.5f, 0.9f),
                           ApocalypticMovementRules.ConvertSurge(0.5f, 0.2f), "絶望が深いほど急増");
            // 上限クランプ
            Assert.AreEqual(1f, ApocalypticMovementRules.ConvertSurge(1f, 1f), Eps);
        }

        /// <summary>終末への備え（過激行動）＝信徒×教義の切迫×0.8。</summary>
        [Test]
        public void EndTimePreparation_信徒と切迫で過激な備えに走る()
        {
            // 0.8*0.5*0.8 = 0.32
            Assert.AreEqual(0.32f, ApocalypticMovementRules.EndTimePreparation(0.8f, 0.5f), Eps);
            // 切迫0なら過激化しない
            Assert.AreEqual(0f, ApocalypticMovementRules.EndTimePreparation(1f, 0f), Eps);
            // 切迫が増すほど過激化（単調）
            Assert.Greater(ApocalypticMovementRules.EndTimePreparation(0.7f, 0.9f),
                           ApocalypticMovementRules.EndTimePreparation(0.7f, 0.3f), "切迫が増すほど過激");
        }

        /// <summary>千年王国の約束＝救済の幻×現世の苦×0.9（苦しいほど効く）。</summary>
        [Test]
        public void MillenarianPromise_現世が苦しいほど千年王国に縋る()
        {
            // 0.9*0.5*0.9 = 0.405
            Assert.AreEqual(0.405f, ApocalypticMovementRules.MillenarianPromise(0.9f, 0.5f), Eps);
            // 両方最大: 0.9
            Assert.AreEqual(0.9f, ApocalypticMovementRules.MillenarianPromise(1f, 1f), Eps);
            // 苦が無ければ約束は効かない
            Assert.AreEqual(0f, ApocalypticMovementRules.MillenarianPromise(1f, 0f), Eps);
            // 苦が深いほど効く（単調）
            Assert.Greater(ApocalypticMovementRules.MillenarianPromise(0.8f, 0.9f),
                           ApocalypticMovementRules.MillenarianPromise(0.8f, 0.2f), "苦しいほど約束が効く");
        }

        /// <summary>予言の不成就＝到来日が予言日を過ぎたぶん（過ぎるほど1へ漸近）。</summary>
        [Test]
        public void ProphecyFailure_予言した日を過ぎると外れが深まる()
        {
            // overshoot=3, 3/(3+1) = 0.75
            Assert.AreEqual(0.75f, ApocalypticMovementRules.ProphecyFailure(10f, 13f), Eps);
            // まだ来ていない（到来≤予言）は外れ0
            Assert.AreEqual(0f, ApocalypticMovementRules.ProphecyFailure(10f, 10f), Eps);
            Assert.AreEqual(0f, ApocalypticMovementRules.ProphecyFailure(10f, 8f), Eps);
            // 過ぎるほど外れが深まる（単調・1未満）
            Assert.Greater(ApocalypticMovementRules.ProphecyFailure(10f, 20f),
                           ApocalypticMovementRules.ProphecyFailure(10f, 13f), "過ぎるほど外れが深い");
            Assert.Less(ApocalypticMovementRules.ProphecyFailure(10f, 1000f), 1f, "外れは1に漸近し超えない");
        }

        /// <summary>認知的不協和＝予言外れ×それまでの傾倒×0.8。</summary>
        [Test]
        public void CognitiveDissonance_外れと傾倒で矛盾が痛む()
        {
            // 0.75*0.8*0.8 = 0.48
            Assert.AreEqual(0.48f, ApocalypticMovementRules.CognitiveDissonance(0.75f, 0.8f), Eps);
            // 傾倒が浅ければ外れても痛まない
            Assert.AreEqual(0f, ApocalypticMovementRules.CognitiveDissonance(1f, 0f), Eps);
            // 傾倒が深いほど不協和が大きい（単調）
            Assert.Greater(ApocalypticMovementRules.CognitiveDissonance(0.8f, 0.9f),
                           ApocalypticMovementRules.CognitiveDissonance(0.8f, 0.3f), "傾倒が深いほど痛む");
        }

        /// <summary>急進化＝不協和×指導者カリスマ×0.7（外れても信仰を深める逆説）。</summary>
        [Test]
        public void Radicalization_カリスマが不協和を信仰深化へ転じる()
        {
            // 0.48*1*0.7 = 0.336
            Assert.AreEqual(0.336f, ApocalypticMovementRules.Radicalization(0.48f, 1f), Eps);
            // カリスマ0なら急進化しない（不協和は解消へ向かう）
            Assert.AreEqual(0f, ApocalypticMovementRules.Radicalization(0.5f, 0f), Eps);
            // カリスマが強いほど急進化（単調）
            Assert.Greater(ApocalypticMovementRules.Radicalization(0.5f, 0.9f),
                           ApocalypticMovementRules.Radicalization(0.5f, 0.3f), "カリスマが強いほど急進化");
        }

        /// <summary>集団自決の危険＝急進化×社会からの孤立×0.85。</summary>
        [Test]
        public void CollectiveSuicideRisk_急進化と孤立が自決の危険を高める()
        {
            // 0.336*1*0.85 = 0.2856
            Assert.AreEqual(0.2856f, ApocalypticMovementRules.CollectiveSuicideRisk(0.336f, 1f), Eps);
            // 社会とつながっていれば危険は低い
            Assert.AreEqual(0f, ApocalypticMovementRules.CollectiveSuicideRisk(0.8f, 0f), Eps);
            // 孤立が深いほど危険（単調）
            Assert.Greater(ApocalypticMovementRules.CollectiveSuicideRisk(0.8f, 0.9f),
                           ApocalypticMovementRules.CollectiveSuicideRisk(0.8f, 0.2f), "孤立が深いほど自決の危険");
        }

        /// <summary>
        /// 物語テスト：危機が予言を鮮烈にし絶望が信徒を急増させ過激な備えに走る。予言は外れるが、
        /// 深く傾倒した信徒の不協和をカリスマある指導者が信仰深化へ転じ、孤立の果てに集団自決の危険が高まる。
        /// </summary>
        [Test]
        public void 物語_予言は外れカリスマと孤立が運動を破滅へ導く()
        {
            // --- 危機の時代：終末予言が鮮烈に響く ---
            float appeal = ApocalypticMovementRules.PropheticAppeal(0.9f, 0.8f);
            Assert.Greater(appeal, 0.5f, "深い危機と鮮烈な予言で訴求が高い");

            // --- 絶望が信徒を急増させる ---
            float surge = ApocalypticMovementRules.ConvertSurge(appeal, 0.9f);
            Assert.Greater(surge, appeal, "社会的絶望が信徒の急増を後押しする");

            // --- 千年王国の約束は苦しいほど効く ---
            float promiseHard = ApocalypticMovementRules.MillenarianPromise(0.9f, 0.9f);
            float promiseEasy = ApocalypticMovementRules.MillenarianPromise(0.9f, 0.2f);
            Assert.Greater(promiseHard, promiseEasy, "現世が苦しいほど千年王国に縋る");

            // --- 終末への備えとして過激行動に走る ---
            float prep = ApocalypticMovementRules.EndTimePreparation(surge, 0.9f);
            Assert.Greater(prep, 0f, "切迫した信徒が過激な備えに走る");

            // --- だが予言の日は外れる ---
            float failure = ApocalypticMovementRules.ProphecyFailure(100f, 130f);
            Assert.Greater(failure, 0f, "終末は来ず予言が外れる");

            // --- 深く傾倒した信徒ほど不協和が痛む ---
            float dissonanceDevout = ApocalypticMovementRules.CognitiveDissonance(failure, 0.9f);
            float dissonanceCasual = ApocalypticMovementRules.CognitiveDissonance(failure, 0.2f);
            Assert.Greater(dissonanceDevout, dissonanceCasual, "傾倒が深いほど外れの痛みが大きい");

            // --- カリスマある指導者が不協和を信仰深化＝急進化へ転じる（逆説） ---
            float radicalCharismatic = ApocalypticMovementRules.Radicalization(dissonanceDevout, 0.9f);
            float radicalWeak = ApocalypticMovementRules.Radicalization(dissonanceDevout, 0.1f);
            Assert.Greater(radicalCharismatic, radicalWeak, "カリスマが外れを乗り越え運動を急進化させる");

            // --- 社会からの孤立が集団自決の危険を高める ---
            float riskIsolated = ApocalypticMovementRules.CollectiveSuicideRisk(radicalCharismatic, 0.95f);
            float riskConnected = ApocalypticMovementRules.CollectiveSuicideRisk(radicalCharismatic, 0.1f);
            Assert.Greater(riskIsolated, riskConnected, "孤立した急進運動ほど集団自決の危険が高い");
        }
    }
}
