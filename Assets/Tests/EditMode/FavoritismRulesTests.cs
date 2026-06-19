using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 依怙贔屓・縁故人事を固定する：贔屓の度合い（縁故/(縁故+客観性)）、人材配置の損失、
    /// 見過ごされた有能者の士気低下（符号付き）、有能者の離反、実力主義の崩壊（制度的チェックで緩和）、
    /// 茶坊主の繁殖、組織の腐朽（総合）、実力主義の存否。境界とクランプを担保。
    /// </summary>
    public class FavoritismRulesTests
    {
        private static readonly FavoritismParams P = FavoritismParams.Default;
        // 配置損失1.0/士気低下1.0/離反1.0/茶坊主1.0/腐朽1.0/実力主義閾値0.5

        [Test]
        public void FavoritismDegree_TiesVersusObjectivity()
        {
            // 縁故が客観性より重ければ贔屓が深い（0.6/(0.6+0.4)=0.6）
            Assert.AreEqual(0.6f, FavoritismRules.FavoritismDegree(0.6f, 0.4f, P), 1e-4f);
            // 縁故と客観性が拮抗＝半々
            Assert.AreEqual(0.5f, FavoritismRules.FavoritismDegree(0.5f, 0.5f, P), 1e-4f);
            // 客観性ゼロ＝完全な情実
            Assert.AreEqual(1f, FavoritismRules.FavoritismDegree(1f, 0f, P), 1e-4f);
            // 両者ゼロ＝判断材料なし＝贔屓なし（ゼロ除算回避）
            Assert.AreEqual(0f, FavoritismRules.FavoritismDegree(0f, 0f, P), 1e-4f);
            // 入力クランプ（負・1超でも度合いは0..1）
            Assert.AreEqual(1f, FavoritismRules.FavoritismDegree(2f, -1f, P), 1e-4f);
        }

        [Test]
        public void MisallocationLoss_DeeperFavorWiderGap()
        {
            // 贔屓0.6×能力差0.5＝配置損失0.3
            Assert.AreEqual(0.3f, FavoritismRules.MisallocationLoss(0.6f, 0.5f, P), 1e-4f);
            // 適任者との差がなければ損なわれない（gap=0）
            Assert.AreEqual(0f, FavoritismRules.MisallocationLoss(1f, 0f, P), 1e-4f);
            // 贔屓ゼロなら適材適所（degree=0）
            Assert.AreEqual(0f, FavoritismRules.MisallocationLoss(0f, 1f, P), 1e-4f);
            // 完全情実×無能登用＝最大損失
            Assert.AreEqual(1f, FavoritismRules.MisallocationLoss(1f, 1f, P), 1e-4f);
        }

        [Test]
        public void OverlookedTalentMorale_SignedDrop()
        {
            // 贔屓0.6×実力1.0＝士気は0.6ぶん沈む（符号は負）
            Assert.AreEqual(-0.6f, FavoritismRules.OverlookedTalentMorale(0.6f, 1f, P), 1e-4f);
            // 実力ある者ほど深く失望（同じ贔屓でも実力0.5なら半分）
            Assert.AreEqual(-0.3f, FavoritismRules.OverlookedTalentMorale(0.6f, 0.5f, P), 1e-4f);
            // 贔屓がなければ士気は下がらない
            Assert.AreEqual(0f, FavoritismRules.OverlookedTalentMorale(0f, 1f, P), 1e-4f);
            // 出力は-1..1にクランプ（過大入力でも下限-1）
            Assert.AreEqual(-1f, FavoritismRules.OverlookedTalentMorale(2f, 2f, P), 1e-4f);
        }

        [Test]
        public void TalentFlight_NeedsDespairAndOpportunity()
        {
            // 士気低下0.6の大きさ×外部機会0.5＝離反0.3
            Assert.AreEqual(0.3f, FavoritismRules.TalentFlight(-0.6f, 0.5f, P), 1e-4f);
            // どれだけ失望しても受け皿がなければ去れない（機会0）
            Assert.AreEqual(0f, FavoritismRules.TalentFlight(-1f, 0f, P), 1e-4f);
            // 失望が無ければ機会があっても去らない
            Assert.AreEqual(0f, FavoritismRules.TalentFlight(0f, 1f, P), 1e-4f);
            // 深い失望×豊富な機会＝離反は最大（符号は大きさで吸収）
            Assert.AreEqual(1f, FavoritismRules.TalentFlight(-1f, 1f, P), 1e-4f);
        }

        [Test]
        public void MeritocracyErosion_InstitutionalCheckMitigates()
        {
            // 歯止めゼロなら贔屓がそのまま崩壊度
            Assert.AreEqual(0.6f, FavoritismRules.MeritocracyErosion(0.6f, 0f, P), 1e-4f);
            // 制度的チェック1.0で崩壊は半減（0.6/(1+1)=0.3）
            Assert.AreEqual(0.3f, FavoritismRules.MeritocracyErosion(0.6f, 1f, P), 1e-4f);
            // 強い歯止め（3.0）で崩壊はさらに抑制（0.8/4=0.2）
            Assert.AreEqual(0.2f, FavoritismRules.MeritocracyErosion(0.8f, 3f, P), 1e-4f);
            // 贔屓ゼロなら崩壊なし
            Assert.AreEqual(0f, FavoritismRules.MeritocracyErosion(0f, 0f, P), 1e-4f);
        }

        [Test]
        public void SycophantProliferation_ScalesWithFavoritism()
        {
            // 贔屓に応じて茶坊主が増える
            Assert.AreEqual(0.6f, FavoritismRules.SycophantProliferation(0.6f, P), 1e-4f);
            Assert.AreEqual(1f, FavoritismRules.SycophantProliferation(1f, P), 1e-4f);
            // 実力人事なら茶坊主は繁殖しない
            Assert.AreEqual(0f, FavoritismRules.SycophantProliferation(0f, P), 1e-4f);
        }

        [Test]
        public void OrganizationalRot_AccumulatesHarms()
        {
            // 配置損失0.3・離反0.3・茶坊主0.6＝1-(0.7)(0.7)(0.4)=0.804
            Assert.AreEqual(0.804f, FavoritismRules.OrganizationalRot(0.3f, 0.3f, 0.6f, P), 1e-4f);
            // どの害も無ければ腐朽なし
            Assert.AreEqual(0f, FavoritismRules.OrganizationalRot(0f, 0f, 0f, P), 1e-4f);
            // 一つでも全面化すれば腐朽は最大（離反1.0）
            Assert.AreEqual(1f, FavoritismRules.OrganizationalRot(0f, 1f, 0f, P), 1e-4f);
            // 三害が揃えば組織はほぼ機能喪失（1-(0.5)(0.5)(0.5)=0.875）
            Assert.AreEqual(0.875f, FavoritismRules.OrganizationalRot(0.5f, 0.5f, 0.5f, P), 1e-4f);
        }

        [Test]
        public void IsMeritocracyIntact_ThresholdGate()
        {
            // 既定閾値0.5：崩壊0.4は保たれている
            Assert.IsTrue(FavoritismRules.IsMeritocracyIntact(0.4f));
            // 崩壊0.6は失われている
            Assert.IsFalse(FavoritismRules.IsMeritocracyIntact(0.6f));
            // 境界（閾値ちょうど）は保たれている扱い
            Assert.IsTrue(FavoritismRules.IsMeritocracyIntact(0.5f));
            // 明示閾値版＝厳しい閾値0.2なら崩壊0.3で失われる
            Assert.IsFalse(FavoritismRules.IsMeritocracyIntact(0.3f, 0.2f));
            Assert.IsTrue(FavoritismRules.IsMeritocracyIntact(0.1f, 0.2f));
        }

        [Test]
        public void Params_CtorClampsInputs()
        {
            var p = new FavoritismParams(2f, -1f, 9f, -0.5f, 3f, 1.5f);
            Assert.AreEqual(1f, p.misallocationScale, 1e-4f);
            Assert.AreEqual(0f, p.moraleDropScale, 1e-4f);
            Assert.AreEqual(1f, p.flightScale, 1e-4f);
            Assert.AreEqual(0f, p.sycophantScale, 1e-4f);
            Assert.AreEqual(1f, p.rotScale, 1e-4f);
            Assert.AreEqual(1f, p.meritocracyThreshold, 1e-4f);
        }

        [Test]
        public void Narrative_FavoritismRotsTheMeritocracy()
        {
            // 縁故0.7・客観性0.3の情実人事＝贔屓度0.7
            float degree = FavoritismRules.FavoritismDegree(0.7f, 0.3f, P);
            Assert.AreEqual(0.7f, degree, 1e-4f);

            // 適任者と大きく離れた者（能力差0.8）を据える＝配置損失0.56
            float loss = FavoritismRules.MisallocationLoss(degree, 0.8f, P);
            Assert.AreEqual(0.56f, loss, 1e-4f);

            // 見過ごされた有能者（実力0.9）は深く失望＝士気-0.63
            float morale = FavoritismRules.OverlookedTalentMorale(degree, 0.9f, P);
            Assert.AreEqual(-0.63f, morale, 1e-4f);

            // 外部機会0.6が受け皿となり離反0.378
            float flight = FavoritismRules.TalentFlight(morale, 0.6f, P);
            Assert.AreEqual(0.378f, flight, 1e-4f);

            // 君主の周りには茶坊主が繁殖（0.7）
            float syc = FavoritismRules.SycophantProliferation(degree, P);
            Assert.AreEqual(0.7f, syc, 1e-4f);

            // 三害が累積し組織は腐朽（1-(0.44)(0.622)(0.3)=0.917896）
            float rot = FavoritismRules.OrganizationalRot(loss, flight, syc, P);
            Assert.AreEqual(0.917896f, rot, 1e-4f);

            // 制度的チェックの無い組織では実力主義は崩壊し失われる
            float erosion = FavoritismRules.MeritocracyErosion(degree, 0f, P);
            Assert.AreEqual(0.7f, erosion, 1e-4f);
            Assert.IsFalse(FavoritismRules.IsMeritocracyIntact(erosion));
        }
    }
}
