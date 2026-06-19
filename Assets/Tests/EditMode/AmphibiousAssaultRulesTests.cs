using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>強襲揚陸（軌道→地上）の純ロジック EditMode テスト。既定 Params で期待値を固定。</summary>
    public class AmphibiousAssaultRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void 降下戦力_まとまりで実効戦力が決まる()
        {
            // Lerp(0.4, 1, 0.5) = 0.7 → 1000 * 0.7 = 700
            Assert.AreEqual(700f, AmphibiousAssaultRules.LandingForce(1000f, 0.5f), Eps);
            // まとまり満額なら全部残る、ゼロでも最低残存ぶん残る
            Assert.AreEqual(1000f, AmphibiousAssaultRules.LandingForce(1000f, 1f), Eps);
            Assert.AreEqual(400f, AmphibiousAssaultRules.LandingForce(1000f, 0f), Eps);
        }

        [Test]
        public void 降下中の脆さ_制宙権で軽減される()
        {
            // 対空0.8 × (1 - 制宙権0.5) = 0.4
            Assert.AreEqual(0.4f, AmphibiousAssaultRules.DescentVulnerability(0.5f, 0.8f), Eps);
            // 制宙権を完全に握れば降下は安全
            Assert.AreEqual(0f, AmphibiousAssaultRules.DescentVulnerability(1f, 1f), Eps);
        }

        [Test]
        public void 橋頭堡確保度_押し合いの比で決まる()
        {
            // 700 / (700 + 300) = 0.7
            Assert.AreEqual(0.7f, AmphibiousAssaultRules.BeachheadEstablishment(700f, 300f), Eps);
            // 双方ゼロは確保なし
            Assert.AreEqual(0f, AmphibiousAssaultRules.BeachheadEstablishment(0f, 0f), Eps);
        }

        [Test]
        public void 第一波損害_薄い橋頭堡ほど削られる()
        {
            // 防御0.6 × (1 - 確保0.7) × 致死性0.5 = 0.09
            Assert.AreEqual(0.09f, AmphibiousAssaultRules.FirstWaveLosses(0.6f, 0.7f), Eps);
            // 確保が満ちれば損害なし
            Assert.AreEqual(0f, AmphibiousAssaultRules.FirstWaveLosses(0.6f, 1f), Eps);
        }

        [Test]
        public void 後続降下速度_制宙権と輸送力の積()
        {
            // 0.8 × 0.5 = 0.4
            Assert.AreEqual(0.4f, AmphibiousAssaultRules.FollowUpRate(0.8f, 0.5f), Eps);
        }

        [Test]
        public void 敵の反応集中_探知と機動の積()
        {
            // 0.6 × 0.7 = 0.42
            Assert.AreEqual(0.42f, AmphibiousAssaultRules.DefenderCounterconcentration(0.6f, 0.7f), Eps);
        }

        [Test]
        public void 上陸の勢い_橋頭堡を後続が底上げ()
        {
            // 0.8 × Lerp(0.3, 1, 0.5) = 0.8 × 0.65 = 0.52
            Assert.AreEqual(0.52f, AmphibiousAssaultRules.AssaultMomentum(0.8f, 0.5f), Eps);
            // 後続ゼロでも橋頭堡ぶんの下限は残る: 0.8 × 0.3 = 0.24
            Assert.AreEqual(0.24f, AmphibiousAssaultRules.AssaultMomentum(0.8f, 0f), Eps);
        }

        [Test]
        public void 橋頭堡確保判定_閾値で成否が分かれる()
        {
            Assert.IsTrue(AmphibiousAssaultRules.IsBeachheadHeld(0.7f, 0.6f));
            Assert.IsFalse(AmphibiousAssaultRules.IsBeachheadHeld(0.5f, 0.6f));
            Assert.IsTrue(AmphibiousAssaultRules.IsBeachheadHeld(0.6f, 0.6f)); // ちょうど閾値は確保
        }

        [Test]
        public void 物語_制宙権下の強襲揚陸が橋頭堡を確保する()
        {
            var p = AmphibiousAssaultParams.Default;

            // 制宙権を握っているので降下は比較的安全。
            float vuln = AmphibiousAssaultRules.DescentVulnerability(0.9f, 0.7f);
            Assert.Less(vuln, 0.2f, "制宙権下なら降下中の脆さは小さい");

            // まとまりよく降下し、薄い守りに対し橋頭堡を確保。
            float force = AmphibiousAssaultRules.LandingForce(1000f, 0.8f, p);
            float beachhead = AmphibiousAssaultRules.BeachheadEstablishment(force, 200f, p);
            Assert.Greater(beachhead, 0.6f, "戦力優位なら橋頭堡を確保");
            Assert.IsTrue(AmphibiousAssaultRules.IsBeachheadHeld(beachhead, 0.5f));

            // 第一波の損害は確保度が高いぶん小さい。
            float losses = AmphibiousAssaultRules.FirstWaveLosses(0.5f, beachhead, p);
            Assert.Less(losses, 0.2f, "確保が進めば第一波損害は抑えられる");

            // 制宙権で後続を速く流し込み、勢いがつく。
            float followUp = AmphibiousAssaultRules.FollowUpRate(0.9f, 0.7f);
            float momentum = AmphibiousAssaultRules.AssaultMomentum(beachhead, followUp, p);
            Assert.Greater(momentum, beachhead * p.momentumFloor, "後続が勢いを押し上げる");

            // 一方、制宙権を失い守りが厚いと橋頭堡は崩れる。
            float weakForce = AmphibiousAssaultRules.LandingForce(1000f, 0.3f, p);
            float weakBeachhead = AmphibiousAssaultRules.BeachheadEstablishment(weakForce, 1500f, p);
            Assert.IsFalse(AmphibiousAssaultRules.IsBeachheadHeld(weakBeachhead, 0.5f),
                "戦力劣位では橋頭堡を確保できない");
        }
    }
}
