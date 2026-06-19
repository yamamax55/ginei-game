using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>革命細胞（秘匿・急進化・大衆浸透・武装・摘発・生存・機運）の純ロジックを EditMode で担保する。</summary>
    public class RevolutionaryCellRulesTests
    {
        const float Eps = 1e-4f;
        const float PowEps = 1e-3f;

        /// <summary>細胞秘匿：分散構造×規律で立ち、どちらか欠ければ崩れる。</summary>
        [Test]
        public void CellSecurity_NeedsCompartmentalizationAndDiscipline()
        {
            // 0.8*0.7 = 0.56
            float sec = RevolutionaryCellRules.CellSecurity(0.8f, 0.7f);
            Assert.AreEqual(0.56f, sec, Eps);

            // 規律が無ければ秘匿は崩れる
            float undisciplined = RevolutionaryCellRules.CellSecurity(1f, 0f);
            Assert.AreEqual(0f, undisciplined, Eps);
        }

        /// <summary>急進化：不満強度×弾圧体験の幾何平均（どちらか0なら過激化しない）。</summary>
        [Test]
        public void Radicalization_IsGeometricMean()
        {
            // sqrt(0.81*0.64) = sqrt(0.5184) = 0.72
            float rad = RevolutionaryCellRules.Radicalization(0.81f, 0.64f);
            Assert.AreEqual(0.72f, rad, PowEps);

            // 弾圧体験が無ければ過激化しない
            float calm = RevolutionaryCellRules.Radicalization(1f, 0f);
            Assert.AreEqual(0f, calm, Eps);
        }

        /// <summary>大衆浸透：宣伝到達×大衆の不満（届かぬ宣伝も満ち足りた大衆も浸透しない）。</summary>
        [Test]
        public void MassPenetration_NeedsReachAndGrievance()
        {
            // 0.6*0.5 = 0.3
            float pen = RevolutionaryCellRules.MassPenetration(0.6f, 0.5f);
            Assert.AreEqual(0.3f, pen, Eps);

            // 大衆に不満が無ければ浸透しない
            float content = RevolutionaryCellRules.MassPenetration(1f, 0f);
            Assert.AreEqual(0f, content, Eps);
        }

        /// <summary>武装準備：武器備蓄×軍事訓練の幾何平均（どちらか0なら準備不足）。</summary>
        [Test]
        public void ArmamentReadiness_IsGeometricMean()
        {
            // sqrt(0.64*0.25) = sqrt(0.16) = 0.4
            float arm = RevolutionaryCellRules.ArmamentReadiness(0.64f, 0.25f);
            Assert.AreEqual(0.4f, arm, PowEps);

            // 訓練が無ければ武器があっても扱えない
            float untrained = RevolutionaryCellRules.ArmamentReadiness(1f, 0f);
            Assert.AreEqual(0f, untrained, Eps);
        }

        /// <summary>摘発リスク：当局の諜報×(1-秘匿)＝秘匿が完璧なら諜報が強くても露見しない。</summary>
        [Test]
        public void StateInfiltration_ScalesWithExposure()
        {
            // 0.8*(1-0.5) = 0.8*0.5 = 0.4
            float risk = RevolutionaryCellRules.StateInfiltration(0.8f, 0.5f);
            Assert.AreEqual(0.4f, risk, Eps);

            // 秘匿が完璧なら摘発されない
            float hidden = RevolutionaryCellRules.StateInfiltration(1f, 1f);
            Assert.AreEqual(0f, hidden, Eps);

            // 諜報が無ければ秘匿が甘くても摘発されない
            float blind = RevolutionaryCellRules.StateInfiltration(0f, 0f);
            Assert.AreEqual(0f, blind, Eps);
        }

        /// <summary>組織の生存：秘匿×浸透×(1-摘発)の幾何平均（三乗根・どれか崩れると潰れる）。</summary>
        [Test]
        public void OrganizationalSurvival_IsGeometricMeanOfThree()
        {
            // 0.5*0.5*(1-0.5) = 0.125 ; cbrt = 0.5
            float surv = RevolutionaryCellRules.OrganizationalSurvival(0.5f, 0.5f, 0.5f);
            Assert.AreEqual(0.5f, surv, PowEps);

            // 摘発確実（リスク1）なら組織は潰れる
            float busted = RevolutionaryCellRules.OrganizationalSurvival(0.9f, 0.9f, 1f);
            Assert.AreEqual(0f, busted, PowEps);
        }

        /// <summary>革命の機運：浸透×急進×体制の弱体の幾何平均（三乗根・どれか欠ければ機は熟さない）。</summary>
        [Test]
        public void RevolutionaryMomentum_IsGeometricMeanOfThree()
        {
            // 0.8*0.8*0.8 = 0.512 ; cbrt = 0.8
            float mom = RevolutionaryCellRules.RevolutionaryMomentum(0.8f, 0.8f, 0.8f);
            Assert.AreEqual(0.8f, mom, PowEps);

            // 体制が盤石（弱体0）なら機運は醸成されない
            float stable = RevolutionaryCellRules.RevolutionaryMomentum(0.9f, 0.9f, 0f);
            Assert.AreEqual(0f, stable, PowEps);
        }

        /// <summary>蜂起判定：革命機運×武装準備が閾値以上＝機運も武装も要る。</summary>
        [Test]
        public void IsRevolutionViable_NeedsMomentumAndArmament()
        {
            // 0.6*0.5 = 0.3 >= 0.3 ＝蜂起できる
            Assert.IsTrue(RevolutionaryCellRules.IsRevolutionViable(0.6f, 0.5f));

            // 機運はあるが丸腰（0.8*0.2=0.16 < 0.3）＝蜂起できない
            Assert.IsFalse(RevolutionaryCellRules.IsRevolutionViable(0.8f, 0.2f));

            // 武装はあるが機運不足（0.2*1=0.2 < 0.3）＝大衆がついてこない
            Assert.IsFalse(RevolutionaryCellRules.IsRevolutionViable(0.2f, 1f));
        }

        /// <summary>物語：芽生えたばかりの孤立した細胞が、弾圧と不満を糧に急進化・浸透・武装し、弱体化した体制に革命を起こす。</summary>
        [Test]
        public void Narrative_NascentCellGrowsIntoViableRevolution()
        {
            // 初期：規律も浸透も低く、当局の諜報が強く、体制も盤石＝蜂起不能
            float earlySec = RevolutionaryCellRules.CellSecurity(0.3f, 0.3f);
            float earlyRad = RevolutionaryCellRules.Radicalization(0.3f, 0.2f);
            float earlyPen = RevolutionaryCellRules.MassPenetration(0.2f, 0.3f);
            float earlyArm = RevolutionaryCellRules.ArmamentReadiness(0.1f, 0.1f);
            float earlyRisk = RevolutionaryCellRules.StateInfiltration(0.9f, earlySec);
            float earlySurv = RevolutionaryCellRules.OrganizationalSurvival(earlySec, earlyPen, earlyRisk);
            float earlyMom = RevolutionaryCellRules.RevolutionaryMomentum(earlyPen, earlyRad, 0.2f);
            Assert.IsFalse(RevolutionaryCellRules.IsRevolutionViable(earlyMom, earlyArm),
                "芽生えたばかりの孤立した細胞は蜂起できない");

            // 成熟：分散秘匿が進み、弾圧体験で過激化し、宣伝が大衆へ浸透し、武装を整え、体制が弱体化
            float lateSec = RevolutionaryCellRules.CellSecurity(0.85f, 0.8f);
            float lateRad = RevolutionaryCellRules.Radicalization(0.8f, 0.8f);
            float latePen = RevolutionaryCellRules.MassPenetration(0.8f, 0.85f);
            float lateArm = RevolutionaryCellRules.ArmamentReadiness(0.7f, 0.7f);
            float lateRisk = RevolutionaryCellRules.StateInfiltration(0.9f, lateSec);
            float lateSurv = RevolutionaryCellRules.OrganizationalSurvival(lateSec, latePen, lateRisk);
            float lateMom = RevolutionaryCellRules.RevolutionaryMomentum(latePen, lateRad, 0.8f);
            Assert.IsTrue(RevolutionaryCellRules.IsRevolutionViable(lateMom, lateArm),
                "急進化し浸透し武装した細胞は弱体化した体制に革命を起こせる");

            // 成熟期はあらゆる指標で初期を上回り、摘発リスクは下がる
            Assert.Greater(lateMom, earlyMom);
            Assert.Greater(lateSurv, earlySurv);
            Assert.Less(lateRisk, earlyRisk);
        }

        /// <summary>既定 Params の具体値を固定（回帰防止）。</summary>
        [Test]
        public void Default_HasExpectedValues()
        {
            var p = RevolutionaryCellParams.Default;
            Assert.AreEqual(1.0f, p.securityWeight, Eps);
            Assert.AreEqual(1.0f, p.radicalizationWeight, Eps);
            Assert.AreEqual(1.0f, p.penetrationWeight, Eps);
            Assert.AreEqual(1.0f, p.armamentWeight, Eps);
            Assert.AreEqual(1.0f, p.infiltrationWeight, Eps);
            Assert.AreEqual(1.0f, p.momentumWeight, Eps);
            Assert.AreEqual(0.3f, RevolutionaryCellRules.DefaultRevolutionThreshold, Eps);
        }
    }
}
