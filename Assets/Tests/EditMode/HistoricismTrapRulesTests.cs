using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>歴史主義の罠（POPR-5 #1521）の純ロジック検証。</summary>
    public class HistoricismTrapRulesTests
    {
        /// <summary>歴史主義の確信＝法則信仰×イデオロギーの硬さ。どちらか緩めば確信も緩む。</summary>
        [Test]
        public void HistoricistConviction_法則信仰とイデオロギーの硬さの積()
        {
            Assert.AreEqual(0.8f * 0.5f, HistoricismTrapRules.HistoricistConviction(0.8f, 0.5f), 1e-4f);
            // 一方がゼロなら確信ゼロ＝両方が要る。
            Assert.AreEqual(0f, HistoricismTrapRules.HistoricistConviction(1f, 0f), 1e-4f);
            // クランプ確認。
            Assert.AreEqual(1f, HistoricismTrapRules.HistoricistConviction(2f, 2f), 1e-4f);
        }

        /// <summary>適応拒否＝確信×反証×0.8。反証が強いのに適応しないのが罠。確信ゼロは拒否ゼロ。</summary>
        [Test]
        public void AdaptationRefusal_確信が反証の無視を生む()
        {
            // 既定 refusalScale=0.8。
            Assert.AreEqual(0.9f * 0.8f * 0.8f, HistoricismTrapRules.AdaptationRefusal(0.9f, 0.8f), 1e-4f);
            // 確信ゼロなら反証に素直＝拒否ゼロ。
            Assert.AreEqual(0f, HistoricismTrapRules.AdaptationRefusal(0f, 1f), 1e-4f);
        }

        /// <summary>誤り否認＝確信×可視性×0.7。見えている失敗ほど教義を守って否認する。</summary>
        [Test]
        public void ErrorDenial_確信が可視の誤りを逸脱と片付ける()
        {
            // 既定 denialScale=0.7。
            Assert.AreEqual(0.8f * 1f * 0.7f, HistoricismTrapRules.ErrorDenial(0.8f, 1f), 1e-4f);
            // 確信ゼロなら率直に認める＝否認ゼロ。
            Assert.AreEqual(0f, HistoricismTrapRules.ErrorDenial(0f, 1f), 1e-4f);
        }

        /// <summary>脆性蓄積＝(拒否+否認)/2×0.1×dt ぶん溜まる。両方ゼロなら蓄積しない。</summary>
        [Test]
        public void BrittlenessAccumulation_適応拒否と誤り否認が脆性を溜める()
        {
            // drift=(0.6+0.4)/2=0.5、rate0.1、dt2 → +0.1。
            float v = HistoricismTrapRules.BrittlenessAccumulation(0.6f, 0.4f, 0.2f, 2f);
            Assert.AreEqual(0.2f + 0.1f * 0.5f * 2f, v, 1e-4f);
            // 両方ゼロなら据え置き。
            Assert.AreEqual(0.3f, HistoricismTrapRules.BrittlenessAccumulation(0f, 0f, 0.3f, 5f), 1e-4f);
        }

        /// <summary>予言の過信＝確信×上限0.9。どれほど信じても確実な予言にはならない。</summary>
        [Test]
        public void PredictionOverconfidence_歴史法則で未来を読めるという過信()
        {
            // 既定 overconfidenceCeiling=0.9。
            Assert.AreEqual(1f * 0.9f, HistoricismTrapRules.PredictionOverconfidence(1f), 1e-4f);
            Assert.AreEqual(0.5f * 0.9f, HistoricismTrapRules.PredictionOverconfidence(0.5f), 1e-4f);
        }

        /// <summary>ユートピア硬直＝確信×終着点ビジョン。終着点を信じるほど社会を型にはめる。</summary>
        [Test]
        public void UtopianRigidity_終着点ビジョンへの型はめ()
        {
            Assert.AreEqual(0.7f * 0.9f, HistoricismTrapRules.UtopianRigidity(0.7f, 0.9f), 1e-4f);
            // ビジョンが曖昧なら硬直しない。
            Assert.AreEqual(0f, HistoricismTrapRules.UtopianRigidity(1f, 0f), 1e-4f);
        }

        /// <summary>改革麻痺＝適応拒否そのもの（漸進改革を無力と見なす度合い）。</summary>
        [Test]
        public void ReformParalysis_適応拒否が漸進改革を麻痺させる()
        {
            Assert.AreEqual(0.65f, HistoricismTrapRules.ReformParalysis(0.65f), 1e-4f);
            Assert.AreEqual(0f, HistoricismTrapRules.ReformParalysis(0f), 1e-4f);
        }

        /// <summary>歴史主義崩壊＝脆性×衝撃が閾値0.5以上で非線形に崩れる。低脆性は耐え高脆性は小衝撃で崩壊。</summary>
        [Test]
        public void IsHistoricistCollapse_蓄積脆性が衝撃で崩壊()
        {
            // 既定 collapseThreshold=0.5。0.8×0.7=0.56≥0.5 → 崩壊。
            Assert.IsTrue(HistoricismTrapRules.IsHistoricistCollapse(0.8f, 0.7f));
            // 0.4×0.5=0.2<0.5 → 耐える（低脆性）。
            Assert.IsFalse(HistoricismTrapRules.IsHistoricistCollapse(0.4f, 0.5f));
            // 高脆性0.9なら衝撃0.6でも 0.54≥0.5 → 崩壊（硬直ほど脆い）。
            Assert.IsTrue(HistoricismTrapRules.IsHistoricistCollapse(0.9f, 0.6f));
        }
    }
}
