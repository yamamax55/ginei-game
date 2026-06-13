using NUnit.Framework;
using Ginei;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>専用旗艦名：既定表/明示指定の解決と、ShipNameRegistry での指名払い出し（愛着の基盤）。</summary>
    public class SignatureShipRegistryTests
    {
        [SetUp]
        public void Setup() { ShipNameRegistry.Clear(); SignatureShipRegistry.ResetToDefaults(); }

        [TearDown]
        public void Cleanup() { ShipNameRegistry.Clear(); SignatureShipRegistry.ResetToDefaults(); }

        [Test]
        public void Defaults_KnownAdmiralsHaveSignatureShips()
        {
            Assert.AreEqual("ヒューベリオン", SignatureShipRegistry.ResolveByName("ヤン・ウェンリー"));
            Assert.AreEqual("リオグランデ", SignatureShipRegistry.ResolveByName("ビュコック"));
            Assert.AreEqual("ブリュンヒルト", SignatureShipRegistry.ResolveByName("ラインハルト"));
            Assert.IsTrue(SignatureShipRegistry.HasSignature("ヤン"));
            Assert.AreEqual("", SignatureShipRegistry.ResolveByName("名もなき提督"));
        }

        [Test]
        public void Resolve_PrefersExplicitFieldOverDefaultTable()
        {
            var admiral = ScriptableObject.CreateInstance<AdmiralData>();
            admiral.admiralName = "ヤン・ウェンリー";
            // 明示指定なし → 既定表（ヒューベリオン）
            Assert.AreEqual("ヒューベリオン", SignatureShipRegistry.Resolve(admiral));
            // 明示指定あり → そちらが優先
            admiral.signatureShipName = "アルテミス";
            Assert.AreEqual("アルテミス", SignatureShipRegistry.Resolve(admiral));
            Assert.AreEqual("", SignatureShipRegistry.Resolve(null));
        }

        [Test]
        public void Register_AddsOrOverridesDefault()
        {
            SignatureShipRegistry.Register("メルカッツ", "ヒューベリオン");
            Assert.AreEqual("ヒューベリオン", SignatureShipRegistry.ResolveByName("メルカッツ"));
            SignatureShipRegistry.Register("メルカッツ", "ランズベルク");
            Assert.AreEqual("ランズベルク", SignatureShipRegistry.ResolveByName("メルカッツ"));
        }

        [Test]
        public void TryAssignSpecific_ClaimsNameThenBlocksReuse()
        {
            // 専用旗艦名（プール外）を指名で払い出せる。
            Assert.IsTrue(ShipNameRegistry.TryAssignSpecific("ヒューベリオン"));
            Assert.IsTrue(ShipNameRegistry.IsInUse("ヒューベリオン"));
            // 使用中は二重に取れない（別の艦が同名にならない）。
            Assert.IsFalse(ShipNameRegistry.TryAssignSpecific("ヒューベリオン"));
            // 返却すれば再び取れる。
            ShipNameRegistry.Release("ヒューベリオン");
            Assert.IsTrue(ShipNameRegistry.TryAssignSpecific("ヒューベリオン"));
            // 撃沈＝永久欠番なら以後取れない。
            ShipNameRegistry.Retire("ヒューベリオン");
            Assert.IsFalse(ShipNameRegistry.TryAssignSpecific("ヒューベリオン"));
        }
    }
}
