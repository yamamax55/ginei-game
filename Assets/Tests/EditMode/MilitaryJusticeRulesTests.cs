using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// MilitaryJusticeRules（軍事司法＝懲罰体制・STSH-2 #2357）の純ロジックテスト。
    /// 抑止（脱走リスク減）・結束（倍率増）・反感（苛烈な公開処刑で増）・最適苛烈さの谷・null 安全。
    /// </summary>
    public class MilitaryJusticeRulesTests
    {
        const float Eps = 1e-4f;

        static PunishmentRegime R(float sev, float enf, float cov, float pub)
            => new PunishmentRegime(sev, enf, cov, pub);

        // --- 構築・クランプ ---
        [Test]
        public void PunishmentRegime_コンストラクタは0..1にクランプする()
        {
            var r = new PunishmentRegime(2f, -1f, 0.5f, 5f);
            Assert.AreEqual(1f, r.severity, Eps);
            Assert.AreEqual(0f, r.enforcement, Eps);
            Assert.AreEqual(0.5f, r.coverageScope, Eps);
            Assert.AreEqual(1f, r.publicVisibility, Eps);
        }

        // --- 脱走リスク：苛烈さ×執行で減る、0..1 ---
        [Test]
        public void DeserterRisk_苛烈さ執行が高いほど減る()
        {
            float low = MilitaryJusticeRules.DeserterRisk(R(0.1f, 0.1f, 0.5f, 0.5f));
            float mid = MilitaryJusticeRules.DeserterRisk(R(0.5f, 0.5f, 0.5f, 0.5f));
            float high = MilitaryJusticeRules.DeserterRisk(R(0.9f, 0.9f, 0.5f, 0.5f));
            Assert.Greater(low, mid);
            Assert.Greater(mid, high);
        }

        [Test]
        public void DeserterRisk_執行ゼロなら苛烈さは抑止に効かない()
        {
            // 執行が無ければ苛烈さを上げても抑止されず中立値のまま。
            float a = MilitaryJusticeRules.DeserterRisk(R(0.2f, 0f, 0.5f, 0.5f));
            float b = MilitaryJusticeRules.DeserterRisk(R(0.9f, 0f, 0.5f, 0.5f));
            Assert.AreEqual(a, b, Eps);
            Assert.AreEqual(MilitaryJusticeRules.NeutralDeserterRisk, a, Eps);
        }

        [Test]
        public void DeserterRisk_境界は0..1に収まる()
        {
            float max = MilitaryJusticeRules.DeserterRisk(R(1f, 1f, 1f, 1f));
            float min = MilitaryJusticeRules.DeserterRisk(R(0f, 0f, 0f, 0f));
            Assert.GreaterOrEqual(max, 0f);
            Assert.LessOrEqual(min, 1f);
            Assert.GreaterOrEqual(max, MilitaryJusticeRules.MinDeserterRisk);
        }

        [Test]
        public void DeserterRisk_null は中立値()
        {
            Assert.AreEqual(MilitaryJusticeRules.NeutralDeserterRisk,
                MilitaryJusticeRules.DeserterRisk(null), Eps);
        }

        // --- 結束ボーナス：執行×適用範囲で増える、>=1 ---
        [Test]
        public void CohesionBonus_執行と適用範囲が高いほど上がる()
        {
            float low = MilitaryJusticeRules.CohesionBonus(R(0.5f, 0.1f, 0.1f, 0.5f));
            float high = MilitaryJusticeRules.CohesionBonus(R(0.5f, 0.9f, 0.9f, 0.5f));
            Assert.Greater(high, low);
        }

        [Test]
        public void CohesionBonus_常に1以上で上限内()
        {
            Assert.AreEqual(1f, MilitaryJusticeRules.CohesionBonus(R(1f, 0f, 1f, 1f)), Eps);
            float full = MilitaryJusticeRules.CohesionBonus(R(1f, 1f, 1f, 1f));
            Assert.AreEqual(1f + MilitaryJusticeRules.MaxCohesionBonus, full, Eps);
            Assert.GreaterOrEqual(full, 1f);
        }

        [Test]
        public void CohesionBonus_null は1()
        {
            Assert.AreEqual(1f, MilitaryJusticeRules.CohesionBonus(null), Eps);
        }

        // --- 反感蓄積：苛烈さ×公開性で増える、0..1 ---
        [Test]
        public void ResentmentAccrual_苛烈さ公開性が高いほど増える()
        {
            float low = MilitaryJusticeRules.ResentmentAccrual(R(0.1f, 0.5f, 0.5f, 0.1f));
            float high = MilitaryJusticeRules.ResentmentAccrual(R(0.9f, 0.5f, 0.5f, 0.9f));
            Assert.Greater(high, low);
        }

        [Test]
        public void ResentmentAccrual_公開性ゼロなら反感なし()
        {
            // 内々の処分は反感を生まない（公開性=0）。
            Assert.AreEqual(0f, MilitaryJusticeRules.ResentmentAccrual(R(1f, 1f, 1f, 0f)), Eps);
        }

        [Test]
        public void ResentmentAccrual_境界は0..1に収まる()
        {
            float max = MilitaryJusticeRules.ResentmentAccrual(R(1f, 1f, 1f, 1f));
            float min = MilitaryJusticeRules.ResentmentAccrual(R(0f, 0f, 0f, 0f));
            Assert.LessOrEqual(max, 1f);
            Assert.GreaterOrEqual(min, 0f);
            Assert.AreEqual(1f, max, Eps);
        }

        [Test]
        public void ResentmentAccrual_null は0()
        {
            Assert.AreEqual(0f, MilitaryJusticeRules.ResentmentAccrual(null), Eps);
        }

        // --- 最適苛烈さ：0..1 かつ真の最小 ---
        [Test]
        public void OptimalSeverity_常に0..1に収まる()
        {
            for (float enf = 0f; enf <= 1f; enf += 0.25f)
            for (float pub = 0f; pub <= 1f; pub += 0.25f)
            {
                float opt = MilitaryJusticeRules.OptimalSeverity(enf, pub);
                Assert.GreaterOrEqual(opt, 0f);
                Assert.LessOrEqual(opt, 1f);
            }
        }

        [Test]
        public void OptimalSeverity_は真の最小で両端以下のコスト()
        {
            float enf = 0.8f, pub = 0.5f;
            float opt = MilitaryJusticeRules.OptimalSeverity(enf, pub);
            float costOpt = MilitaryJusticeRules.CombinedCost(opt, enf, pub);
            float costMin = MilitaryJusticeRules.CombinedCost(0f, enf, pub);
            float costMax = MilitaryJusticeRules.CombinedCost(1f, enf, pub);
            Assert.LessOrEqual(costOpt, costMin + Eps);
            Assert.LessOrEqual(costOpt, costMax + Eps);
        }

        [Test]
        public void OptimalSeverity_は内点の真の谷()
        {
            // 凹（抑止 √s）＋凸（反感 s²）で内点に谷ができる構成（端ではない最適）。
            float enf = 0.8f, pub = 0.5f;
            float opt = MilitaryJusticeRules.OptimalSeverity(enf, pub);
            Assert.Greater(opt, 0f);
            Assert.Less(opt, 1f);
        }

        [Test]
        public void OptimalSeverity_公開性が高いほど苛烈さを抑える()
        {
            // 反感が高くつく（公開性大）ほど最適苛烈さは下がる（恨みを避ける）。
            float enf = 0.8f;
            float lowPub = MilitaryJusticeRules.OptimalSeverity(enf, 0.2f);
            float highPub = MilitaryJusticeRules.OptimalSeverity(enf, 0.9f);
            Assert.Greater(lowPub, highPub);
        }

        [Test]
        public void OptimalSeverity_執行が高いほど苛烈さを掛ける価値がある()
        {
            // 執行が確実なほど苛烈さの抑止が立ちやすい＝最適苛烈さは高まる（無駄打ちにならない）。
            float pub = 0.6f;
            float lowEnf = MilitaryJusticeRules.OptimalSeverity(0.3f, pub);
            float highEnf = MilitaryJusticeRules.OptimalSeverity(1f, pub);
            Assert.Greater(highEnf, lowEnf);
            // いずれも真の谷であることを担保。
            float c = MilitaryJusticeRules.CombinedCost(highEnf, 1f, pub);
            Assert.LessOrEqual(c, MilitaryJusticeRules.CombinedCost(0f, 1f, pub) + Eps);
            Assert.LessOrEqual(c, MilitaryJusticeRules.CombinedCost(1f, 1f, pub) + Eps);
        }
    }
}
