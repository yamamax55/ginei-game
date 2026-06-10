using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// RankSystem（階級 tier 判定の唯一の窓口）の現状動作を固定する特性テスト。
    /// 既定ラダーに合わせ tier: 5准将/6少将/7中将/8大将/10元帥（9＝上級大将は欠番）で検証する。
    /// </summary>
    public class RankSystemTests
    {
        // 同盟型（9＝上級大将が欠番）の階級表を持つ FactionData を作る。
        private FactionData MakeAllianceLikeFaction()
        {
            var f = ScriptableObject.CreateInstance<FactionData>();
            f.ranks = new List<FactionData.RankEntry>
            {
                new FactionData.RankEntry(5, "准将"),
                new FactionData.RankEntry(6, "少将"),
                new FactionData.RankEntry(7, "中将"),
                new FactionData.RankEntry(8, "大将"),
                new FactionData.RankEntry(10, "元帥"),
            };
            return f;
        }

        [Test]
        public void AreEquivalent_SameTier_IsTrue()
        {
            Assert.IsTrue(RankSystem.AreEquivalent(8, 8));
        }

        [Test]
        public void AreEquivalent_DifferentTier_IsFalse()
        {
            Assert.IsFalse(RankSystem.AreEquivalent(8, 7));
        }

        [Test]
        public void Compare_And_IsHigher_FollowTierOrder()
        {
            Assert.Greater(RankSystem.Compare(10, 8), 0);
            Assert.Less(RankSystem.Compare(7, 8), 0);
            Assert.AreEqual(0, RankSystem.Compare(8, 8));
            Assert.IsTrue(RankSystem.IsHigher(10, 8));
            Assert.IsFalse(RankSystem.IsHigher(8, 10));
        }

        [Test]
        public void NextRankTier_ReturnsSmallestHigher()
        {
            var f = MakeAllianceLikeFaction();
            Assert.AreEqual(6, RankSystem.NextRankTier(f, 5)); // 准将→少将
            Assert.AreEqual(10, RankSystem.NextRankTier(f, 8)); // 大将→元帥（9欠番を飛ばす）
        }

        [Test]
        public void NextRankTier_AtTop_StaysSame()
        {
            var f = MakeAllianceLikeFaction();
            Assert.AreEqual(10, RankSystem.NextRankTier(f, 10));
        }

        [Test]
        public void NextRankTier_NullFaction_StaysSame()
        {
            Assert.AreEqual(8, RankSystem.NextRankTier(null, 8));
        }

        [Test]
        public void PreviousRankTier_ReturnsLargestLower()
        {
            var f = MakeAllianceLikeFaction();
            Assert.AreEqual(7, RankSystem.PreviousRankTier(f, 8));
        }

        [Test]
        public void PreviousRankTier_AtBottom_StaysSame()
        {
            var f = MakeAllianceLikeFaction();
            Assert.AreEqual(5, RankSystem.PreviousRankTier(f, 5));
        }

        [Test]
        public void ResolveTier_ExactMatch_ReturnsSame()
        {
            var f = MakeAllianceLikeFaction();
            Assert.AreEqual(8, RankSystem.ResolveTier(f, 8));
        }

        [Test]
        public void ResolveTier_MissingTier_SnapsToNearestLower()
        {
            var f = MakeAllianceLikeFaction();
            // 9（上級大将）は同盟型に無い → 直近下位の 8（大将）へ。
            Assert.AreEqual(8, RankSystem.ResolveTier(f, 9));
        }

        [Test]
        public void ResolveTier_BelowAll_SnapsToNearestHigher()
        {
            var f = MakeAllianceLikeFaction();
            // 3 は最下位(5)より下 → 直近下位が無いので直近上位の 5 へ。
            Assert.AreEqual(5, RankSystem.ResolveTier(f, 3));
        }

        [Test]
        public void ResolveTier_NoRanks_ReturnsSame()
        {
            var f = ScriptableObject.CreateInstance<FactionData>();
            f.ranks = new List<FactionData.RankEntry>();
            Assert.AreEqual(42, RankSystem.ResolveTier(f, 42));
        }

        // ───────── ResolveRankName（#14・HUD表示用） ─────────

        [Test]
        public void ResolveRankName_NullFaction_IsEmpty()
        {
            Assert.AreEqual("", RankSystem.ResolveRankName(null, 8));
        }

        [Test]
        public void ResolveRankName_UnsetTier_IsEmpty()
        {
            var f = MakeAllianceLikeFaction();
            // 0 以下＝未設定＝階級を出さない（後方互換）。
            Assert.AreEqual("", RankSystem.ResolveRankName(f, 0));
            Assert.AreEqual("", RankSystem.ResolveRankName(f, -1));
        }

        [Test]
        public void ResolveRankName_ExistingTier_ReturnsName()
        {
            var f = MakeAllianceLikeFaction();
            Assert.AreEqual("大将", RankSystem.ResolveRankName(f, 8));
            Assert.AreEqual("元帥", RankSystem.ResolveRankName(f, 10));
        }

        [Test]
        public void ResolveRankName_MissingTier_SnapsToNearest()
        {
            var f = MakeAllianceLikeFaction();
            // 9（上級大将）は同盟型に無い → 直近下位 8（大将）の名称。
            Assert.AreEqual("大将", RankSystem.ResolveRankName(f, 9));
        }

        [Test]
        public void ResolveRankName_EmptyRanks_IsEmpty()
        {
            var f = ScriptableObject.CreateInstance<FactionData>();
            f.ranks = new List<FactionData.RankEntry>();
            Assert.AreEqual("", RankSystem.ResolveRankName(f, 8));
        }

        // ───────── DefaultRankName / ResolveRankNameOrDefault（#14・フォールバック） ─────────

        [Test]
        public void DefaultRankName_KnownTiers()
        {
            Assert.AreEqual("中将", RankSystem.DefaultRankName(7));
            Assert.AreEqual("上級大将", RankSystem.DefaultRankName(9));
            Assert.AreEqual("元帥", RankSystem.DefaultRankName(10));
        }

        [Test]
        public void DefaultRankName_OutOfRange_IsEmpty()
        {
            Assert.AreEqual("", RankSystem.DefaultRankName(0));
            Assert.AreEqual("", RankSystem.DefaultRankName(11));
        }

        [Test]
        public void ResolveRankNameOrDefault_NullFaction_UsesDefaultLadder()
        {
            // FactionData 未割当でも既定ラダーで階級が出る（HUD用）。
            Assert.AreEqual("中将", RankSystem.ResolveRankNameOrDefault(null, 7));
        }

        [Test]
        public void ResolveRankNameOrDefault_FactionTablePreferred()
        {
            var f = MakeAllianceLikeFaction();
            // 勢力の階級表に該当があればそれを使う。
            Assert.AreEqual("中将", RankSystem.ResolveRankNameOrDefault(f, 7));
            // 同盟型に無い 9 は表側で直近下位 8（大将）へ丸め → 既定の「上級大将」ではなく「大将」。
            Assert.AreEqual("大将", RankSystem.ResolveRankNameOrDefault(f, 9));
        }

        [Test]
        public void ResolveRankNameOrDefault_UnsetTier_IsEmpty()
        {
            Assert.AreEqual("", RankSystem.ResolveRankNameOrDefault(null, 0));
            Assert.AreEqual("", RankSystem.ResolveRankNameOrDefault(MakeAllianceLikeFaction(), 0));
        }

        // ───────── 敵対的エッジケース追加（境界/クランプ/null/分岐/不変条件） ─────────

        // 帝国型（9＝上級大将を含む完全ラダー）。同盟型との非対称検証に使う。
        private FactionData MakeImperialLikeFaction()
        {
            var f = ScriptableObject.CreateInstance<FactionData>();
            f.ranks = new List<FactionData.RankEntry>
            {
                new FactionData.RankEntry(5, "准将"),
                new FactionData.RankEntry(6, "少将"),
                new FactionData.RankEntry(7, "中将"),
                new FactionData.RankEntry(8, "大将"),
                new FactionData.RankEntry(9, "上級大将"),
                new FactionData.RankEntry(10, "元帥"),
            };
            return f;
        }

        // (1) ResolveTier: ranks==null の faction は tier をそのまま返す（null ガード分岐）。
        // 仕様：「階級が一つも無ければ tier をそのまま返す」。null ranks も同様であるべき。
        [Test]
        public void ResolveTier_NullRanksList_ReturnsSame()
        {
            var f = ScriptableObject.CreateInstance<FactionData>();
            f.ranks = null;
            Assert.AreEqual(7, RankSystem.ResolveTier(f, 7));
        }

        // (2) ResolveTier: 全ランクより上の tier は直近下位（最上位 tier=10）へスナップ。
        // 仕様：完全一致無し→直近下位優先。99 は 10 へ丸まるべき。
        [Test]
        public void ResolveTier_AboveAll_SnapsToTop()
        {
            var f = MakeAllianceLikeFaction();
            Assert.AreEqual(10, RankSystem.ResolveTier(f, 99));
        }

        // (3) ResolveTier: 両側に候補がある欠番は「直近下位」を優先する非対称性。
        // 同盟型(…7,8,10…)で tier 9 は下8/上10 の両方が在るが、仕様は下位優先＝8。
        [Test]
        public void ResolveTier_BetweenTwo_PrefersLowerNotHigher()
        {
            var f = MakeAllianceLikeFaction();
            Assert.AreEqual(8, RankSystem.ResolveTier(f, 9));
            // 帝国型なら 9 は実在 → 9 のまま（非対称の対照）。
            Assert.AreEqual(9, RankSystem.ResolveTier(MakeImperialLikeFaction(), 9));
        }

        // (4) NextRankTier: ランク表に null エントリが混じっても落ちず正しく飛ばす（r==null 分岐）。
        [Test]
        public void NextRankTier_SkipsNullEntries()
        {
            var f = ScriptableObject.CreateInstance<FactionData>();
            f.ranks = new List<FactionData.RankEntry>
            {
                new FactionData.RankEntry(5, "准将"),
                null,
                new FactionData.RankEntry(8, "大将"),
                null,
            };
            // 5 の次は（null を飛ばして）8。
            Assert.AreEqual(8, RankSystem.NextRankTier(f, 5));
            // 8 が最上位（null 以外で上が無い）→ 据え置き。
            Assert.AreEqual(8, RankSystem.NextRankTier(f, 8));
        }

        // (5) PreviousRankTier: null エントリ混在でも直近下位を返す（r==null 分岐）。
        [Test]
        public void PreviousRankTier_SkipsNullEntries()
        {
            var f = ScriptableObject.CreateInstance<FactionData>();
            f.ranks = new List<FactionData.RankEntry>
            {
                null,
                new FactionData.RankEntry(5, "准将"),
                new FactionData.RankEntry(8, "大将"),
                null,
            };
            Assert.AreEqual(5, RankSystem.PreviousRankTier(f, 8));
            // 5 が最下位 → 据え置き。
            Assert.AreEqual(5, RankSystem.PreviousRankTier(f, 5));
        }

        // (6) Next/Previous の往復：上位へ上げて下げると元に戻る（隣接 tier の単調往復不変条件）。
        [Test]
        public void NextThenPrevious_RoundTrips()
        {
            var f = MakeImperialLikeFaction();
            for (int t = 5; t <= 9; t++)
            {
                int up = RankSystem.NextRankTier(f, t);
                Assert.AreEqual(t, RankSystem.PreviousRankTier(f, up),
                    $"tier {t} の昇進→降格で元に戻らない（up={up}）");
            }
        }

        // (7) Compare の反対称性：Compare(a,b) と Compare(b,a) は符号が反転する不変条件。
        [Test]
        public void Compare_IsAntisymmetric()
        {
            Assert.AreEqual(System.Math.Sign(RankSystem.Compare(5, 10)),
                            -System.Math.Sign(RankSystem.Compare(10, 5)));
            Assert.AreEqual(0, RankSystem.Compare(-3, -3));
            // 負 tier でも順序は保たれる。
            Assert.Less(RankSystem.Compare(-5, -1), 0);
            Assert.IsTrue(RankSystem.IsHigher(-1, -5));
        }

        // (8) AreEquivalent と Compare==0 の整合（同値判定の二重窓口が一致する不変条件）。
        [Test]
        public void AreEquivalent_ConsistentWithCompareZero()
        {
            int[] samples = { -2, 0, 5, 8, 10, 99 };
            foreach (var a in samples)
                foreach (var b in samples)
                    Assert.AreEqual(RankSystem.Compare(a, b) == 0,
                                    RankSystem.AreEquivalent(a, b),
                                    $"AreEquivalent と Compare==0 が不一致（a={a}, b={b}）");
        }

        // (9) DefaultRankName: 負の tier / 上限直上は空文字（範囲外クランプの両端）。
        [Test]
        public void DefaultRankName_NegativeAndBoundary_IsEmpty()
        {
            Assert.AreEqual("", RankSystem.DefaultRankName(-1));
            Assert.AreEqual("", RankSystem.DefaultRankName(4));   // 5 の直下
            Assert.AreEqual("准将", RankSystem.DefaultRankName(5)); // 下端
            Assert.AreEqual("元帥", RankSystem.DefaultRankName(10)); // 上端
            Assert.AreEqual("", RankSystem.DefaultRankName(int.MaxValue));
        }

        // (10) ResolveRankNameOrDefault: 階級表が空でも既定ラダーへフォールバックする。
        // 空 ranks → ResolveTier は tier 据え置き → GetRankName は "" → DefaultRankName(7)="中将"。
        [Test]
        public void ResolveRankNameOrDefault_EmptyTable_FallsBackToDefaultLadder()
        {
            var f = ScriptableObject.CreateInstance<FactionData>();
            f.ranks = new List<FactionData.RankEntry>();
            Assert.AreEqual("中将", RankSystem.ResolveRankNameOrDefault(f, 7));
            // 既定ラダーにも無い tier は空文字。
            Assert.AreEqual("", RankSystem.ResolveRankNameOrDefault(f, 4));
        }

        // (11) ResolveRankNameOrDefault: 負 tier は faction の有無に関わらず空文字（tier<=0 ガード）。
        [Test]
        public void ResolveRankNameOrDefault_NegativeTier_IsEmpty()
        {
            Assert.AreEqual("", RankSystem.ResolveRankNameOrDefault(null, -5));
            Assert.AreEqual("", RankSystem.ResolveRankNameOrDefault(MakeImperialLikeFaction(), -5));
        }

        // (12) ResolveRankName: ランク表に該当 tier があるが rankName が空文字のエントリは "" を返す。
        // GetRankName は一致 tier の rankName をそのまま返す＝空名はそのまま空。
        [Test]
        public void ResolveRankName_EmptyNameEntry_ReturnsEmpty()
        {
            var f = ScriptableObject.CreateInstance<FactionData>();
            f.ranks = new List<FactionData.RankEntry>
            {
                new FactionData.RankEntry(8, ""),
            };
            Assert.AreEqual("", RankSystem.ResolveRankName(f, 8));
            // ただし OrDefault は空名を「引けなかった」とみなし既定ラダーへ → 大将。
            Assert.AreEqual("大将", RankSystem.ResolveRankNameOrDefault(f, 8));
        }
    }
}
