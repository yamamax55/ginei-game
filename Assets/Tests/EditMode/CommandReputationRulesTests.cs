using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>将帥の声望（武名・威信）の純ロジックテスト。戦歴→武名→鼓舞/萎縮/失墜/功高震主/嫉妬/実効権威を担保。</summary>
    public class CommandReputationRulesTests
    {
        private const float Tol = 1e-4f;

        /// <summary>戦歴から武名＝（勝利−敗北×2）×（1＋規模×1）を正規化（大会戦の勝利ほど名を上げる）。</summary>
        [Test]
        public void ReputationFromRecord_VictoriesAndScaleRaiseFame()
        {
            // net = 8 - 1×2 = 6, weighted = 6×(1+0.5×1)=9, /20 = 0.45
            float r = CommandReputationRules.ReputationFromRecord(8, 1, 0.5f);
            Assert.AreEqual(0.45f, r, Tol);
        }

        /// <summary>負け越せば（敗北が勝利を上回る）声望は 0 にクランプ。</summary>
        [Test]
        public void ReputationFromRecord_LosingRecordClampsToZero()
        {
            // net = 1 - 3×2 = -5, weighted = -5×2 = -10, /20 = -0.5 → 0
            float r = CommandReputationRules.ReputationFromRecord(1, 3, 1f);
            Assert.AreEqual(0f, r, Tol);
        }

        /// <summary>味方士気の鼓舞＝声望×（1＋馴染み×0.5）。古参の部下ほど将を信じて士気が上がる。</summary>
        [Test]
        public void MoraleInspiration_FamiliarityBoostsAndCapApplies()
        {
            // 0.2×(1+0.5×0.5)=0.2×1.25 = 0.25（上限0.3未満）
            Assert.AreEqual(0.25f, CommandReputationRules.MoraleInspiration(0.2f, 0.5f), Tol);
            // 0.8×(1+1×0.5)=1.2 → 上限0.3でクランプ（過信を抑える）
            Assert.AreEqual(0.3f, CommandReputationRules.MoraleInspiration(0.8f, 1f), Tol);
        }

        /// <summary>敵の萎縮＝声望×1.5／（声望×1.5＋敵の場慣れ）。声望0なら萎縮0。</summary>
        [Test]
        public void EnemyIntimidation_FameVersusEnemyExperience()
        {
            // fame=0.6×1.5=0.9, denom=0.9+0.3=1.2, 0.9/1.2 = 0.75
            Assert.AreEqual(0.75f, CommandReputationRules.EnemyIntimidation(0.6f, 0.3f), Tol);
            // 無名(声望0)は敵を萎縮させない
            Assert.AreEqual(0f, CommandReputationRules.EnemyIntimidation(0f, 0.3f), Tol);
        }

        /// <summary>直近の敗北で声望が失墜＝声望×（1−敗北×0.4）。名声は脆く一敗で大きく削れる。</summary>
        [Test]
        public void ReputationDecay_RecentDefeatErodesFame()
        {
            // 0.8×(1-1×0.4) = 0.48
            Assert.AreEqual(0.48f, CommandReputationRules.ReputationDecay(0.8f, 1f), Tol);
            // 敗北なし(0)なら不変
            Assert.AreEqual(0.8f, CommandReputationRules.ReputationDecay(0.8f, 0f), Tol);
        }

        /// <summary>功高震主＝声望が君主の信頼を上回った差×0.8。信頼が声望以上なら0。</summary>
        [Test]
        public void RenownExcess_FameBeyondTrustProvokesWariness()
        {
            // (0.9-0.5)×0.8 = 0.32
            Assert.AreEqual(0.32f, CommandReputationRules.RenownExcess(0.9f, 0.5f), Tol);
            // 信頼が声望以上なら功高震主は起きない
            Assert.AreEqual(0f, CommandReputationRules.RenownExcess(0.4f, 0.6f), Tol);
        }

        /// <summary>嫉妬＝功高震主×同僚の野心×1。どちらかが0なら嫉妬は起きない。</summary>
        [Test]
        public void JealousyProvoked_RequiresExcessAndPeerAmbition()
        {
            // 0.32×0.5×1 = 0.16
            Assert.AreEqual(0.16f, CommandReputationRules.JealousyProvoked(0.32f, 0.5f), Tol);
            // 同僚に野心がなければ嫉妬なし
            Assert.AreEqual(0f, CommandReputationRules.JealousyProvoked(0.32f, 0f), Tol);
        }

        /// <summary>実効権威＝声望×（1＋階級×0.4）−嫉妬×0.6。嫉妬が深ければ負（指揮系統が軋む）にもなる。</summary>
        [Test]
        public void EffectiveAuthority_RankSupportsJealousyErodes()
        {
            // 0.6×(1+0.5×0.4)-0.16×0.6 = 0.6×1.2-0.096 = 0.72-0.096 = 0.624
            Assert.AreEqual(0.624f, CommandReputationRules.EffectiveAuthority(0.6f, 0.5f, 0.16f), Tol);
            // 低声望×無位×強い嫉妬 → 負: 0.1×1-0.8×0.6 = 0.1-0.48 = -0.38
            Assert.AreEqual(-0.38f, CommandReputationRules.EffectiveAuthority(0.1f, 0f, 0.8f), Tol);
        }

        /// <summary>名将判定＝声望が閾値以上（既定0.6）。</summary>
        [Test]
        public void IsRenownedCommander_ThresholdAndDefault()
        {
            Assert.IsTrue(CommandReputationRules.IsRenownedCommander(0.7f, 0.6f));
            Assert.IsFalse(CommandReputationRules.IsRenownedCommander(0.5f, 0.6f));
            // 既定閾値委譲版（0.6）
            Assert.IsTrue(CommandReputationRules.IsRenownedCommander(0.6f));
            Assert.IsFalse(CommandReputationRules.IsRenownedCommander(0.59f));
        }

        /// <summary>
        /// 物語＝戦歴で武名を成した名将が、君主の信頼を超え、野心ある同僚の嫉妬を買い、それでも階級と
        /// 武名で実効権威を保つ一連の流れ（声望の興りと翳り）。
        /// </summary>
        [Test]
        public void Story_RenownedCommanderRisesThenDrawsJealousy()
        {
            // 大会戦を勝ち重ねて武名を成す: net=12-1×2=10, ×(1+0.8)=18, /20 = 0.9
            float rep = CommandReputationRules.ReputationFromRecord(12, 1, 0.8f);
            Assert.AreEqual(0.9f, rep, Tol);
            Assert.IsTrue(CommandReputationRules.IsRenownedCommander(rep));

            // 名将の旗の下、古参部隊は奮い立つ（上限0.3でキャップ）
            float inspire = CommandReputationRules.MoraleInspiration(rep, 0.9f);
            Assert.AreEqual(0.3f, inspire, Tol);

            // 歴戦でない敵は大いに萎縮する: fame=0.9×1.5=1.35→clamp元のままfame=1.35, denom=1.35+0.2=1.55
            float intimidation = CommandReputationRules.EnemyIntimidation(rep, 0.2f);
            Assert.AreEqual(1.35f / 1.55f, intimidation, Tol);

            // しかし声望が君主の信頼(0.5)を超え、功高震主を招く: (0.9-0.5)×0.8 = 0.32
            float excess = CommandReputationRules.RenownExcess(rep, 0.5f);
            Assert.AreEqual(0.32f, excess, Tol);

            // 野心ある同僚(0.7)が嫉妬・讒言に火をつける: 0.32×0.7×1 = 0.224
            float jealousy = CommandReputationRules.JealousyProvoked(excess, 0.7f);
            Assert.AreEqual(0.224f, jealousy, Tol);

            // それでも高い武名と上位の階級(0.8)で実効権威は保たれる:
            // 0.9×(1+0.8×0.4)-0.224×0.6 = 0.9×1.32-0.1344 = 1.188-0.1344 = 1.0536 → clamp 1.0
            float authority = CommandReputationRules.EffectiveAuthority(rep, 0.8f, jealousy);
            Assert.AreEqual(1f, authority, Tol);
        }
    }
}
