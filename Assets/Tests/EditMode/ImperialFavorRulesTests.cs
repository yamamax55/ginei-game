using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>君寵（君主の寵愛）とその不安定さの純ロジックテスト。寵の獲得・権勢・気まぐれな失墜を担保。</summary>
    public class ImperialFavorRulesTests
    {
        private const float Tol = 1e-4f;

        /// <summary>寵の獲得＝功績×追従×個人的親密さの加重和（実力だけでなくおべっかと親しさが寵を呼ぶ）。</summary>
        [Test]
        public void FavorGain_BlendsServiceFlatteryRapport()
        {
            // 0.8×0.5 + 0.6×0.3 + 0.4×0.2 = 0.4 + 0.18 + 0.08 = 0.66
            float g = ImperialFavorRules.FavorGain(0.8f, 0.6f, 0.4f);
            Assert.AreEqual(0.66f, g, Tol);
        }

        /// <summary>追従と親密さがあれば功績ゼロでも寵を得られる（佞臣の道）。</summary>
        [Test]
        public void FavorGain_FlatteryAndRapportAloneStillGain()
        {
            // 0×0.5 + 1×0.3 + 1×0.2 = 0.5
            float g = ImperialFavorRules.FavorGain(0f, 1f, 1f);
            Assert.AreEqual(0.5f, g, Tol);
        }

        /// <summary>寵臣の権勢＝寵×君主の権力（虎の威を借る・弱い君主の寵臣は威を借りきれない）。</summary>
        [Test]
        public void FavoriteInfluence_BorrowsSovereignPower()
        {
            // 0.66×0.8×1.0 = 0.528
            Assert.AreEqual(0.528f, ImperialFavorRules.FavoriteInfluence(0.66f, 0.8f), Tol);
            // 君主が無力なら寵があっても権勢なし
            Assert.AreEqual(0f, ImperialFavorRules.FavoriteInfluence(1f, 0f), Tol);
        }

        /// <summary>寵の不安定さ＝君主の気まぐれ÷寵の根拠（根拠が薄いほど揺らぐ）。</summary>
        [Test]
        public void FavorVolatility_RisesWithCapriceAndThinBasis()
        {
            // 0.4 / 0.8 × 1.0 = 0.5
            Assert.AreEqual(0.5f, ImperialFavorRules.FavorVolatility(0.4f, 0.8f), Tol);
            // 根拠が厚い（=1）と同じ気まぐれでも不安定さは下がる: 0.4/1 = 0.4
            Assert.AreEqual(0.4f, ImperialFavorRules.FavorVolatility(0.4f, 1f), Tol);
            // 根拠ゼロは basisFloor(0.2) で発散せず: 0.1/0.2 = 0.5
            Assert.AreEqual(0.5f, ImperialFavorRules.FavorVolatility(0.1f, 0f), Tol);
        }

        /// <summary>増長＝権勢×(1-人格)。人格が低い寵臣ほど権勢を笠に着て驕る（満点人格は増長しない）。</summary>
        [Test]
        public void Arrogance_LowCharacterAmplifiesInfluence()
        {
            // 0.8×(1-0.25)×1.0 = 0.6
            Assert.AreEqual(0.6f, ImperialFavorRules.Arrogance(0.8f, 0.25f), Tol);
            // 人格満点なら増長なし
            Assert.AreEqual(0f, ImperialFavorRules.Arrogance(0.8f, 1f), Tol);
        }

        /// <summary>周囲の追従＝権勢に応じてすり寄る（権勢が大きいほど追従も大きい）。</summary>
        [Test]
        public void Sycophancy_TracksInfluence()
        {
            Assert.AreEqual(0.6f, ImperialFavorRules.Sycophancy(0.6f), Tol);
            Assert.AreEqual(0f, ImperialFavorRules.Sycophancy(0f), Tol);
        }

        /// <summary>周囲の反感＝増長×追従（表向きおもねる者ほど内心で反感を募らせる）。</summary>
        [Test]
        public void PeerResentment_FromArroganceAndSycophancy()
        {
            // 0.6×0.5×1.0 = 0.3
            Assert.AreEqual(0.3f, ImperialFavorRules.PeerResentment(0.6f, 0.5f), Tol);
        }

        /// <summary>寵愛喪失（失脚）＝不安定さ×増長×反感（三つ揃うと一気に失脚する）。</summary>
        [Test]
        public void FavorLoss_RequiresVolatilityArroganceAndResentment()
        {
            // 0.5×0.6×0.4×1.0 = 0.12
            Assert.AreEqual(0.12f, ImperialFavorRules.FavorLoss(0.5f, 0.6f, 0.4f), Tol);
            // 反感がなければ（=0）失脚は起きない
            Assert.AreEqual(0f, ImperialFavorRules.FavorLoss(0.5f, 0.6f, 0f), Tol);
        }

        /// <summary>寵の安泰判定＝寵が閾値超かつ失脚度合いが小さいとき真（寵が厚くても失脚度合いが高ければ安泰でない）。</summary>
        [Test]
        public void IsFavorSecure_NeedsHighFavorAndLowLoss()
        {
            // 既定閾値0.4: favor 0.6>0.4 かつ loss 0.12<0.6 → 安泰
            Assert.IsTrue(ImperialFavorRules.IsFavorSecure(0.6f, 0.12f));
            // 寵が厚くても失脚度合いが高ければ安泰でない: loss 0.7 ≥ 0.6
            Assert.IsFalse(ImperialFavorRules.IsFavorSecure(0.6f, 0.7f));
            // 寵が薄ければ安泰でない
            Assert.IsFalse(ImperialFavorRules.IsFavorSecure(0.3f, 0.0f));
        }

        /// <summary>Params は全フィールドを範囲内にクランプし、根拠下駄はゼロ割を防ぐ下限を持つ。</summary>
        [Test]
        public void Params_ClampsFields()
        {
            var p = new ImperialFavorParams(1.5f, -0.2f, 0.5f, -1f, -1f, 0f, -1f, -1f, -1f, -1f, 2f);
            Assert.AreEqual(1f, p.serviceWeight, Tol);
            Assert.AreEqual(0f, p.flatteryWeight, Tol);
            Assert.AreEqual(0f, p.influenceLeverage, Tol);
            Assert.AreEqual(0.01f, p.basisFloor, Tol); // 下限でゼロ割回避
            Assert.AreEqual(1f, p.secureThreshold, Tol);
        }

        /// <summary>
        /// 物語＝功績薄く佞臣の道で君主に取り入った寵臣が、弱い人格ゆえ権勢を笠に着て増長し、
        /// 周囲の反感と君主の気まぐれが重なって一夜にして失脚する（寵愛の不安定さ）。
        /// </summary>
        [Test]
        public void Narrative_FlatterersRiseAndFall()
        {
            var p = ImperialFavorParams.Default;
            // おべっかと親密さで寵を得る（功績は薄い）
            float favor = ImperialFavorRules.FavorGain(0.2f, 0.9f, 0.8f, p);
            // 0.2×0.5 + 0.9×0.3 + 0.8×0.2 = 0.1 + 0.27 + 0.16 = 0.53
            Assert.AreEqual(0.53f, favor, Tol);

            // 強い君主の権力を借りて権勢を振るう
            float influence = ImperialFavorRules.FavoriteInfluence(favor, 0.9f, p);
            // 0.53×0.9 = 0.477
            Assert.AreEqual(0.477f, influence, Tol);

            // 功績（根拠）が薄く君主は気まぐれ → 寵は不安定
            float volatility = ImperialFavorRules.FavorVolatility(0.7f, 0.2f, p);
            // 0.7/0.2 = 3.5 → clamp 1.0
            Assert.AreEqual(1f, volatility, Tol);

            // 人格が低いので権勢を笠に着て増長
            float arrogance = ImperialFavorRules.Arrogance(influence, 0.2f, p);
            // 0.477×0.8 = 0.3816
            Assert.AreEqual(0.3816f, arrogance, Tol);

            // 周囲はすり寄りつつ反感を募らせる
            float syco = ImperialFavorRules.Sycophancy(influence, p);
            Assert.AreEqual(0.477f, syco, Tol);
            float resent = ImperialFavorRules.PeerResentment(arrogance, syco, p);
            // 0.3816×0.477 = 0.18201...
            Assert.AreEqual(0.182023f, resent, Tol);

            // 不安定さ・増長・反感が重なり失脚する
            float loss = ImperialFavorRules.FavorLoss(volatility, arrogance, resent, p);
            // 1.0×0.3816×0.182023 = 0.069460...
            Assert.AreEqual(0.06946f, loss, Tol);

            // 寵は厚い（0.53>0.4）が失脚の芽はあり、なお安泰圏（loss<0.6）にとどまる段階
            Assert.IsTrue(ImperialFavorRules.IsFavorSecure(favor, loss, p.secureThreshold));
        }
    }
}
