using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 正義の天秤（サンデル #918-923）を固定する：各正義観の是認の分岐、是認のクランプ、住民構成への加重和→正統性増減、
    /// 最も不満な正義観の特定。決定論で境界・クランプ・全分岐を担保する。
    /// </summary>
    public class JusticeRulesTests
    {
        // ヘルパ：住民構成を作る
        private static List<(JusticeView, float)> Pop(params (JusticeView, float)[] entries)
            => new List<(JusticeView, float)>(entries);

        [Test]
        public void Approval_EachViewWeighsDifferentPolicyFeature()
        {
            // 高再分配・低自由・低功績・低共同体の政策
            float redist = 1f, lib = 0f, merit = 0f, comm = 0f;

            // ロールズは高再分配を是とする＝最大
            Assert.AreEqual(1f, JusticeRules.Approval(JusticeView.ロールズ, redist, lib, merit, comm), 1e-5f);
            // リバタリアンは高再分配を不正とみなす＝最小（自由0−再分配ペナルティ全幅）
            Assert.AreEqual(0f, JusticeRules.Approval(JusticeView.リバタリアン, redist, lib, merit, comm), 1e-5f);
            // アリストテレスは功績主義＝0
            Assert.AreEqual(0f, JusticeRules.Approval(JusticeView.アリストテレス, redist, lib, merit, comm), 1e-5f);
            // 共通善は共同体忠誠＝0
            Assert.AreEqual(0f, JusticeRules.Approval(JusticeView.共通善, redist, lib, merit, comm), 1e-5f);
            // 功利主義は再分配/自由のバランス（既定0.5/0.5）＝0.5
            Assert.AreEqual(0.5f, JusticeRules.Approval(JusticeView.功利主義, redist, lib, merit, comm), 1e-5f);
        }

        [Test]
        public void Approval_MeritAndCommunalFeaturesRespected()
        {
            // 功績主義のみ高い政策
            Assert.AreEqual(0.8f, JusticeRules.Approval(JusticeView.アリストテレス, 0f, 0f, 0.8f, 0f), 1e-5f);
            // 共同体忠誠のみ高い政策
            Assert.AreEqual(0.7f, JusticeRules.Approval(JusticeView.共通善, 0f, 0f, 0f, 0.7f), 1e-5f);
        }

        [Test]
        public void Approval_ClampsInputsAndOutput()
        {
            // 入力が範囲外でもクランプ：再分配2→1、自由-1→0 でリバタリアンは 0-1*1=0 にクランプ
            float a = JusticeRules.Approval(JusticeView.リバタリアン, 2f, -1f, 0f, 0f);
            Assert.AreEqual(0f, a, 1e-5f);
            // 出力は0未満にならない（クランプ）
            Assert.GreaterOrEqual(a, 0f);
        }

        [Test]
        public void LegitimacyDelta_PositiveWhenApproved_NegativeWhenRejected()
        {
            var p = JusticeParams.Default; // legitimacySwing=0.2
            // ロールズだけの住民に高再分配＝全是認＝+swing
            var rawls = Pop((JusticeView.ロールズ, 1f));
            Assert.AreEqual(0.2f, JusticeRules.LegitimacyDelta(rawls, 1f, 0f, 0f, 0f), 1e-5f);
            // リバタリアンだけの住民に高再分配＝全否認＝-swing
            var libt = Pop((JusticeView.リバタリアン, 1f));
            Assert.AreEqual(-0.2f, JusticeRules.LegitimacyDelta(libt, 1f, 0f, 0f, 0f), 1e-5f);
        }

        [Test]
        public void LegitimacyDelta_WeightedSumAcrossPopulace()
        {
            // ロールズ(是認1.0)とリバタリアン(否認0.0)を同重み＝加重平均0.5＝中立＝delta0
            var mixed = Pop((JusticeView.ロールズ, 1f), (JusticeView.リバタリアン, 1f));
            Assert.AreEqual(0f, JusticeRules.LegitimacyDelta(mixed, 1f, 0f, 0f, 0f), 1e-5f);
        }

        [Test]
        public void LegitimacyDelta_EmptyOrZeroWeightPopulace_IsNeutral()
        {
            // 空＝中立0.5＝delta0
            Assert.AreEqual(0f, JusticeRules.LegitimacyDelta(Pop(), 1f, 0f, 0f, 0f), 1e-5f);
            // 全重み0＝中立
            var zero = Pop((JusticeView.ロールズ, 0f), (JusticeView.リバタリアン, 0f));
            Assert.AreEqual(0f, JusticeRules.LegitimacyDelta(zero, 1f, 0f, 0f, 0f), 1e-5f);
            // null も中立
            Assert.AreEqual(0f, JusticeRules.LegitimacyDelta(null, 1f, 0f, 0f, 0f), 1e-5f);
        }

        [Test]
        public void DominantGrievance_PicksLeastSatisfiedView()
        {
            // 高再分配政策：リバタリアン(0)が最も不満、ロールズ(1)は満足
            var pop = Pop((JusticeView.ロールズ, 1f), (JusticeView.リバタリアン, 1f), (JusticeView.功利主義, 1f));
            var worst = JusticeRules.DominantGrievance(pop, 1f, 0f, 0f, 0f, out bool has);
            Assert.IsTrue(has);
            Assert.AreEqual(JusticeView.リバタリアン, worst);
        }

        [Test]
        public void DominantGrievance_IgnoresZeroWeight_AndEmptyHasNone()
        {
            // 重み0の正義観は不満集計に数えない：リバタリアンを重み0にすればロールズ住民の中で最低が選ばれる
            var pop = Pop((JusticeView.リバタリアン, 0f), (JusticeView.共通善, 1f));
            // 高再分配・共同体忠誠0＝共通善が最低の有効正義観
            var worst = JusticeRules.DominantGrievance(pop, 1f, 0f, 0f, 0f, out bool has);
            Assert.IsTrue(has);
            Assert.AreEqual(JusticeView.共通善, worst);

            // 空住民＝該当なし
            JusticeRules.DominantGrievance(Pop(), 1f, 0f, 0f, 0f, out bool hasEmpty);
            Assert.IsFalse(hasEmpty);
            // null も該当なし
            JusticeRules.DominantGrievance(null, 1f, 0f, 0f, 0f, out bool hasNull);
            Assert.IsFalse(hasNull);
        }
    }
}
