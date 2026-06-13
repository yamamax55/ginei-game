using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// パンとサーカスを固定する：ガス抜きは表出だけ抑え根は治さない（鎮痛剤）、本物の不満が
    /// 大きすぎるとサーカスでは隠せない（慰撫の限界閾値）、与え続けるほど依存が形成され
    /// 慣れは抜けにくい、依存ほど同じ娯楽では足りない（刺激の逓減）、急停止で元本＋利子が
    /// 一気に返る（切れた時に倍痛い）、飼い慣らしの政治的無関心。クランプを担保。
    /// </summary>
    public class BreadAndCircusesRulesTests
    {
        private static readonly BreadAndCircusesParams P = BreadAndCircusesParams.Default;
        // ガス抜き0.6/露出閾値0.35/依存形成0.1/依存回復0.02/刺激逓減1/反動元本0.5/反動利子1/無関心0.01

        [Test]
        public void PacificationEffect_MasksExpression_NotTheRoot()
        {
            // 供給最大×不満0.5＝0.6×1×0.5=0.3 だけ表出を抑える（根の0.5は不変＝鎮痛剤）
            Assert.AreEqual(0.3f, BreadAndCircusesRules.PacificationEffect(1f, 0.5f, P), 1e-5f);
            // 供給半分なら効果も半分
            Assert.AreEqual(0.15f, BreadAndCircusesRules.PacificationEffect(0.5f, 0.5f, P), 1e-5f);
            // 供給ゼロ＝ガス抜きなし
            Assert.AreEqual(0f, BreadAndCircusesRules.PacificationEffect(0f, 1f, P), 1e-5f);
            // 入力はクランプ（供給2・不満2でも 0.6×1×1=0.6）＝表出を負にしない
            Assert.AreEqual(0.6f, BreadAndCircusesRules.PacificationEffect(2f, 2f, P), 1e-5f);
        }

        [Test]
        public void SubstitutionFailure_CircusCannotHideRealGrievance()
        {
            // 不満0.8×供給最大＝表出0.8×0.4=0.32≤0.35 → まだ隠せる
            Assert.IsFalse(BreadAndCircusesRules.SubstitutionFailure(1f, 0.8f, P));
            // 不満0.9×供給最大＝表出0.36>0.35 → 本物の不満はサーカスでは隠せない
            Assert.IsTrue(BreadAndCircusesRules.SubstitutionFailure(1f, 0.9f, P));
            // 供給ゼロなら閾値超えの不満はそのまま漏れる
            Assert.IsTrue(BreadAndCircusesRules.SubstitutionFailure(0f, 0.6f, P));
            // 小さな不満は供給ゼロでも閾値未満＝そもそも漏れない
            Assert.IsFalse(BreadAndCircusesRules.SubstitutionFailure(0f, 0.3f, P));
        }

        [Test]
        public void DependencyTick_EntitlementForms_HabitFadesSlowly()
        {
            // 供給1×dt5＝0+(0.1×1×1−0)×5=0.5 「当然の権利」が形成される
            Assert.AreEqual(0.5f, BreadAndCircusesRules.DependencyTick(0f, 1f, 5f, P), 1e-5f);
            // 供給停止＝慣れはゆっくり抜ける：0.5−0.02×0.5×5=0.45
            Assert.AreEqual(0.45f, BreadAndCircusesRules.DependencyTick(0.5f, 0f, 5f, P), 1e-5f);
            // 形成は回復より速い（同じdtで動く量が大きい）
            float grown = BreadAndCircusesRules.DependencyTick(0.5f, 1f, 5f, P) - 0.5f;
            float faded = 0.5f - BreadAndCircusesRules.DependencyTick(0.5f, 0f, 5f, P);
            Assert.Greater(grown, faded);
            // 供給し続けても依存は均衡点 growth/(growth+decay)=0.1/0.12≈0.8333 で飽和（1には達しない）
            float d = 0f;
            for (int i = 0; i < 100; i++) d = BreadAndCircusesRules.DependencyTick(d, 1f, 1f, P);
            Assert.AreEqual(0.83333f, d, 1e-3f);
        }

        [Test]
        public void ToleranceEscalation_SameCircusIsNotEnough()
        {
            // 依存ゼロ＝素のコスト
            Assert.AreEqual(1f, BreadAndCircusesRules.ToleranceEscalation(0f, P), 1e-5f);
            // 依存半分＝1.5倍、依存最大＝2倍（同じ娯楽では足りない＝供給コスト上昇圧）
            Assert.AreEqual(1.5f, BreadAndCircusesRules.ToleranceEscalation(0.5f, P), 1e-5f);
            Assert.AreEqual(2f, BreadAndCircusesRules.ToleranceEscalation(1f, P), 1e-5f);
            // 依存はクランプ（2を渡しても1と同じ）
            Assert.AreEqual(2f, BreadAndCircusesRules.ToleranceEscalation(2f, P), 1e-5f);
        }

        [Test]
        public void WithdrawalRage_PrincipalPlusInterest()
        {
            // 依存ゼロなら何を切っても無傷
            Assert.AreEqual(0f, BreadAndCircusesRules.WithdrawalRage(0f, 1f, P), 1e-5f);
            // 緩やかな縮小（suddenness=0）でも元本は返る：1×0.5=0.5
            Assert.AreEqual(0.5f, BreadAndCircusesRules.WithdrawalRage(1f, 0f, P), 1e-5f);
            // 急停止＝元本＋利子：0.5×(0.5+1×1)=0.75
            Assert.AreEqual(0.75f, BreadAndCircusesRules.WithdrawalRage(0.5f, 1f, P), 1e-5f);
            // 依存最大×急停止＝1.5→上限1にクランプ（切れた時に倍痛い）
            Assert.AreEqual(1f, BreadAndCircusesRules.WithdrawalRage(1f, 1f, P), 1e-5f);
            // 急停止は緩停止より痛い
            Assert.Greater(BreadAndCircusesRules.WithdrawalRage(0.5f, 1f, P),
                           BreadAndCircusesRules.WithdrawalRage(0.5f, 0f, P));
            // 反動の最大(1.0)はガス抜きで抑えられる最大量(0.6)を上回る＝鎮痛剤が切れると元より痛い
            Assert.Greater(BreadAndCircusesRules.WithdrawalRage(1f, 1f, P),
                           BreadAndCircusesRules.PacificationEffect(1f, 1f, P));
        }

        [Test]
        public void PoliticalApathyEffect_TamedCitizensDoNotVoteOrRevolt()
        {
            // 供給1×期間50＝0.5 の無関心が積もる
            Assert.AreEqual(0.5f, BreadAndCircusesRules.PoliticalApathyEffect(1f, 50f, P), 1e-5f);
            // 供給半分なら半分
            Assert.AreEqual(0.25f, BreadAndCircusesRules.PoliticalApathyEffect(0.5f, 50f, P), 1e-5f);
            // 供給ゼロ＝飼い慣らされない
            Assert.AreEqual(0f, BreadAndCircusesRules.PoliticalApathyEffect(0f, 100f, P), 1e-5f);
            // 長期供給でも上限1にクランプ・負の期間は無効
            Assert.AreEqual(1f, BreadAndCircusesRules.PoliticalApathyEffect(1f, 500f, P), 1e-5f);
            Assert.AreEqual(0f, BreadAndCircusesRules.PoliticalApathyEffect(1f, -10f, P), 1e-5f);
        }
    }
}
