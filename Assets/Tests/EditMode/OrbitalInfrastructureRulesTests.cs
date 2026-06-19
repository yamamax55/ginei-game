using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 軌道インフラ（宇宙港・軌道造船所・軌道居住区・防衛プラットフォーム）の建設と機能の純ロジックのテスト。
    /// 既定Params期待値固定。
    /// </summary>
    public class OrbitalInfrastructureRulesTests
    {
        const float Eps = 0.0001f;

        /// <summary>建設進捗＝資材×軌道労働/(1+複雑さ)。複雑な施設ほど遅れる。</summary>
        [Test]
        public void ConstructionProgress_資材と労働で進み複雑さで遅れる()
        {
            // (0.4×0.6)/(1+1) = 0.24/2 = 0.12。
            Assert.AreEqual(0.12f, OrbitalInfrastructureRules.ConstructionProgress(0.4f, 0.6f, 1f), Eps);
            // 資材ゼロは進捗ゼロ。
            Assert.AreEqual(0f, OrbitalInfrastructureRules.ConstructionProgress(0f, 5f, 0f), Eps);
            // 大量投入は1.0で頭打ち（(2×3)/(1+0)=6 → clamp 1）。
            Assert.AreEqual(1f, OrbitalInfrastructureRules.ConstructionProgress(2f, 3f, 0f), Eps);
            // 複雑さが上がると分母が膨らみ進捗が鈍る（複雑さ5：(0.4×0.6)/6 = 0.04）。
            Assert.AreEqual(0.04f, OrbitalInfrastructureRules.ConstructionProgress(0.4f, 0.6f, 5f), Eps);
        }

        /// <summary>宇宙港の処理量＝接岸能力で捌くが交通量が飽和点を超えると頭打ち。</summary>
        [Test]
        public void PortThroughput_混雑で頭打ち()
        {
            // 交通が能力以下＝交通そのまま（過剰能力は遊ぶ）。
            Assert.AreEqual(5f, OrbitalInfrastructureRules.PortThroughput(10f, 5f), Eps);
            // 能力超だが飽和点(能力×2=20)以下＝交通そのまま捌ける（15）。
            Assert.AreEqual(15f, OrbitalInfrastructureRules.PortThroughput(10f, 15f), Eps);
            // 交通30は飽和点20で頭打ち（発着待ち）。
            Assert.AreEqual(20f, OrbitalInfrastructureRules.PortThroughput(10f, 30f), Eps);
            // 接岸能力ゼロは何も捌けない。
            Assert.AreEqual(0f, OrbitalInfrastructureRules.PortThroughput(0f, 10f), Eps);
        }

        /// <summary>軌道造船所の産出＝造船能力×サプライチェーン。部材が細ると落ちる。</summary>
        [Test]
        public void OrbitalShipyardOutput_サプライチェーンで産出()
        {
            // 8×0.5×1 = 4。
            Assert.AreEqual(4f, OrbitalInfrastructureRules.OrbitalShipyardOutput(8f, 0.5f), Eps);
            // サプライチェーン満タンなら能力そのまま。
            Assert.AreEqual(8f, OrbitalInfrastructureRules.OrbitalShipyardOutput(8f, 1f), Eps);
            // サプライチェーンゼロで停止。
            Assert.AreEqual(0f, OrbitalInfrastructureRules.OrbitalShipyardOutput(8f, 0f), Eps);
        }

        /// <summary>軌道居住区の収容力＝与圧容積×生命維持。生命維持が落ちると収容力が落ちる。</summary>
        [Test]
        public void HabitatCapacity_与圧容積と生命維持で収容()
        {
            // 100×0.8×1 = 80。
            Assert.AreEqual(80f, OrbitalInfrastructureRules.HabitatCapacity(100f, 0.8f), Eps);
            // 生命維持ゼロは居住不能。
            Assert.AreEqual(0f, OrbitalInfrastructureRules.HabitatCapacity(100f, 0f), Eps);
        }

        /// <summary>軌道防衛＝防衛プラットフォーム×(1+惑星シールド×底上げ)。シールドが守る。</summary>
        [Test]
        public void OrbitalDefense_シールドが軌道を底上げ()
        {
            // 4×(1+0.6×0.5) = 4×1.3 = 5.2。
            Assert.AreEqual(5.2f, OrbitalInfrastructureRules.OrbitalDefense(4f, 0.6f), Eps);
            // シールドなしならプラットフォームそのまま。
            Assert.AreEqual(4f, OrbitalInfrastructureRules.OrbitalDefense(4f, 0f), Eps);
            // プラットフォームが無ければシールドだけでは0。
            Assert.AreEqual(0f, OrbitalInfrastructureRules.OrbitalDefense(0f, 1f), Eps);
        }

        /// <summary>相乗効果＝1+相乗強度×統合度×(施設数-1)。統合されるほど効率が上がる。</summary>
        [Test]
        public void Synergy_施設数と統合で複合効率()
        {
            // 1+0.25×0.8×(3-1) = 1+0.4 = 1.4。
            Assert.AreEqual(1.4f, OrbitalInfrastructureRules.Synergy(3, 0.8f), Eps);
            // 単独施設は相乗なし（1.0）。
            Assert.AreEqual(1f, OrbitalInfrastructureRules.Synergy(1, 1f), Eps);
            // 統合度ゼロは相乗なし。
            Assert.AreEqual(1f, OrbitalInfrastructureRules.Synergy(5, 0f), Eps);
        }

        /// <summary>脆弱性＝素の脆さ×(1-防御)×集中度。軌道施設は防御が無いと脆い。</summary>
        [Test]
        public void VulnerabilityToAttack_防御が無いと脆い()
        {
            // 集中度 1-1/4 = 0.75。1×(1-0.5)×0.75 = 0.375。
            Assert.AreEqual(0.375f, OrbitalInfrastructureRules.VulnerabilityToAttack(4, 0.5f), Eps);
            // 防御満タンなら守り切る（脆さ0）。
            Assert.AreEqual(0f, OrbitalInfrastructureRules.VulnerabilityToAttack(4, 1f), Eps);
            // 単独施設は標的の集中なし（集中度0 → 脆さ0）。
            Assert.AreEqual(0f, OrbitalInfrastructureRules.VulnerabilityToAttack(1, 0f), Eps);
            // 施設が無ければ失う物も無い。
            Assert.AreEqual(0f, OrbitalInfrastructureRules.VulnerabilityToAttack(0, 0f), Eps);
        }

        /// <summary>稼働可否＝建設進捗が閾値以上か。既定閾値は0.8。</summary>
        [Test]
        public void IsInfrastructureOperational_閾値で稼働判定()
        {
            // 進捗0.8 ≥ 閾値0.8 → 稼働。
            Assert.IsTrue(OrbitalInfrastructureRules.IsInfrastructureOperational(0.8f, 0.8f));
            // 進捗0.79 < 0.8 → 建設中で機能しない。
            Assert.IsFalse(OrbitalInfrastructureRules.IsInfrastructureOperational(0.79f, 0.8f));
            // 既定委譲版は閾値0.8＝進捗0.5は未稼働。
            Assert.IsFalse(OrbitalInfrastructureRules.IsInfrastructureOperational(0.5f));
            // 既定委譲版＝進捗1.0は稼働。
            Assert.IsTrue(OrbitalInfrastructureRules.IsInfrastructureOperational(1f));
        }

        /// <summary>
        /// 物語：辺境星系に軌道インフラ複合体（宇宙港＋軌道造船所＋軌道居住区＋防衛プラットフォーム）を築く。
        /// まず資材と軌道労働を注ぎ込み複合体を建設（進捗0.9）＝稼働閾値0.8を超え稼働開始。
        /// 4施設が高統合（0.8）で集まり相乗効果1.6倍＝複合施設は単体の和より効率的に回る。
        /// しかし防衛プラットフォームが薄く軌道防衛が低い（正規化0.2）と、4施設は格好の標的で脆弱性が高い
        /// （0.6）＝敵艦隊に襲われれば軌道インフラは脆く崩れる。防衛を厚くする（正規化0.9）と脆弱性は
        /// 0.075まで下がり、複合体は守り切れる＝軌道は脆いがシールドと防衛プラットフォームが盾になる。
        /// </summary>
        [Test]
        public void Story_軌道複合体は相乗で効率的だが防御が薄いと脆い()
        {
            var p = OrbitalInfrastructureParams.Default;

            // --- 建設（資材0.95×労働1.9/(1+1複雑さ)）---
            float progress = OrbitalInfrastructureRules.ConstructionProgress(0.95f, 1.9f, 1f, p);
            // (0.95×1.9)/2 = 1.805/2 = 0.9025。
            Assert.AreEqual(0.9025f, progress, 1e-3f);
            // 進捗0.9025 ≥ 閾値0.8 → 稼働開始。
            Assert.IsTrue(OrbitalInfrastructureRules.IsInfrastructureOperational(progress, p.operationalThreshold));

            // --- 機能（各施設）---
            float throughput = OrbitalInfrastructureRules.PortThroughput(10f, 12f, p); // 12≤飽和20 → 12
            Assert.AreEqual(12f, throughput, Eps);
            float ships = OrbitalInfrastructureRules.OrbitalShipyardOutput(6f, 0.75f, p); // 6×0.75 = 4.5
            Assert.AreEqual(4.5f, ships, Eps);
            float habitat = OrbitalInfrastructureRules.HabitatCapacity(200f, 0.9f, p); // 200×0.9 = 180
            Assert.AreEqual(180f, habitat, Eps);

            // --- 相乗（4施設×統合0.8）---
            float synergy = OrbitalInfrastructureRules.Synergy(4, 0.8f, p); // 1+0.25×0.8×3 = 1.6
            Assert.AreEqual(1.6f, synergy, Eps);

            // --- 防御が薄い（軌道防衛正規化0.2）---
            // 集中度 1-1/4 = 0.75。1×(1-0.2)×0.75 = 0.6。
            float vulnThin = OrbitalInfrastructureRules.VulnerabilityToAttack(4, 0.2f, p);
            Assert.AreEqual(0.6f, vulnThin, Eps);

            // --- 防御を厚く（軌道防衛正規化0.9）---
            // 1×(1-0.9)×0.75 = 0.075。
            float vulnThick = OrbitalInfrastructureRules.VulnerabilityToAttack(4, 0.9f, p);
            Assert.AreEqual(0.075f, vulnThick, Eps);
            // 防御を厚くすれば脆弱性は大きく下がる（軌道は脆いが盾で守れる）。
            Assert.Less(vulnThick, vulnThin);
        }
    }
}
