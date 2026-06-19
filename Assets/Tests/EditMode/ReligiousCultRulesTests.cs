using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 地球教型カルトを固定する：不安な時代ほど教義が効く、魅力と布教が両方そろって信徒が増える、
    /// 篤い信者が献金を太らせる、資金と工作員で要人へ浸透、信仰×過激さで狂信者を動員、
    /// 工作と動員で隠然たる影響力、影響力は当局の警戒で世俗と対立し対立コストが実効権力を削る。
    /// 既定Params期待値を手計算で固定し、入力クランプ・符号付き出力の-1..1を担保する。
    /// </summary>
    public class ReligiousCultRulesTests
    {
        private static readonly ReligiousCultParams P = ReligiousCultParams.Default;
        // 約束重み0.6/不安下駄0.5/獲得スケール1.0/献金1.0/資金換算0.01/工作員下駄0.3/工作重み0.6/警戒下駄0.2/対立コスト0.8

        [Test]
        public void DoctrineAppeal_AnxiousAgeAmplifiesAppeal()
        {
            // 不安1：1×0.6×(0.5+0.5×1)=0.6
            Assert.AreEqual(0.6f, ReligiousCultRules.DoctrineAppeal(1f, 1f, P), 1e-4f);
            // 不安0：1×0.6×0.5=0.3（不安な時代ほど効く＝平穏では半減）
            Assert.AreEqual(0.3f, ReligiousCultRules.DoctrineAppeal(1f, 0f, P), 1e-4f);
            // 不安が高いほど魅力が単調増加
            Assert.Greater(ReligiousCultRules.DoctrineAppeal(0.8f, 1f, P),
                           ReligiousCultRules.DoctrineAppeal(0.8f, 0f, P));
        }

        [Test]
        public void DoctrineAppeal_ClampsInputs()
        {
            // 範囲外はクランプ＝最大でも 1×0.6×1=0.6
            Assert.AreEqual(0.6f, ReligiousCultRules.DoctrineAppeal(5f, 5f, P), 1e-4f);
            // 救済の約束が無ければ魅力ゼロ
            Assert.AreEqual(0f, ReligiousCultRules.DoctrineAppeal(-1f, 1f, P), 1e-4f);
        }

        [Test]
        public void ConvertAcquisition_NeedsBothAppealAndEffort()
        {
            // 0.36×0.25=0.09 → sqrt=0.3（Pow/Sqrt箇所＝1e-3f）
            Assert.AreEqual(0.3f, ReligiousCultRules.ConvertAcquisition(0.36f, 0.25f, P), 1e-3f);
            // 満額＝1
            Assert.AreEqual(1f, ReligiousCultRules.ConvertAcquisition(1f, 1f, P), 1e-3f);
            // 布教努力ゼロなら魅力満点でも広まらない（幾何平均＝片方薄いと崩れる）
            Assert.AreEqual(0f, ReligiousCultRules.ConvertAcquisition(1f, 0f, P), 1e-4f);
        }

        [Test]
        public void TitheRevenue_ScalesWithFlockAndDevotion()
        {
            // 100信徒×篤さ0.8×1.0=80
            Assert.AreEqual(80f, ReligiousCultRules.TitheRevenue(100f, 0.8f, P), 1e-4f);
            // 信仰が冷めれば（0）献金は枯れる
            Assert.AreEqual(0f, ReligiousCultRules.TitheRevenue(100f, 0f, P), 1e-4f);
            // 負の信徒数は0扱い
            Assert.AreEqual(0f, ReligiousCultRules.TitheRevenue(-50f, 0.8f, P), 1e-4f);
        }

        [Test]
        public void PoliticalInfiltration_NeedsFundsAndAgents()
        {
            // 資金100×0.01=1.0（飽和）、工作員1.0 → 1×(0.3+0.7×1)=1.0
            Assert.AreEqual(1f, ReligiousCultRules.PoliticalInfiltration(100f, 1f, P), 1e-4f);
            // 資金50×0.01=0.5、工作員0.5 → 0.5×(0.3+0.7×0.5)=0.5×0.65=0.325
            Assert.AreEqual(0.325f, ReligiousCultRules.PoliticalInfiltration(50f, 0.5f, P), 1e-4f);
            // 工作員ゼロでも下駄ぶんは通る：0.5×0.3=0.15
            Assert.AreEqual(0.15f, ReligiousCultRules.PoliticalInfiltration(50f, 0f, P), 1e-4f);
        }

        [Test]
        public void FanaticMobilization_GeometricMeanOfDevotionAndRadicalism()
        {
            // 0.64×0.25=0.16 → sqrt=0.4
            Assert.AreEqual(0.4f, ReligiousCultRules.FanaticMobilization(0.64f, 0.25f, P), 1e-3f);
            Assert.AreEqual(1f, ReligiousCultRules.FanaticMobilization(1f, 1f, P), 1e-3f);
            // 過激さゼロ＝穏健な信仰は手駒にならない
            Assert.AreEqual(0f, ReligiousCultRules.FanaticMobilization(1f, 0f, P), 1e-4f);
        }

        [Test]
        public void HiddenInfluence_BlendsInfiltrationAndMobilization()
        {
            // 1×0.6＋1×0.4=1.0
            Assert.AreEqual(1f, ReligiousCultRules.HiddenInfluence(1f, 1f, P), 1e-4f);
            // 工作のみ＝0.6、動員のみ＝0.4（上層浸透の重みが大きい）
            Assert.AreEqual(0.6f, ReligiousCultRules.HiddenInfluence(1f, 0f, P), 1e-4f);
            Assert.AreEqual(0.4f, ReligiousCultRules.HiddenInfluence(0f, 1f, P), 1e-4f);
        }

        [Test]
        public void SecularConflict_RisesWithInfluenceAndVigilance()
        {
            // 影響力1×(0.2+0.8×1)=1.0
            Assert.AreEqual(1f, ReligiousCultRules.SecularConflict(1f, 1f, P), 1e-4f);
            // 当局が無警戒でも露見した影響力は摩擦を生む：1×0.2=0.2
            Assert.AreEqual(0.2f, ReligiousCultRules.SecularConflict(1f, 0f, P), 1e-4f);
            // 影響力0.5・警戒0.5：0.5×(0.2+0.8×0.5)=0.5×0.6=0.3
            Assert.AreEqual(0.3f, ReligiousCultRules.SecularConflict(0.5f, 0.5f, P), 1e-4f);
        }

        [Test]
        public void CultPower_InfluenceMinusConflictCost()
        {
            // 影響力1・対立0＝満額1（誰にも気づかれず操る理想形）
            Assert.AreEqual(1f, ReligiousCultRules.CultPower(1f, 0f, P), 1e-4f);
            // 影響力1・対立1：1-0.8×1=0.2（激突して実効が削れる）
            Assert.AreEqual(0.2f, ReligiousCultRules.CultPower(1f, 1f, P), 1e-4f);
            // 影響力0・対立1：0-0.8=-0.8（弾圧されて潰える＝負）
            Assert.AreEqual(-0.8f, ReligiousCultRules.CultPower(0f, 1f, P), 1e-4f);
        }

        [Test]
        public void Narrative_TerraCultRisesInTroubledTimesThenClashesWithState()
        {
            // 物語：乱世（社会不安0.9）で救済を説く地球教が信徒を集め、献金で政界へ食い込み、
            // 狂信者を動員して隠然たる権力を握るが、やがて当局に警戒され世俗権力と激突する。
            var p = ReligiousCultParams.Default;

            // 乱世＝高不安で教義が効く（平時の不安0.1より魅力が大きい）
            float appealTroubled = ReligiousCultRules.DoctrineAppeal(0.9f, 0.9f, p);
            float appealCalm = ReligiousCultRules.DoctrineAppeal(0.9f, 0.1f, p);
            Assert.Greater(appealTroubled, appealCalm);

            // 熱心な布教で信徒を獲得
            float converts = ReligiousCultRules.ConvertAcquisition(appealTroubled, 0.8f, p);
            Assert.Greater(converts, 0f);

            // 篤い信徒5000人の献金
            float tithe = ReligiousCultRules.TitheRevenue(5000f * converts, 0.85f, p);
            Assert.Greater(tithe, 0f);

            // 資金と工作員で政界浸透・狂信者動員
            float infiltration = ReligiousCultRules.PoliticalInfiltration(tithe, 0.7f, p);
            float fanatics = ReligiousCultRules.FanaticMobilization(0.85f, 0.8f, p);
            float influence = ReligiousCultRules.HiddenInfluence(infiltration, fanatics, p);
            Assert.Greater(influence, 0f);

            // 当局がほぼ無警戒なら隠然と君臨（権力＞0）、強く弾圧されれば実効権力が崩れる
            float conflictHidden = ReligiousCultRules.SecularConflict(influence, 0.05f, p);
            float powerHidden = ReligiousCultRules.CultPower(influence, conflictHidden, p);
            Assert.Greater(powerHidden, 0f);

            float conflictExposed = ReligiousCultRules.SecularConflict(influence, 1f, p);
            float powerExposed = ReligiousCultRules.CultPower(influence, conflictExposed, p);
            Assert.Less(powerExposed, powerHidden);   // 激突すると実効権力が目減りする
            Assert.GreaterOrEqual(powerExposed, -1f); // 符号付き出力は-1..1にクランプ
            Assert.LessOrEqual(powerHidden, 1f);
        }
    }
}
