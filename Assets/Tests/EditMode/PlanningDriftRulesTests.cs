using NUnit.Framework;

namespace Ginei.Tests
{
    /// <summary>
    /// 計画経済ドリフト（ハイエク型・HAYK-1 #1541・隷属への道）の純ロジック検証。介入ラチェット・強制の増大・
    /// 自由の侵食・権威主義圧力・知識代替の損失・坂道・計画者のジレンマ・隷属への道判定を既定Paramsの具体値で固定する。
    /// </summary>
    public class PlanningDriftRulesTests
    {
        private const float Eps = 1e-4f;

        /// <summary>計画失敗がさらなる介入を呼ぶ＝既定ラチェット0.03×失敗1×撤回困難0.7で0.5→0.521。失敗0なら不変。</summary>
        [Test]
        public void InterventionRatchet_計画失敗が介入を累積させる()
        {
            // 0.03 * (1 * 0.7) * 1 = 0.021
            float next = PlanningDriftRules.InterventionRatchet(0.5f, 1f, 1f);
            Assert.AreEqual(0.521f, next, Eps);

            // 計画失敗0なら介入は積まれない。
            float none = PlanningDriftRules.InterventionRatchet(0.5f, 0f, 1f);
            Assert.AreEqual(0.5f, none, Eps);

            // ラチェットは一方通行＝増えこそすれ減らない。
            Assert.GreaterOrEqual(next, 0.5f);
        }

        /// <summary>計画化が進むほど強制が増す＝既定強制0.04×計画化1で0.3→0.34。計画化0なら不変。</summary>
        [Test]
        public void CoercionTick_計画化が強制を生む()
        {
            float next = PlanningDriftRules.CoercionTick(0.3f, 1f, 1f);
            Assert.AreEqual(0.34f, next, Eps);

            // 計画化0なら強制は増えない。
            float none = PlanningDriftRules.CoercionTick(0.3f, 0f, 1f);
            Assert.AreEqual(0.3f, none, Eps);
        }

        /// <summary>強制が自由を蝕む＝既定侵食0.05×強制1で0.8→0.75。強制0なら不変（隷属への道の核）。</summary>
        [Test]
        public void FreedomErosion_強制が自由を侵食する()
        {
            float next = PlanningDriftRules.FreedomErosion(0.8f, 1f, 1f);
            Assert.AreEqual(0.75f, next, Eps);

            // 強制0なら自由は減らない。
            float intact = PlanningDriftRules.FreedomErosion(0.8f, 0f, 1f);
            Assert.AreEqual(0.8f, intact, Eps);
        }

        /// <summary>計画化と強制が権威主義圧力を生む＝重み0.5ずつの加重和。計画0.8・強制0.6で0.7。両方1で1。</summary>
        [Test]
        public void AuthoritarianPressure_計画と強制が権威主義を呼ぶ()
        {
            Assert.AreEqual(0.7f, PlanningDriftRules.AuthoritarianPressure(0.8f, 0.6f), Eps);
            Assert.AreEqual(1f, PlanningDriftRules.AuthoritarianPressure(1f, 1f), Eps);
            Assert.AreEqual(0f, PlanningDriftRules.AuthoritarianPressure(0f, 0f), Eps);
        }

        /// <summary>計画化が分散知識を置き換えそこねる効率損失＝計画化に比例し最大0.6。計画1で0.6・計画0.5で0.3・計画0で0。</summary>
        [Test]
        public void KnowledgeSubstitution_計画化が知識を失い効率を落とす()
        {
            Assert.AreEqual(0.6f, PlanningDriftRules.KnowledgeSubstitution(1f), Eps);
            Assert.AreEqual(0.3f, PlanningDriftRules.KnowledgeSubstitution(0.5f), Eps);
            Assert.AreEqual(0f, PlanningDriftRules.KnowledgeSubstitution(0f), Eps);
        }

        /// <summary>計画化が臨界を超えると統制が加速＝閾値0.5以下は1倍、超えると1〜3倍へ。計画1で坂道倍率3に到達。</summary>
        [Test]
        public void SlipperySlope_臨界を超えると統制が加速する()
        {
            // 閾値以下は普通の進行（1倍）。
            Assert.AreEqual(1f, PlanningDriftRules.SlipperySlope(0.4f, 0.5f), Eps);
            Assert.AreEqual(1f, PlanningDriftRules.SlipperySlope(0.5f, 0.5f), Eps);

            // 計画1.0は超過分が満タン＝坂道倍率3に到達。
            Assert.AreEqual(3f, PlanningDriftRules.SlipperySlope(1f, 0.5f), Eps);

            // 中間（0.75）は1と3の中間＝2。
            Assert.AreEqual(2f, PlanningDriftRules.SlipperySlope(0.75f, 0.5f), Eps);
        }

        /// <summary>計画失敗は撤回より統制へ傾く＝失敗1×撤回困難0.7=0.7>0.5でさらなる統制。撤回肢が無ければ統制一択。</summary>
        [Test]
        public void PlannersDilemma_失敗は撤回より統制へ傾く()
        {
            var both = new[] { PlannerChoice.撤回, PlannerChoice.さらなる統制 };

            // 大きな失敗＝統制傾斜0.7>0.5でさらなる統制。
            Assert.AreEqual(PlannerChoice.さらなる統制, PlanningDriftRules.PlannersDilemma(1f, both));

            // 小さな失敗（0.5×0.7=0.35≤0.5）なら撤回も選べる。
            Assert.AreEqual(PlannerChoice.撤回, PlanningDriftRules.PlannersDilemma(0.5f, both));

            // 撤回肢が無ければ統制しかない。
            var onlyControl = new[] { PlannerChoice.さらなる統制 };
            Assert.AreEqual(PlannerChoice.さらなる統制, PlanningDriftRules.PlannersDilemma(0.1f, onlyControl));
        }

        /// <summary>権威主義圧力が閾値超かつ自由が(1−閾値)未満なら隷属への道に入る＝計画の累積が自由を蝕み権威主義へ。</summary>
        [Test]
        public void IsRoadToSerfdom_権威主義化に入った判定()
        {
            // 圧力0.8>0.6 かつ 自由0.3<0.4 ＝隷属への道。
            Assert.IsTrue(PlanningDriftRules.IsRoadToSerfdom(0.8f, 0.3f, 0.6f));

            // 圧力は高いが自由がまだ十分（0.5≥0.4）＝未到達。
            Assert.IsFalse(PlanningDriftRules.IsRoadToSerfdom(0.8f, 0.5f, 0.6f));

            // 自由は乏しいが圧力が低い（0.5≤0.6）＝未到達。
            Assert.IsFalse(PlanningDriftRules.IsRoadToSerfdom(0.5f, 0.3f, 0.6f));
        }
    }
}
