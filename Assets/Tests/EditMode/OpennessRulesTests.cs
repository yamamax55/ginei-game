using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 開放度スペクトル（ポパー型・POPR-1 #1511）の純ロジックを既定 Params の具体値で固定する。
    /// 開放度・自己修正能力・適応速度・誤り蓄積率・開く/閉じる・開放のイノベーション・閉じた社会判定を担保。
    /// </summary>
    public class OpennessRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>開放度＝批判の自由×多元性×(1−教条主義)＝どれか欠ければ閉じる。</summary>
        [Test]
        public void OpennessLevel_積で算出_どれか欠けると閉じる()
        {
            // 0.8 × 0.75 × (1−0.5)=0.3
            Assert.AreEqual(0.3f, OpennessRules.OpennessLevel(0.8f, 0.75f, 0.5f), Eps);
            // 教条が満点なら開放度0
            Assert.AreEqual(0f, OpennessRules.OpennessLevel(1f, 1f, 1f), Eps);
            // 批判の自由ゼロでも0
            Assert.AreEqual(0f, OpennessRules.OpennessLevel(0f, 1f, 0f), Eps);
        }

        /// <summary>自己修正能力＝開放度で下駄0.1〜1.0を線形補間（開かれた社会ほど誤りを正せる）。</summary>
        [Test]
        public void SelfCorrectionCapacity_開放度で下駄から線形に上がる()
        {
            Assert.AreEqual(0.1f, OpennessRules.SelfCorrectionCapacity(0f), Eps);   // 下駄
            Assert.AreEqual(1f, OpennessRules.SelfCorrectionCapacity(1f), Eps);     // 満点
            Assert.AreEqual(0.55f, OpennessRules.SelfCorrectionCapacity(0.5f), Eps); // Lerp(0.1,1,0.5)
        }

        /// <summary>適応速度＝適応率0.05×開放度×環境変化×dt＝開かれた社会ほど速く適応・変化なしは0。</summary>
        [Test]
        public void AdaptationSpeed_開放度と環境変化に比例()
        {
            // 0.05 × 0.8 × 0.5 × 2 = 0.04
            Assert.AreEqual(0.04f, OpennessRules.AdaptationSpeed(0.8f, 0.5f, 2f), Eps);
            // 環境変化0なら適応しない
            Assert.AreEqual(0f, OpennessRules.AdaptationSpeed(1f, 0f, 1f), Eps);
            // 閉じた社会(開放度0)は変化があっても適応しない
            Assert.AreEqual(0f, OpennessRules.AdaptationSpeed(0f, 1f, 1f), Eps);
        }

        /// <summary>誤り蓄積率＝最大0.04×(1−開放度)＝閉じた社会ほど誤りが溜まる・開いた社会は即修正。</summary>
        [Test]
        public void ErrorAccumulationRate_閉じた社会ほど大きい()
        {
            Assert.AreEqual(0.04f, OpennessRules.ErrorAccumulationRate(0f), Eps);  // 閉＝最大
            Assert.AreEqual(0f, OpennessRules.ErrorAccumulationRate(1f), Eps);     // 開＝0
            Assert.AreEqual(0.02f, OpennessRules.ErrorAccumulationRate(0.5f), Eps);
        }

        /// <summary>開く＜閉ざす＝同条件でも閉鎖の方が速い非対称（自由化は時間がかかる）。</summary>
        [Test]
        public void OpeningTick_ClosingTick_閉ざす方が速い非対称()
        {
            // 開く：0.03 × 1 × (1−0.5) × 1 = 0.015 → 0.515
            float opened = OpennessRules.OpeningTick(0.5f, 1f, 1f);
            Assert.AreEqual(0.515f, opened, Eps);
            // 閉ざす：0.06 × 1 × 0.5 × 1 = 0.03 → 0.47
            float closed = OpennessRules.ClosingTick(0.5f, 1f, 1f);
            Assert.AreEqual(0.47f, closed, Eps);
            // 同じ開放度0.5・同じ圧力1・同じdtで閉鎖変化量が開放変化量を上回る
            Assert.Greater(0.5f - closed, opened - 0.5f);
            // 圧力0なら不変
            Assert.AreEqual(0.5f, OpennessRules.OpeningTick(0.5f, 0f, 1f), Eps);
            Assert.AreEqual(0.5f, OpennessRules.ClosingTick(0.5f, 0f, 1f), Eps);
        }

        /// <summary>開放のイノベーション＝才能×(下駄0.2〜1.0を開放度で補間)＝同じ才能でも開いた社会で活きる。</summary>
        [Test]
        public void InnovationFromOpenness_開いた社会で才能が活きる()
        {
            // 開放度1：才能0.8 × 1.0 = 0.8
            Assert.AreEqual(0.8f, OpennessRules.InnovationFromOpenness(1f, 0.8f), Eps);
            // 閉じた社会(開放度0)：才能0.8 × 0.2(下駄) = 0.16
            Assert.AreEqual(0.16f, OpennessRules.InnovationFromOpenness(0f, 0.8f), Eps);
            // 同じ才能でも開いた社会の方がイノベーションが大きい
            Assert.Greater(OpennessRules.InnovationFromOpenness(1f, 0.8f),
                           OpennessRules.InnovationFromOpenness(0f, 0.8f));
        }

        /// <summary>閉じた社会判定＝開放度が閾値以下なら閉鎖（教義で固まり適応できない）。</summary>
        [Test]
        public void IsClosedSociety_閾値以下で閉鎖判定()
        {
            Assert.IsTrue(OpennessRules.IsClosedSociety(0.2f, 0.3f));   // 閾値以下＝閉
            Assert.IsTrue(OpennessRules.IsClosedSociety(0.3f, 0.3f));   // 境界も閉
            Assert.IsFalse(OpennessRules.IsClosedSociety(0.5f, 0.3f));  // 閾値超＝開かれた社会
        }

        /// <summary>OpennessState の生成はフィールドをクランプする。</summary>
        [Test]
        public void OpennessState_生成でクランプ()
        {
            var s = new OpennessState(1.5f, -0.2f, 0.6f);
            Assert.AreEqual(1f, s.openness, Eps);
            Assert.AreEqual(0f, s.criticismFreedom, Eps);
            Assert.AreEqual(0.6f, s.adaptability, Eps);
        }
    }
}
