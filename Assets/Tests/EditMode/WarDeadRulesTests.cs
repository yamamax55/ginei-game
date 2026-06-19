using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 戦没者の弔いと社会への影響を固定する：戦死者比が重みを生み（人口0は計上しない）、補償は水準×死者比、
    /// 弔いは慰霊&gt;顕彰の合成、英霊の物語は弔い×意味づけ（片方0で消える）、政治利用は物語×思惑、
    /// 累積厭戦は重み×長さ、記憶は弔い×世代継承、社会結束は物語−厭戦（団結か厭戦か・最後は厭戦へ傾く）。
    /// 既定Params期待値を手計算で固定し、入力クランプを担保。
    /// </summary>
    public class WarDeadRulesTests
    {
        private static readonly WarDeadParams P = WarDeadParams.Default;
        // 戦死スケール50/補償スケール1.0/慰霊重み0.6/顕彰重み0.4/政治化1.0/厭戦0.5/結束物語0.8/結束厭戦1.0

        [Test]
        public void CasualtyWeight_RatioScaledAndPopulationGuard()
        {
            // 死者100/人口100000=0.001、×50=0.05
            Assert.AreEqual(0.05f, WarDeadRules.CasualtyWeight(100f, 100000f, P), 1e-4f);
            // 比2%×50=1.0で飽和（クランプ上限）
            Assert.AreEqual(1f, WarDeadRules.CasualtyWeight(2000f, 100000f, P), 1e-4f);
            // 人口0以下は計上しない（母数不明）
            Assert.AreEqual(0f, WarDeadRules.CasualtyWeight(100f, 0f, P), 1e-4f);
            // 負の死者は0扱い
            Assert.AreEqual(0f, WarDeadRules.CasualtyWeight(-100f, 100000f, P), 1e-4f);
        }

        [Test]
        public void BereavedCompensation_LevelTimesDeathRatio()
        {
            // 補償0.5×死者比0.01(=1000/100000)×1.0=0.005
            Assert.AreEqual(0.005f, WarDeadRules.BereavedCompensation(0.5f, 1000f, 100000f, P), 1e-4f);
            // 満額補償×死者比0.02=0.02
            Assert.AreEqual(0.02f, WarDeadRules.BereavedCompensation(1f, 2000f, 100000f, P), 1e-4f);
            // 補償水準0＝負担なし
            Assert.AreEqual(0f, WarDeadRules.BereavedCompensation(0f, 2000f, 100000f, P), 1e-4f);
            // 人口0は0
            Assert.AreEqual(0f, WarDeadRules.BereavedCompensation(1f, 1000f, 0f, P), 1e-4f);
        }

        [Test]
        public void Commemoration_EffortWeighsMoreThanHonor()
        {
            // 慰霊のみ満額＝0.6、顕彰のみ満額＝0.4（弔いの本体は努力）
            Assert.AreEqual(0.6f, WarDeadRules.Commemoration(1f, 0f, P), 1e-4f);
            Assert.AreEqual(0.4f, WarDeadRules.Commemoration(0f, 1f, P), 1e-4f);
            Assert.Greater(WarDeadRules.Commemoration(1f, 0f, P), WarDeadRules.Commemoration(0f, 1f, P));
            // 両取り＝満額1、範囲外はクランプ
            Assert.AreEqual(1f, WarDeadRules.Commemoration(1f, 1f, P), 1e-4f);
            Assert.AreEqual(0f, WarDeadRules.Commemoration(-1f, -1f, P), 1e-4f);
        }

        [Test]
        public void HeroicNarrative_NeedsBothCommemorationAndMeaning()
        {
            // 弔い0.8×意味づけ0.5=0.4
            Assert.AreEqual(0.4f, WarDeadRules.HeroicNarrative(0.8f, 0.5f, P), 1e-4f);
            // 意味づけが無ければ（0）どれだけ弔っても物語にならない
            Assert.AreEqual(0f, WarDeadRules.HeroicNarrative(1f, 0f, P), 1e-4f);
            // 弔いが無ければ（0）意味だけでは物語にならない
            Assert.AreEqual(0f, WarDeadRules.HeroicNarrative(0f, 1f, P), 1e-4f);
        }

        [Test]
        public void PoliticalUseOfDead_NarrativeTimesAgenda()
        {
            // 物語0.6×思惑0.5×1.0=0.3
            Assert.AreEqual(0.3f, WarDeadRules.PoliticalUseOfDead(0.6f, 0.5f, P), 1e-4f);
            // 政権が利用を望まなければ（思惑0）政治利用されない
            Assert.AreEqual(0f, WarDeadRules.PoliticalUseOfDead(1f, 0f, P), 1e-4f);
            // 強い物語×強い思惑＝最大の政治利用
            Assert.AreEqual(1f, WarDeadRules.PoliticalUseOfDead(1f, 1f, P), 1e-4f);
        }

        [Test]
        public void AccumulatedWarWeariness_WeightTimesDuration()
        {
            // 重み0.8×長さ1.0×0.5=0.4
            Assert.AreEqual(0.4f, WarDeadRules.AccumulatedWarWeariness(0.8f, 1f, P), 1e-4f);
            // 開戦直後（長さ0）＝厭戦なし
            Assert.AreEqual(0f, WarDeadRules.AccumulatedWarWeariness(1f, 0f, P), 1e-4f);
            // 長引くほど厭戦は増える（長さに単調増加）
            Assert.Greater(WarDeadRules.AccumulatedWarWeariness(0.8f, 1f, P),
                           WarDeadRules.AccumulatedWarWeariness(0.8f, 0.5f, P));
        }

        [Test]
        public void CollectiveMemory_NeedsTransmission()
        {
            // 弔い0.6×世代継承0.5=0.3
            Assert.AreEqual(0.3f, WarDeadRules.CollectiveMemory(0.6f, 0.5f, P), 1e-4f);
            // 語り継がれなければ（継承0）記憶は残らない
            Assert.AreEqual(0f, WarDeadRules.CollectiveMemory(1f, 0f, P), 1e-4f);
            // 両満額＝記憶として残る
            Assert.AreEqual(1f, WarDeadRules.CollectiveMemory(1f, 1f, P), 1e-4f);
        }

        [Test]
        public void SocialCohesionImpact_UnityOrWeariness()
        {
            // 物語だけ＝団結（正）：0.7×0.8 − 0×1.0 = 0.56
            Assert.AreEqual(0.56f, WarDeadRules.SocialCohesionImpact(0.7f, 0f, P), 1e-4f);
            // 厭戦だけ＝分断（負）：0×0.8 − 0.7×1.0 = -0.7
            Assert.AreEqual(-0.7f, WarDeadRules.SocialCohesionImpact(0f, 0.7f, P), 1e-4f);
            // 出力は -1..1 にクランプ（最大の厭戦）
            Assert.AreEqual(-1f, WarDeadRules.SocialCohesionImpact(0f, 1f, P), 1e-4f);
        }

        [Test]
        public void Narrative_DeathTipsTowardWearinessInTheEnd()
        {
            // 物語：手厚い弔い（慰霊0.9/顕彰0.8）×強い意味づけ0.9
            float comm = WarDeadRules.Commemoration(0.9f, 0.8f, P);            // 0.54+0.32=0.86
            float narrative = WarDeadRules.HeroicNarrative(comm, 0.9f, P);     // 0.774

            // 戦争初期：戦死は軽く（比0.2%＝重み0.1）短い（長さ0.2）→厭戦わずか→物語が勝つ＝団結
            float weightEarly = WarDeadRules.CasualtyWeight(200f, 100000f, P); // 0.1
            float wearyEarly = WarDeadRules.AccumulatedWarWeariness(weightEarly, 0.2f, P); // 0.01
            float cohesionEarly = WarDeadRules.SocialCohesionImpact(narrative, wearyEarly, P);
            Assert.Greater(cohesionEarly, 0f); // 序盤は英霊の物語で団結

            // 戦争末期：戦死が積もり（重み飽和1.0）長期化（長さ1.0）→厭戦最大→物語を上回る＝分断
            float weightLate = WarDeadRules.CasualtyWeight(5000f, 100000f, P); // 1.0
            float wearyLate = WarDeadRules.AccumulatedWarWeariness(weightLate, 1f, P); // 0.5
            float cohesionLate = WarDeadRules.SocialCohesionImpact(narrative, wearyLate, P);
            // 同じ物語でも積もる死で結束は下がる＝死は最後に厭戦へ傾く
            Assert.Less(cohesionLate, cohesionEarly);
        }
    }
}
