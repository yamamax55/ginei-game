using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>WarCrimesRules（責任連鎖・個人有責性＝#1536）の純ロジックテスト。既定Params具体値で固定。</summary>
    public class WarCrimesRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>個人有責性＝階層×裁量×関与の重み付き和（既定 0.4/0.3/0.3）。全1で1.0、階層のみ1で0.4。</summary>
        [Test]
        public void IndividualCulpability_重み付き和()
        {
            Assert.AreEqual(1.0f, WarCrimesRules.IndividualCulpability(1f, 1f, 1f), Eps);
            Assert.AreEqual(0.5f, WarCrimesRules.IndividualCulpability(0.5f, 0.5f, 0.5f), Eps);
            // 上官だが裁量・関与ゼロ＝地位ぶんの0.4のみ。
            Assert.AreEqual(0.4f, WarCrimesRules.IndividualCulpability(1f, 0f, 0f), Eps);
            // 末端兵だが自由意思で全力関与＝裁量0.3＋関与0.3＝0.6。
            Assert.AreEqual(0.6f, WarCrimesRules.IndividualCulpability(0f, 1f, 1f), Eps);
        }

        /// <summary>「命令に従っただけ」抗弁＝裁量が閾値(0.3)以下のときだけ一部成立。裁量があれば不成立。</summary>
        [Test]
        public void CanClaimObedience_裁量がなければ一部成立()
        {
            Assert.IsTrue(WarCrimesRules.CanClaimObedience(0.1f));   // 強制された＝成立
            Assert.IsTrue(WarCrimesRules.CanClaimObedience(0.3f));   // 閾値ちょうど＝成立
            Assert.IsFalse(WarCrimesRules.CanClaimObedience(0.5f));  // 裁量があった＝免責されない
        }

        /// <summary>上官責任＝指揮階層×知り得た度。知り得たのに止めなかった指揮官は手を下さずとも責任。</summary>
        [Test]
        public void SuperiorResponsibility_知り得たのに止めなかった責任()
        {
            Assert.AreEqual(0.8f, WarCrimesRules.SuperiorResponsibility(1f, 0.8f), Eps);
            // 知り得なかった（情報遮断）なら上官責任は問えない。
            Assert.AreEqual(0f, WarCrimesRules.SuperiorResponsibility(1f, 0f), Eps);
        }

        /// <summary>命令者の責任＝指揮階層×命令の違法度。命令者は連鎖の頂点＝最も重い。</summary>
        [Test]
        public void OrderGiverCulpability_違法命令を下した頂点()
        {
            Assert.AreEqual(0.9f, WarCrimesRules.OrderGiverCulpability(1f, 0.9f), Eps);
            // 合法命令なら違法度0＝有責性なし。
            Assert.AreEqual(0f, WarCrimesRules.OrderGiverCulpability(1f, 0f), Eps);
        }

        /// <summary>傍観者の共犯性＝黙認分(1−関与)×認識×止める力。力がありながら知って黙認するほど重い。</summary>
        [Test]
        public void BystanderComplicity_止める力ある黙認()
        {
            // 無関与(0)・認識1・力1＝完全な黙認共犯。
            Assert.AreEqual(1.0f, WarCrimesRules.BystanderComplicity(0f, 1f, 1f), Eps);
            // 止める力がなければ共犯性なし。
            Assert.AreEqual(0f, WarCrimesRules.BystanderComplicity(0f, 1f, 0f), Eps);
            // 自ら全力関与なら黙認分が消える（傍観者ではない）。
            Assert.AreEqual(0f, WarCrimesRules.BystanderComplicity(1f, 1f, 1f), Eps);
        }

        /// <summary>判決＝有責性×証拠スコアで区分（既定 有罪0.4・極刑0.75・減刑0.2）。証拠が弱ければ有責でも無罪。</summary>
        [Test]
        public void TrialVerdict_有責性と証拠で判決()
        {
            // 有責1×証拠1＝1.0≥0.75＝極刑。
            Assert.AreEqual(TrialOutcome.極刑, WarCrimesRules.TrialVerdict(1f, 1f));
            // 0.6×0.8＝0.48≥0.4＝有罪。
            Assert.AreEqual(TrialOutcome.有罪, WarCrimesRules.TrialVerdict(0.6f, 0.8f));
            // 0.5×0.5＝0.25≥0.2＝減刑。
            Assert.AreEqual(TrialOutcome.減刑, WarCrimesRules.TrialVerdict(0.5f, 0.5f));
            // 有責1でも証拠0.1＝0.1<0.2＝無罪（疑わしきは罰せず）。
            Assert.AreEqual(TrialOutcome.無罪, WarCrimesRules.TrialVerdict(1f, 0.1f));
        }

        /// <summary>強要下の減刑＝最大0.5割引するが良心の床(0.1)は残る＝脅されてもゼロにはならない。</summary>
        [Test]
        public void MitigationFromDuress_減刑するがゼロにはしない()
        {
            // 有責0.8・強要なし＝据え置き。
            Assert.AreEqual(0.8f, WarCrimesRules.MitigationFromDuress(0.8f, 0f), Eps);
            // 有責0.8・強要1＝0.8×(1−0.5)＝0.4。
            Assert.AreEqual(0.4f, WarCrimesRules.MitigationFromDuress(0.8f, 1f), Eps);
            // 有責0.15・強要1＝0.075だが床0.1で下げ止まる（最終的な良心）。
            Assert.AreEqual(0.1f, WarCrimesRules.MitigationFromDuress(0.15f, 1f), Eps);
        }

        /// <summary>明白に違法な命令＝違法度が閾値(0.7)以上で従う義務なし＝抗弁不成立。</summary>
        [Test]
        public void IsManifestlyIllegal_明白な違法命令()
        {
            Assert.IsTrue(WarCrimesRules.IsManifestlyIllegal(0.9f));   // 誰が見ても違法＝従う義務なし
            Assert.IsTrue(WarCrimesRules.IsManifestlyIllegal(0.7f));   // 閾値ちょうど
            Assert.IsFalse(WarCrimesRules.IsManifestlyIllegal(0.4f));  // 微妙＝抗弁の余地あり
        }

        /// <summary>責任連鎖 struct からの個人有責性糖衣が直接呼びと一致。</summary>
        [Test]
        public void AccountabilityChain_糖衣が一致()
        {
            var chain = new AccountabilityChain(1f, 0.5f, 0.5f);
            Assert.AreEqual(
                WarCrimesRules.IndividualCulpability(1f, 0.5f, 0.5f),
                WarCrimesRules.IndividualCulpability(chain), Eps);
            // クランプ確認（範囲外入力が0..1に収まる）。
            var clamped = new AccountabilityChain(2f, -1f, 5f);
            Assert.AreEqual(1f, clamped.commandRank, Eps);
            Assert.AreEqual(0f, clamped.discretion, Eps);
            Assert.AreEqual(1f, clamped.participation, Eps);
        }
    }
}
