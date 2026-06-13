using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 歴史叙述（勝者が歴史を書く）を固定する：公式評価は政権との整合で歪み（整合は持ち上げ・敵対は貶め）、
    /// 政権交代の再評価は新政権の都合＋旧叙述の慣性で真実には戻らない。生き証人の退場で検証可能性が痩せ、
    /// 学問の自由だけが歴史家の抵抗力となり、長期の審判を真実へ収束させる。既定 Params の具体値で担保。
    /// </summary>
    public class HistoriographyRulesTests
    {
        private static readonly HistoriographyParams P = HistoriographyParams.Default;

        [Test]
        public void OfficialVerdict_政権に都合よく歪む()
        {
            // 政権に整合(+1)した凡将(0.3)は持ち上げられる＝Lerp(0.3,1,0.6)=0.72
            Assert.AreEqual(0.72f, HistoriographyRules.OfficialVerdict(0.3f, 1f, P), 1e-5f);
            // 政権に敵対(-1)した英雄(0.8)は貶められる＝Lerp(0.8,0,0.6)=0.32
            Assert.AreEqual(0.32f, HistoriographyRules.OfficialVerdict(0.8f, -1f, P), 1e-5f);
            // 政権に無関係(0)の人物だけが真実どおり書かれる
            Assert.AreEqual(0.8f, HistoriographyRules.OfficialVerdict(0.8f, 0f, P), 1e-5f);
        }

        [Test]
        public void DistortionGap_改竄の幅は公式と真実の乖離()
        {
            // 貶められた英雄：|0.32-0.8|=0.48
            float official = HistoriographyRules.OfficialVerdict(0.8f, -1f, P);
            Assert.AreEqual(0.48f, HistoriographyRules.DistortionGap(official, 0.8f), 1e-5f);
            // 歪み無しなら乖離ゼロ
            Assert.AreEqual(0f, HistoriographyRules.DistortionGap(0.8f, 0.8f), 1e-5f);
        }

        [Test]
        public void RevisionOnRegimeChange_政権交代でも真実には戻らない()
        {
            // 旧政権で貶められた英雄（真実0.8→公式0.32）を、新政権が味方(+1)として再評価：
            // 新政権の都合の評価=Lerp(0.8,1,0.6)=0.92 へ revisionRate=0.7 だけ動く＝0.32+0.6*0.7=0.74
            float revised = HistoriographyRules.RevisionOnRegimeChange(0.32f, 0.8f, 1f, P);
            Assert.AreEqual(0.74f, revised, 1e-5f);
            Assert.AreNotEqual(0.8f, revised); // 名誉回復しても真実(0.8)そのものには戻らない

            // 新政権が中立(0)でも旧叙述の慣性が残る＝0.32+0.48*0.7=0.656（真実0.8には届かない）
            float neutral = HistoriographyRules.RevisionOnRegimeChange(0.32f, 0.8f, 0f, P);
            Assert.AreEqual(0.656f, neutral, 1e-5f);
            Assert.Less(neutral, 0.8f);
        }

        [Test]
        public void TruthErosionTick_証人の退場で検証可能性が痩せる()
        {
            // 証人が充足(10名)なら痩せない
            Assert.AreEqual(1f, HistoriographyRules.TruthErosionTick(1f, 10, 1f, P), 1e-5f);
            // 証人ゼロなら最大速度で痩せる＝1-0.1*1*1=0.9
            Assert.AreEqual(0.9f, HistoriographyRules.TruthErosionTick(1f, 0, 1f, P), 1e-5f);
            // 半減(5名)なら半分の速さ＝1-0.1*0.5*1=0.95
            Assert.AreEqual(0.95f, HistoriographyRules.TruthErosionTick(1f, 5, 1f, P), 1e-5f);
            // 0未満にはならない
            Assert.AreEqual(0f, HistoriographyRules.TruthErosionTick(0.05f, 0, 10f, P), 1e-5f);
        }

        [Test]
        public void HistorianIntegrity_学問の自由だけが歪みを抑える()
        {
            // 自由ゼロなら圧力が無くても抵抗力ゼロ
            Assert.AreEqual(0f, HistoriographyRules.HistorianIntegrity(0f, 0f, P), 1e-5f);
            // 完全な自由×圧力無し＝満点
            Assert.AreEqual(1f, HistoriographyRules.HistorianIntegrity(0f, 1f, P), 1e-5f);
            // 最大圧力でも自由が土台なら半分残る＝1*(1-1*0.5)=0.5
            Assert.AreEqual(0.5f, HistoriographyRules.HistorianIntegrity(1f, 1f, P), 1e-5f);
            // 中間値：0.8*(1-0.5*0.5)=0.6
            Assert.AreEqual(0.6f, HistoriographyRules.HistorianIntegrity(0.5f, 0.8f, P), 1e-5f);
        }

        [Test]
        public void LongRunVerdict_自由な学問は長期で真実へ収束()
        {
            // 抵抗力1＝真実(0.8)へ完全収束
            Assert.AreEqual(0.8f, HistoriographyRules.LongRunVerdict(0.32f, 0.8f, 1f), 1e-5f);
            // 統制下(抵抗力0)＝政権の都合(0.32)のまま
            Assert.AreEqual(0.32f, HistoriographyRules.LongRunVerdict(0.32f, 0.8f, 0f), 1e-5f);
            // 中間の自由＝中間まで＝Lerp(0.32,0.8,0.5)=0.56
            Assert.AreEqual(0.56f, HistoriographyRules.LongRunVerdict(0.32f, 0.8f, 0.5f), 1e-5f);
        }

        [Test]
        public void LongRunVerdict_検証可能性が失われれば自由でも戻れない()
        {
            // 学問が自由でも証人と記録が消えていれば（verifiability=0）公式評価のまま
            Assert.AreEqual(0.32f, HistoriographyRules.LongRunVerdict(0.32f, 0.8f, 1f, 0f), 1e-5f);
            // 半分残っていれば半分だけ戻る＝Lerp(0.32,0.8,1*0.5)=0.56
            Assert.AreEqual(0.56f, HistoriographyRules.LongRunVerdict(0.32f, 0.8f, 1f, 0.5f), 1e-5f);
        }

        [Test]
        public void Params_ctorクランプ()
        {
            var p = new HistoriographyParams(2f, -1f, -0.5f, 0, 2f);
            Assert.AreEqual(1f, p.distortionStrength, 1e-5f);
            Assert.AreEqual(0f, p.revisionRate, 1e-5f);
            Assert.AreEqual(0f, p.erosionRate, 1e-5f);
            Assert.AreEqual(1, p.witnessSufficiency);
            Assert.AreEqual(1f, p.suppressionWeight, 1e-5f);
        }
    }
}
