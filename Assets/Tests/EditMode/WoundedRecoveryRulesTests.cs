using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>負傷からの回復（療養と戦列復帰）の純ロジックテスト。既定Paramsの具体値で期待値を固定。</summary>
    public class WoundedRecoveryRulesTests
    {
        private const float Eps = 1e-4f;

        /// <summary>重傷ほど治癒が長く、治療の質が期間を縮める。</summary>
        [Test]
        public void HealingTime_重症度と治療の質で治癒期間が決まる()
        {
            // 既定: severityTimeScale=1, treatmentTimeEffect=1
            // sev=0.8, quality=0.5 → 0.8/(1+0.5) = 0.5333...
            Assert.AreEqual(0.8f / 1.5f, WoundedRecoveryRules.HealingTime(0.8f, 0.5f), Eps);
            // 治療が手厚いほど短い（質1.0 → 0.8/2 = 0.4）
            Assert.AreEqual(0.4f, WoundedRecoveryRules.HealingTime(0.8f, 1f), Eps);
            // 軽傷は速い
            Assert.Less(WoundedRecoveryRules.HealingTime(0.2f, 0.5f), WoundedRecoveryRules.HealingTime(0.8f, 0.5f));
        }

        /// <summary>施設と療養環境が回復環境を決める。野戦でも床は残る。</summary>
        [Test]
        public void RecoveryEnvironment_施設と療養で環境が決まり床が残る()
        {
            // 既定: facilityWeight=0.6, restWeight=0.4 → 共に1.0で 1.0
            Assert.AreEqual(1f, WoundedRecoveryRules.RecoveryEnvironment(1f, 1f), Eps);
            // 施設のみ良い → 0.6
            Assert.AreEqual(0.6f, WoundedRecoveryRules.RecoveryEnvironment(1f, 0f), Eps);
            // 何もなくても床(0.2)は残る
            Assert.AreEqual(0.2f, WoundedRecoveryRules.RecoveryEnvironment(0f, 0f), Eps);
        }

        /// <summary>治癒の進捗は経過時間×環境÷期間。良い環境ほど早く完治する。</summary>
        [Test]
        public void HealingProgress_経過と環境で治癒が進む()
        {
            // healingTime=0.5, env=1.0, elapsed=0.25 → 0.25/0.5 = 0.5
            Assert.AreEqual(0.5f, WoundedRecoveryRules.HealingProgress(0.5f, 1f, 0.25f), Eps);
            // 十分な経過で完治(1にクランプ)
            Assert.AreEqual(1f, WoundedRecoveryRules.HealingProgress(0.5f, 1f, 1f), Eps);
            // 環境が悪いと進みが遅い（env=0.5 → 0.25*0.5/0.5 = 0.25）
            Assert.AreEqual(0.25f, WoundedRecoveryRules.HealingProgress(0.5f, 0.5f, 0.25f), Eps);
            // 期間0は即完治
            Assert.AreEqual(1f, WoundedRecoveryRules.HealingProgress(0f, 1f, 0f), Eps);
        }

        /// <summary>後遺症は重症度×(1−治療の質)。手厚い治療は後遺症を減らす。</summary>
        [Test]
        public void PermanentDisability_粗末な治療ほど後遺症が残る()
        {
            // sev=0.8, quality=0.25 → 0.8*0.75 = 0.6
            Assert.AreEqual(0.6f, WoundedRecoveryRules.PermanentDisability(0.8f, 0.25f), Eps);
            // 完璧な治療は後遺症ゼロ
            Assert.AreEqual(0f, WoundedRecoveryRules.PermanentDisability(0.8f, 1f), Eps);
            // 軽傷は後遺症が小さい
            Assert.Less(WoundedRecoveryRules.PermanentDisability(0.2f, 0.25f),
                        WoundedRecoveryRules.PermanentDisability(0.8f, 0.25f));
        }

        /// <summary>復帰適性は治癒−後遺症ペナルティ。後遺症が重いと適性が下がる。</summary>
        [Test]
        public void ReturnToDutyFitness_治癒から後遺症を引く()
        {
            // 既定 disabilityPenalty=0.5
            // progress=0.8, disability=0.6 → 0.8 - 0.6*0.5 = 0.5
            Assert.AreEqual(0.5f, WoundedRecoveryRules.ReturnToDutyFitness(0.8f, 0.6f), Eps);
            // 後遺症なしなら治癒そのまま
            Assert.AreEqual(0.8f, WoundedRecoveryRules.ReturnToDutyFitness(0.8f, 0f), Eps);
            // 治っても後遺症が重ければ下がる
            Assert.Less(WoundedRecoveryRules.ReturnToDutyFitness(1f, 1f),
                        WoundedRecoveryRules.ReturnToDutyFitness(1f, 0f));
        }

        /// <summary>復帰後の能力は適性×元の能力。後遺症ぶん目減りする（実効値）。</summary>
        [Test]
        public void PostRecoveryCapability_適性ぶん能力が目減りする()
        {
            // fitness=0.5, original=80 → 40
            Assert.AreEqual(40f, WoundedRecoveryRules.PostRecoveryCapability(0.5f, 80f), Eps);
            // 完全復帰なら無減衰
            Assert.AreEqual(80f, WoundedRecoveryRules.PostRecoveryCapability(1f, 80f), Eps);
            // 適性ゼロなら戦力0
            Assert.AreEqual(0f, WoundedRecoveryRules.PostRecoveryCapability(0f, 80f), Eps);
        }

        /// <summary>長期療養者は後遺症×(1−治癒)。治らぬ重い後遺症ほど戦力外。</summary>
        [Test]
        public void LongTermInvalid_治らぬ後遺症ほど戦力外()
        {
            // disability=0.6, progress=0.2 → 0.6*0.8 = 0.48
            Assert.AreEqual(0.48f, WoundedRecoveryRules.LongTermInvalid(0.6f, 0.2f), Eps);
            // 完治すれば戦力外でない
            Assert.AreEqual(0f, WoundedRecoveryRules.LongTermInvalid(0.6f, 1f), Eps);
            // 後遺症がなければ戦力外でない
            Assert.AreEqual(0f, WoundedRecoveryRules.LongTermInvalid(0f, 0f), Eps);
        }

        /// <summary>復帰可否は適性が閾値以上か。リハビリで適性が伸びれば前線へ戻せる。</summary>
        [Test]
        public void IsFitForDuty_閾値で復帰可否を判定()
        {
            // 既定 fitnessThreshold=0.5
            Assert.IsTrue(WoundedRecoveryRules.IsFitForDuty(0.5f));
            Assert.IsTrue(WoundedRecoveryRules.IsFitForDuty(0.7f));
            Assert.IsFalse(WoundedRecoveryRules.IsFitForDuty(0.49f));
            // 明示閾値オーバーロード
            Assert.IsTrue(WoundedRecoveryRules.IsFitForDuty(0.3f, 0.3f));
            Assert.IsFalse(WoundedRecoveryRules.IsFitForDuty(0.2f, 0.3f));
        }

        /// <summary>
        /// 物語：重傷の士官が前線で粗末な手当てを受け、後送されて療養するまで。
        /// 野戦の応急処置では治らず後遺症が残り戦力外だが、後方の良い病院で療養が進むと
        /// 戦列に復帰できる。ただし後遺症ぶん能力は元に戻らない。
        /// </summary>
        [Test]
        public void Narrative_重傷士官が後送療養を経て戦列復帰する()
        {
            float severity = 0.8f; // 重傷

            // 前線：応急処置のみ（治療の質も施設も乏しい）
            float frontTreatment = 0.2f;
            float frontTime = WoundedRecoveryRules.HealingTime(severity, frontTreatment);
            float frontEnv = WoundedRecoveryRules.RecoveryEnvironment(0.1f, 0.1f); // 床0.2に近い
            float frontProgress = WoundedRecoveryRules.HealingProgress(frontTime, frontEnv, 0.2f);
            float frontDisability = WoundedRecoveryRules.PermanentDisability(severity, frontTreatment);
            float frontFitness = WoundedRecoveryRules.ReturnToDutyFitness(frontProgress, frontDisability);
            // 前線では復帰できず、長期療養者（戦力外）の度合いが高い
            Assert.IsFalse(WoundedRecoveryRules.IsFitForDuty(frontFitness));
            float frontInvalid = WoundedRecoveryRules.LongTermInvalid(frontDisability, frontProgress);
            Assert.Greater(frontInvalid, 0.3f);

            // 後送：質の高い病院で十分な治療と療養（リハビリ）
            float rearTreatment = 0.9f;
            float rearTime = WoundedRecoveryRules.HealingTime(severity, rearTreatment);
            float rearEnv = WoundedRecoveryRules.RecoveryEnvironment(0.9f, 0.9f);
            float rearProgress = WoundedRecoveryRules.HealingProgress(rearTime, rearEnv, 1f); // 時間をかけて完治
            float rearDisability = WoundedRecoveryRules.PermanentDisability(severity, rearTreatment);
            float rearFitness = WoundedRecoveryRules.ReturnToDutyFitness(rearProgress, rearDisability);
            // 良い治療は後遺症を減らし、療養の進捗で復帰適性が上がる
            Assert.Less(rearDisability, frontDisability);
            Assert.Greater(rearFitness, frontFitness);
            Assert.IsTrue(WoundedRecoveryRules.IsFitForDuty(rearFitness));

            // 復帰後の能力は後遺症ぶん元（統率80）に届かない
            float restored = WoundedRecoveryRules.PostRecoveryCapability(rearFitness, 80f);
            Assert.Less(restored, 80f);
            Assert.Greater(restored, 0f);
        }
    }
}
