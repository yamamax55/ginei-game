using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>固定会戦QAの調整プリセット（SPEED-07）：既定＝未指定・1項目だけ変える・不正値の拒否・設定名/版の記録。</summary>
    public class BattleQaTuningProfileTests
    {
        private static readonly BattleQaTuningField[] AllFields =
        {
            BattleQaTuningField.移動速度, BattleQaTuningField.回頭速度, BattleQaTuningField.士気回復量,
            BattleQaTuningField.敗走回復待ち, BattleQaTuningField.軍団隊形間隔,
        };

        [Test]
        public void Default_HasNoOverrides_AndResolvesToBaseline()
        {
            BattleQaTuningProfile p = BattleQaTuningProfile.Default;
            Assert.IsTrue(p.IsDefault);
            Assert.AreEqual(0, p.OverrideCount);
            Assert.AreEqual(BattleQaTuningProfile.CurrentVersion, p.version);
            CollectionAssert.IsEmpty(p.Validate());
            foreach (BattleQaTuningField f in AllFields)
            {
                Assert.IsFalse(p.Get(f).IsSet, f + " が既定で指定されている");
                Assert.AreEqual(3.5f, p.Get(f).Resolve(3.5f), 1e-6f, f + "：既定は基準値のまま");
            }
        }

        [Test]
        public void Override_ResolvesByMode()
        {
            Assert.AreEqual(5.25f, BattleQaTuningOverride.Scale(1.5f).Resolve(3.5f), 1e-4f);
            Assert.AreEqual(9f, BattleQaTuningOverride.Absolute(9f).Resolve(3.5f), 1e-6f);
            Assert.AreEqual(3.5f, BattleQaTuningOverride.None.Resolve(3.5f), 1e-6f);
        }

        [Test]
        public void ScaleOne_ChangesOnlyThatField()
        {
            foreach (BattleQaTuningField target in AllFields)
            {
                BattleQaTuningProfile p = BattleQaTuningCatalog.ScaleOne(target, 2f);
                Assert.AreEqual(1, p.OverrideCount, target + "：1項目だけのはず");
                CollectionAssert.IsEmpty(p.Validate(), target + " の比較プリセットが不正");
                foreach (BattleQaTuningField f in AllFields)
                {
                    float r = p.Get(f).Resolve(10f);
                    if (f == target) Assert.AreEqual(20f, r, 1e-4f, f + "：倍率が効いていない");
                    else Assert.AreEqual(10f, r, 1e-6f, f + "：対象外の項目が変わった");
                }
            }
        }

        [Test]
        public void With_KeepsOtherOverrides_AndDoesNotMutateSource()
        {
            BattleQaTuningProfile a = BattleQaTuningCatalog.ScaleOne(BattleQaTuningField.移動速度, 1.5f);
            BattleQaTuningProfile b = a.With("二項目", BattleQaTuningField.士気回復量, BattleQaTuningOverride.Absolute(0f));
            Assert.AreEqual(1, a.OverrideCount, "元のプリセットが書き換わった");
            Assert.AreEqual(2, b.OverrideCount);
            Assert.AreEqual("二項目", b.name);
            Assert.AreEqual(BattleQaTuningMode.倍率, b.Get(BattleQaTuningField.移動速度).mode);
            CollectionAssert.IsEmpty(b.Validate(), "回復量0は有効な条件");
        }

        [Test]
        public void Validate_RejectsNaNInfinityAndOutOfRange()
        {
            var bad = new List<BattleQaTuningProfile>
            {
                BattleQaTuningCatalog.ScaleOne(BattleQaTuningField.移動速度, float.NaN),
                BattleQaTuningCatalog.ScaleOne(BattleQaTuningField.回頭速度, float.PositiveInfinity),
                BattleQaTuningCatalog.ScaleOne(BattleQaTuningField.移動速度, 0f),
                BattleQaTuningCatalog.ScaleOne(BattleQaTuningField.士気回復量, -1f),
                BattleQaTuningProfile.Default.With("x", BattleQaTuningField.回頭速度, BattleQaTuningOverride.Absolute(-3f)),
                BattleQaTuningProfile.Default.With("x", BattleQaTuningField.敗走回復待ち, BattleQaTuningOverride.Absolute(float.NegativeInfinity)),
            };
            foreach (BattleQaTuningProfile p in bad)
                Assert.IsNotEmpty(p.Validate(), p.Describe() + " を拒否していない");

            Assert.IsEmpty(BattleQaTuningCatalog.ScaleOne(BattleQaTuningField.敗走回復待ち, 0f).Validate(), "回復待ち0は有効");
        }

        [Test]
        public void Validate_CorpsSpacingIsConnectedToMinSpacing_AndRejectsInvalid()
        {
            BattleQaTuningProfile p = BattleQaTuningCatalog.ScaleOne(BattleQaTuningField.軍団隊形間隔, 1.5f);
            CollectionAssert.IsEmpty(p.Validate(), "軍団隊形間隔は実接続＝有効な指定を拒否しない");
            Assert.IsTrue(BattleQaTuningProfile.RequiresCorpsCommandManager(BattleQaTuningField.軍団隊形間隔));
            Assert.IsFalse(BattleQaTuningProfile.RequiresCorpsCommandManager(BattleQaTuningField.移動速度));
            Assert.AreEqual("BattlefieldCommandManager.corpsMinSpacing",
                BattleQaTuningProfile.ComponentFieldName(BattleQaTuningField.軍団隊形間隔), "実間隔でなく最小間隔の項目を指す");

            var bad = new[]
            {
                BattleQaTuningCatalog.ScaleOne(BattleQaTuningField.軍団隊形間隔, float.NaN),
                BattleQaTuningCatalog.ScaleOne(BattleQaTuningField.軍団隊形間隔, 0f),
                BattleQaTuningProfile.Default.With("x", BattleQaTuningField.軍団隊形間隔, BattleQaTuningOverride.Absolute(0f)),
                BattleQaTuningProfile.Default.With("x", BattleQaTuningField.軍団隊形間隔, BattleQaTuningOverride.Absolute(-7f)),
                BattleQaTuningProfile.Default.With("x", BattleQaTuningField.軍団隊形間隔, BattleQaTuningOverride.Absolute(float.PositiveInfinity)),
            };
            foreach (BattleQaTuningProfile b in bad)
                Assert.IsNotEmpty(b.Validate(), b.Describe() + " を拒否していない");

            bool listed = false;
            foreach (BattleQaTuningProfile c in BattleQaTuningCatalog.All())
                if (c.Get(BattleQaTuningField.軍団隊形間隔).IsSet) listed = true;
            Assert.IsTrue(listed, "比較プリセットに軍団隊形間隔が無い");
        }

        [Test]
        public void Validate_RejectsVersionMismatch()
        {
            var p = new BattleQaTuningProfile("旧版", BattleQaTuningProfile.CurrentVersion + 1, null);
            Assert.IsTrue(p.Validate().Exists(e => e.Contains("版")));
        }

        [Test]
        public void ValidateResolved_ChecksFiniteAndRange()
        {
            Assert.IsNull(BattleQaTuningProfile.ValidateResolved(BattleQaTuningField.移動速度, 3.5f));
            Assert.IsNotNull(BattleQaTuningProfile.ValidateResolved(BattleQaTuningField.移動速度, 0f));
            Assert.IsNotNull(BattleQaTuningProfile.ValidateResolved(BattleQaTuningField.回頭速度, float.PositiveInfinity));
            Assert.IsNotNull(BattleQaTuningProfile.ValidateResolved(BattleQaTuningField.士気回復量, float.NaN));
            Assert.IsNull(BattleQaTuningProfile.ValidateResolved(BattleQaTuningField.士気回復量, 0f));
            Assert.IsNotNull(BattleQaTuningProfile.ValidateResolved(BattleQaTuningField.敗走回復待ち, -0.01f));
            // 有限な倍率でも基準との積があふれたら拒否（適用時の検査）。
            float overflow = BattleQaTuningOverride.Scale(float.MaxValue).Resolve(10f);
            Assert.IsNotNull(BattleQaTuningProfile.ValidateResolved(BattleQaTuningField.移動速度, overflow));
        }

        [Test]
        public void Describe_ContainsNameVersionAndComponentField()
        {
            BattleQaTuningProfile p = BattleQaTuningCatalog.ScaleOne(BattleQaTuningField.回頭速度, 1.5f);
            string d = p.Describe();
            StringAssert.Contains(p.name, d);
            StringAssert.Contains("版" + BattleQaTuningProfile.CurrentVersion, d);
            StringAssert.Contains("FleetMovement.rotationSpeed", d);
            StringAssert.Contains("×1.5", d);
            StringAssert.Contains("全項目未指定", BattleQaTuningProfile.Default.Describe());
        }

        [Test]
        public void Catalog_FirstIsDefault_AndEachVariantIsSingleValidOverride()
        {
            List<BattleQaTuningProfile> all = BattleQaTuningCatalog.All();
            Assert.IsTrue(all[0].IsDefault);
            var names = new HashSet<string>();
            for (int i = 0; i < all.Count; i++)
            {
                Assert.IsTrue(names.Add(all[i].name), "設定名が重複：" + all[i].name);
                CollectionAssert.IsEmpty(all[i].Validate(), all[i].name);
                if (i > 0) Assert.AreEqual(1, all[i].OverrideCount, all[i].name + "：比較は1項目だけ");
            }
        }
    }
}
