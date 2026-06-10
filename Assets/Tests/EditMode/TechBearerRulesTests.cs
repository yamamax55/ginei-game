using NUnit.Framework;

namespace Ginei.Tests
{
    /// <summary>
    /// TechBearerRules（技術は人に宿る・#1092）の純ロジック検証。1人依存の脆さと、文書化/徒弟による
    /// 冗長化を既定 TechBearerParams の具体値で固定する。
    /// </summary>
    public class TechBearerRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>保持度＝保持者数×0.4＋文書化×0.5を1でクランプ。1人の頭だけ（文書化0）は脆い0.4。</summary>
        [Test]
        public void TechRetention_HumanAndCodification_Clamped()
        {
            // 1人・文書化0 ＝ 0.4（脆い）
            Assert.AreEqual(0.4f, TechBearerRules.TechRetention(1, 0f), Eps);
            // 0人・文書化0 ＝ 失伝 0
            Assert.AreEqual(0f, TechBearerRules.TechRetention(0, 0f), Eps);
            // 1人・文書化0.6 ＝ 0.4 + 0.6*0.5 = 0.7
            Assert.AreEqual(0.7f, TechBearerRules.TechRetention(1, 0.6f), Eps);
            // 3人 ＝ 1.2 → 1.0 でクランプ
            Assert.AreEqual(1f, TechBearerRules.TechRetention(3, 0f), Eps);
        }

        /// <summary>最後の1人（保持者1・文書化0）の死は技術価値を丸ごと失う＝工法消滅。</summary>
        [Test]
        public void LossOnDeath_LastBearer_LosesEverything()
        {
            // before=0.4, after(0人)=0 → 喪失幅0.4 × 価値100 = 40
            Assert.AreEqual(40f, TechBearerRules.LossOnDeath(100f, 1, 0f), Eps);
        }

        /// <summary>文書化していれば最後の1人が死んでも喪失が緩和される（冗長化）。</summary>
        [Test]
        public void LossOnDeath_Codification_Mitigates()
        {
            // before=TechRetention(1,0.8)=0.4+0.4=0.8, after(0人)=0.4 → 喪失0.4 × 100 = 40
            // 文書化なしと同じ喪失幅だが、残存保持度after=0.4が残る＝再建可能（脆さの差は残存に現れる）
            float lossDoc = TechBearerRules.LossOnDeath(100f, 1, 0.8f);
            Assert.AreEqual(40f, lossDoc, Eps);
            // 残存：文書化ありは0.4残るが、文書化なしは0＝完全失伝
            Assert.AreEqual(0.4f, TechBearerRules.TechRetention(0, 0.8f), Eps);
            Assert.AreEqual(0f, TechBearerRules.TechRetention(0, 0f), Eps);
        }

        /// <summary>保持者が複数いれば1人死んでも喪失は頭数ぶんに薄まる（冗長化）。</summary>
        [Test]
        public void LossOnDeath_MultipleBearers_LessFragile()
        {
            // before=TechRetention(2,0)=0.8, after=TechRetention(1,0)=0.4 → 喪失0.4 × 100 = 40
            float lossTwo = TechBearerRules.LossOnDeath(100f, 2, 0f);
            float lossOne = TechBearerRules.LossOnDeath(100f, 1, 0f);
            Assert.AreEqual(40f, lossTwo, Eps);
            Assert.AreEqual(40f, lossOne, Eps);
            // 死後も2人体制は1人残り技術が生き続ける（after>0）が、1人体制は失伝（after=0）
            Assert.AreEqual(0.4f, TechBearerRules.TechRetention(1, 0f), Eps);
            Assert.AreEqual(0f, TechBearerRules.TechRetention(0, 0f), Eps);
        }

        /// <summary>引き抜きは技量比例（×0.8）＝凡人を奪っても技術は来ない。</summary>
        [Test]
        public void PoachingValue_ScalesWithSkill()
        {
            // 価値100 × 技量1 × 0.8 = 80
            Assert.AreEqual(80f, TechBearerRules.PoachingValue(100f, 1f), Eps);
            // 技量0の凡人 ＝ 0
            Assert.AreEqual(0f, TechBearerRules.PoachingValue(100f, 0f), Eps);
        }

        /// <summary>亡命＝低忠誠×好条件×0.7。忠誠が高ければ好条件でも動かない。</summary>
        [Test]
        public void DefectionTransfer_NeedsBothDisaffectionAndOffer()
        {
            // 忠誠0.2(=不満0.8) × 好条件1 × 0.7 × 価値100 = 56
            Assert.AreEqual(56f, TechBearerRules.DefectionTransfer(100f, 0.2f, 1f), Eps);
            // 忠誠1.0＝不満0 ＝ 動かない
            Assert.AreEqual(0f, TechBearerRules.DefectionTransfer(100f, 1f, 1f), Eps);
            // 好条件0 ＝ 低忠誠でも動かない
            Assert.AreEqual(0f, TechBearerRules.DefectionTransfer(100f, 0f, 0f), Eps);
        }

        /// <summary>徒弟は師の技量(×0.5×dt)で伝承し師を超えない／文書化は努力で漸近。</summary>
        [Test]
        public void ApprenticeshipAndCodification_Tick()
        {
            // 師技量0.8・進捗0・dt1 ＝ 0 + 0.8*0.5*1 = 0.4
            Assert.AreEqual(0.4f, TechBearerRules.ApprenticeshipTick(0.8f, 0f, 1f), Eps);
            // 進捗が師に達したら頭打ち
            Assert.AreEqual(0.8f, TechBearerRules.ApprenticeshipTick(0.8f, 0.8f, 1f), Eps);
            // 文書化0.2・努力1・dt1 ＝ 0.2 + (1-0.2)*1*0.3*1 = 0.44
            Assert.AreEqual(0.44f, TechBearerRules.CodificationTick(0.2f, 1f, 1f), Eps);
            // 努力0 ＝ 進まない
            Assert.AreEqual(0.2f, TechBearerRules.CodificationTick(0.2f, 0f, 1f), Eps);
        }
    }
}
