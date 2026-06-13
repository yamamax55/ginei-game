using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// LegalFitnessRules（法の適合性・MONT-5 #1449）の EditMode テスト。
    /// 法の適合度・適合の正統性・輸入法の不適合・不適合の反乱圧力・有機的な法の発展・
    /// 法の移植拒絶・法の精神・適合した法判定を既定 Params の具体値で固定する。
    /// </summary>
    public class LegalFitnessRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>法の適合度＝風土・思想・産業の相乗平均。一致が揃えば適合度=その値。</summary>
        [Test]
        public void LegalFitness_相乗平均で三者の調和を返す()
        {
            // (0.8^3)^(1/3) = 0.8
            Assert.AreEqual(0.8f, LegalFitnessRules.LegalFitness(0.8f, 0.8f, 0.8f), Eps);
            // どれか一つが大きく外れると全体が痩せる（相乗平均）
            float mixed = LegalFitnessRules.LegalFitness(0.9f, 0.6f, 0.3f);
            Assert.AreEqual(Mathf.Pow(0.162f, 1f / 3f), mixed, Eps);
            Assert.Less(mixed, 0.6f);
        }

        /// <summary>適合度が高いほど正統性が高い（基礎下駄0.1＋適合度×0.7）。</summary>
        [Test]
        public void LegitimacyFromFitness_適合するほど正統性が高い()
        {
            Assert.AreEqual(0.66f, LegalFitnessRules.LegitimacyFromFitness(0.8f), Eps); // 0.1+0.8*0.7
            Assert.AreEqual(0.1f, LegalFitnessRules.LegitimacyFromFitness(0f), Eps);    // 下駄のみ
            // 単調増加
            Assert.Greater(LegalFitnessRules.LegitimacyFromFitness(0.9f),
                           LegalFitnessRules.LegitimacyFromFitness(0.5f));
        }

        /// <summary>輸入された外国法は現地文脈が薄いほど不適合が大きい。</summary>
        [Test]
        public void ImportedLawMisfit_現地文脈が薄いほど不適合()
        {
            Assert.AreEqual(0.64f, LegalFitnessRules.ImportedLawMisfit(0.8f, 0.2f), Eps); // 0.8*(1-0.2)
            // 現地文脈が満ちれば不適合は消える
            Assert.AreEqual(0f, LegalFitnessRules.ImportedLawMisfit(1f, 1f), Eps);
            // 外国法でなければ不適合なし
            Assert.AreEqual(0f, LegalFitnessRules.ImportedLawMisfit(0f, 0f), Eps);
        }

        /// <summary>法が社会に合わないほど反乱圧力が高まる（不適合×0.6）。</summary>
        [Test]
        public void RebellionPressureFromMisfit_不適合ほど反乱圧力が高い()
        {
            Assert.AreEqual(0.12f, LegalFitnessRules.RebellionPressureFromMisfit(0.8f), Eps); // (1-0.8)*0.6
            Assert.AreEqual(0.48f, LegalFitnessRules.RebellionPressureFromMisfit(0.2f), Eps); // (1-0.2)*0.6
            // 適合度が高いほど反乱圧力は低い
            Assert.Less(LegalFitnessRules.RebellionPressureFromMisfit(0.9f),
                        LegalFitnessRules.RebellionPressureFromMisfit(0.3f));
        }

        /// <summary>慣習に根ざした法は時間で馴染み適合度が育つ。根が無いと育たない。</summary>
        [Test]
        public void OrganicLawDevelopment_慣習に根ざせば時間で馴染む()
        {
            // 根の深さ1.0×0.05×dt1.0 = 0.05 ぶん 1 へ近づく
            Assert.AreEqual(0.55f, LegalFitnessRules.OrganicLawDevelopment(0.5f, 1f, 1f), Eps);
            // 移植された法（根なし）は育たない
            Assert.AreEqual(0.5f, LegalFitnessRules.OrganicLawDevelopment(0.5f, 0f, 1f), Eps);
            // dt<=0 は変化なし
            Assert.AreEqual(0.5f, LegalFitnessRules.OrganicLawDevelopment(0.5f, 1f, 0f), Eps);
        }

        /// <summary>文化的距離が大きいほど移植された法が拒絶される（法の拒絶反応）。</summary>
        [Test]
        public void LegalTransplantRejection_文化的距離が大きいほど拒絶()
        {
            Assert.AreEqual(0.8f, LegalFitnessRules.LegalTransplantRejection(1f, 1f), Eps); // 1*1*0.8
            // 文化的に近ければ拒絶は小さい
            Assert.AreEqual(0.08f, LegalFitnessRules.LegalTransplantRejection(1f, 0.1f), Eps);
            // 単調増加
            Assert.Greater(LegalFitnessRules.LegalTransplantRejection(1f, 0.9f),
                           LegalFitnessRules.LegalTransplantRejection(1f, 0.3f));
        }

        /// <summary>法の精神＝適合度と政体形態の総合。全要素が整合してはじめて高い。</summary>
        [Test]
        public void SpiritOfLaws_法と政体と社会条件の総合的な精神()
        {
            // fitness=0.8, govForm=1.0 → sqrt(0.8*1)
            Assert.AreEqual(Mathf.Sqrt(0.8f), LegalFitnessRules.SpiritOfLaws(0.8f, 0.8f, 0.8f, 1f), Eps);
            // 政体形態が不適合なら精神は痩せる
            Assert.Less(LegalFitnessRules.SpiritOfLaws(0.8f, 0.8f, 0.8f, 0.2f),
                        LegalFitnessRules.SpiritOfLaws(0.8f, 0.8f, 0.8f, 1f));
            // 政体形態0なら全体が0
            Assert.AreEqual(0f, LegalFitnessRules.SpiritOfLaws(0.8f, 0.8f, 0.8f, 0f), Eps);
        }

        /// <summary>社会によく適合した（機能する）法の判定＝閾値0.6以上。</summary>
        [Test]
        public void IsWellFittedLaw_閾値以上でよく適合した法()
        {
            Assert.IsTrue(LegalFitnessRules.IsWellFittedLaw(0.8f));
            Assert.IsFalse(LegalFitnessRules.IsWellFittedLaw(0.5f));
            // 閾値ちょうどは適合とみなす
            Assert.IsTrue(LegalFitnessRules.IsWellFittedLaw(0.6f));
        }
    }
}
