using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 惑星攻城（#131 PB-3〜PB-7）の純ロジックを固定する：
    /// 制空権(ピラー・ドメイン)が健在な間は接近不可→S-AVで段階制圧→ドメイン・ダウン→
    /// 侵略値を蓄積→閾値で占領（所有フリップ）。二段階・1tick一段階・遷移の単発性。
    /// </summary>
    public class PlanetSiegeTests
    {
        // 制空権10・侵略閾値10・帝国所有の惑星
        private Planet NewPlanet() => new Planet(systemId: 1, owner: Faction.帝国,
            maxOrbitalDefense: 10f, invasionThreshold: 10f);

        [Test]
        public void DomainUp_BlocksFleetApproach_DownAllows()
        {
            var p = NewPlanet();
            Assert.IsFalse(p.DomainDown);
            Assert.IsTrue(p.FleetApproachBlocked); // 制空権健在＝接近限界で止まる

            p.orbitalDefense = 0f;
            Assert.IsTrue(p.DomainDown);
            Assert.IsFalse(p.FleetApproachBlocked); // ドメイン・ダウンで接近解禁
        }

        [Test]
        public void Suppress_ReducesDefense_DomainDownReportedOnce()
        {
            var p = NewPlanet();

            // S-AV戦力5で抑制（既定係数：5*1*1=5/ tick）
            var r1 = PlanetSiegeRules.Tick(p, Faction.同盟, attackerSAV: 5f, deltaTime: 1f);
            Assert.AreEqual(5f, p.orbitalDefense, 1e-4f);
            Assert.IsFalse(r1.domainWentDown);
            Assert.AreEqual(0f, p.invasionProgress, 1e-4f); // ドメイン健在中は侵略しない

            // 2tick目で 0 ＝ドメイン・ダウン（単発で報告）
            var r2 = PlanetSiegeRules.Tick(p, Faction.同盟, 5f, 1f);
            Assert.IsTrue(p.DomainDown);
            Assert.IsTrue(r2.domainWentDown);
            Assert.AreEqual(0f, p.invasionProgress, 1e-4f); // ダウンしたtickはまだ侵略しない（1tick一段階）

            // 3tick目以降は再度 down 報告しない
            var r3 = PlanetSiegeRules.Tick(p, Faction.同盟, 5f, 1f);
            Assert.IsFalse(r3.domainWentDown);
        }

        [Test]
        public void AfterDomainDown_InvasionAccumulates_CaptureFlipsOwner()
        {
            var p = NewPlanet();
            p.orbitalDefense = 0f; // 既にドメイン・ダウン

            var a = PlanetSiegeRules.Tick(p, Faction.同盟, attackerSAV: 5f, deltaTime: 1f);
            Assert.AreEqual(5f, p.invasionProgress, 1e-4f);
            Assert.IsFalse(a.captured);
            Assert.AreEqual(Faction.帝国, p.owner); // まだ陥落していない

            var b = PlanetSiegeRules.Tick(p, Faction.同盟, 5f, 1f);
            Assert.IsTrue(p.Captured);
            Assert.IsTrue(b.captured);             // 占領の遷移は単発
            Assert.AreEqual(Faction.同盟, p.owner); // 所有が攻撃側へフリップ

            // さらに叩いても二重に captured 報告しない
            var c = PlanetSiegeRules.Tick(p, Faction.同盟, 5f, 1f);
            Assert.IsFalse(c.captured);
        }

        [Test]
        public void NoAttacker_RegensDefense_NotAboveMax()
        {
            var p = NewPlanet();
            p.orbitalDefense = 4f;
            var prm = new SiegeParams(suppressRate: 1f, invadeRate: 1f, defenseRegen: 3f);

            PlanetSiegeRules.Tick(p, Faction.同盟, attackerSAV: 0f, deltaTime: 1f, prm);
            Assert.AreEqual(7f, p.orbitalDefense, 1e-4f); // 4+3 再建

            PlanetSiegeRules.Tick(p, Faction.同盟, 0f, 10f, prm);
            Assert.AreEqual(10f, p.orbitalDefense, 1e-4f); // max を超えない
        }

        [Test]
        public void NoAttacker_DoesNotRegen_AfterDomainDown()
        {
            var p = NewPlanet();
            p.orbitalDefense = 0f; // ダウン済み
            var prm = new SiegeParams(1f, 1f, defenseRegen: 5f);

            PlanetSiegeRules.Tick(p, Faction.同盟, 0f, 1f, prm);
            Assert.AreEqual(0f, p.orbitalDefense, 1e-4f); // ダウン後は再建しない（制圧された制空権は戻らない）
        }

        // ===== 以下、敵対的エッジケース追加 =====

        /// <summary>null 惑星でも例外を投げず default(遷移なし)を返す。</summary>
        [Test]
        public void Tick_NullPlanet_ReturnsDefault_NoThrow()
        {
            var r = PlanetSiegeRules.Tick(null, Faction.同盟, 100f, 1f);
            Assert.IsFalse(r.domainWentDown);
            Assert.IsFalse(r.captured);
        }

        /// <summary>deltaTime=0 は何も進めない（guard：dt&lt;=0 で default）。状態は不変。</summary>
        [Test]
        public void Tick_ZeroDeltaTime_NoMutation()
        {
            var p = NewPlanet();
            float def0 = p.orbitalDefense;
            var r = PlanetSiegeRules.Tick(p, Faction.同盟, 100f, 0f);
            Assert.AreEqual(def0, p.orbitalDefense, 1e-5f); // 抑制されない
            Assert.AreEqual(0f, p.invasionProgress, 1e-5f);
            Assert.IsFalse(r.domainWentDown);
            Assert.IsFalse(r.captured);
        }

        /// <summary>負の deltaTime も guard(dt&lt;=0)で弾く＝時間逆行で防衛が回復したりしない。</summary>
        [Test]
        public void Tick_NegativeDeltaTime_NoMutation()
        {
            var p = NewPlanet();
            p.orbitalDefense = 5f;
            float def0 = p.orbitalDefense;
            var r = PlanetSiegeRules.Tick(p, Faction.同盟, 100f, -3f);
            Assert.AreEqual(def0, p.orbitalDefense, 1e-5f); // 変化なし（負時間で増減しない）
            Assert.IsFalse(r.domainWentDown);
            Assert.IsFalse(r.captured);
        }

        /// <summary>
        /// 負の attackerSAV は「非交戦(&lt;=0)」分岐へ。regen 無し(既定)なら制空権は不変＝
        /// 負戦力で def を *増やしてしまう* 退行が無いこと（def - 負*dt = def+ になる罠を防ぐ）。
        /// </summary>
        [Test]
        public void Tick_NegativeAttackerSAV_TreatedAsNonAttacking_NoDefenseIncrease()
        {
            var p = NewPlanet();
            p.orbitalDefense = 6f;
            // 既定係数は defenseRegen=0 なので非交戦扱いでも回復しない＝6 のまま
            var r = PlanetSiegeRules.Tick(p, Faction.同盟, attackerSAV: -100f, deltaTime: 1f);
            Assert.AreEqual(6f, p.orbitalDefense, 1e-5f); // 増えも減りもしない
            Assert.AreEqual(0f, p.invasionProgress, 1e-5f);
            Assert.IsFalse(r.domainWentDown);
        }

        /// <summary>
        /// 過大な S-AV で 1tick の抑制が max を超えても 0 でクランプ（負の制空権にならない）。
        /// 1tick一段階＝同tickでは侵略値は進まず、domainWentDown のみ単発で立つ。
        /// </summary>
        [Test]
        public void Tick_HugeSAV_ClampsDefenseAtZero_OnlyDomainDownThisTick()
        {
            var p = NewPlanet(); // 制空権10
            var r = PlanetSiegeRules.Tick(p, Faction.同盟, attackerSAV: 9999f, deltaTime: 1f);
            Assert.AreEqual(0f, p.orbitalDefense, 1e-5f); // 0 で止まる（負にならない）
            Assert.IsTrue(p.DomainDown);
            Assert.IsTrue(r.domainWentDown);
            Assert.IsFalse(r.captured);                   // 同tickでは侵略しない（一段階）
            Assert.AreEqual(0f, p.invasionProgress, 1e-5f);
        }

        /// <summary>
        /// コロニー(maxOrbitalDefense=0)は生成時から DomainDown。最初の Tick は即・侵攻段階へ入り、
        /// wasDown が既に true なので domainWentDown は立たない（"落ちる瞬間"が無い）。
        /// </summary>
        [Test]
        public void Colony_StartsDown_FirstTickInvadesWithoutDomainDownSignal()
        {
            var c = PlanetSiegeRules.CreateTarget(systemId: 9, owner: Faction.帝国,
                kind: Planet.SiegeTargetKind.コロニー);
            Assert.IsTrue(c.DomainDown);                  // 制空権0＝最初からダウン
            Assert.IsFalse(c.FleetApproachBlocked);       // 接近限界なし

            var r = PlanetSiegeRules.Tick(c, Faction.同盟, attackerSAV: 5f, deltaTime: 1f);
            Assert.AreEqual(5f, c.invasionProgress, 1e-5f); // 即・侵攻が進む
            Assert.IsFalse(r.domainWentDown);             // 既にダウン済み＝信号は立たない
            Assert.IsFalse(r.captured);                   // コロニー侵略閾値18 未満
        }

        /// <summary>
        /// CreateTarget の規模差プロファイル（PB-6）。要塞＞惑星＞コロニーの制空権/侵略閾値を仕様値で固定。
        /// </summary>
        [Test]
        public void CreateTarget_AppliesScaleProfileByKind()
        {
            var fortress = PlanetSiegeRules.CreateTarget(1, Faction.帝国, Planet.SiegeTargetKind.要塞);
            Assert.AreEqual(180f, fortress.maxOrbitalDefense, 1e-5f);
            Assert.AreEqual(180f, fortress.orbitalDefense, 1e-5f); // 初期=最大
            Assert.AreEqual(60f, fortress.invasionThreshold, 1e-5f);
            Assert.IsTrue(fortress.FleetApproachBlocked);

            var planet = PlanetSiegeRules.CreateTarget(2, Faction.帝国, Planet.SiegeTargetKind.惑星);
            Assert.AreEqual(100f, planet.maxOrbitalDefense, 1e-5f);
            Assert.AreEqual(40f, planet.invasionThreshold, 1e-5f);

            var colony = PlanetSiegeRules.CreateTarget(3, Faction.帝国, Planet.SiegeTargetKind.コロニー);
            Assert.AreEqual(0f, colony.maxOrbitalDefense, 1e-5f);
            // コロニーの侵略閾値18 は >0 なのでコンストラクタの 0.0001 クランプには掛からない
            Assert.AreEqual(18f, colony.invasionThreshold, 1e-5f);
        }

        /// <summary>
        /// 占領は threshold "以上" で成立（&gt;=、オフバイワン確認）。
        /// 残り 5 で侵略5を入れて丁度 invasionProgress==invasionThreshold ＝占領成立。
        /// </summary>
        [Test]
        public void Capture_TriggersExactlyAtThreshold_Inclusive()
        {
            var p = NewPlanet(); // 侵略閾値10
            p.orbitalDefense = 0f;
            p.invasionProgress = 5f;
            var r = PlanetSiegeRules.Tick(p, Faction.同盟, attackerSAV: 5f, deltaTime: 1f);
            Assert.AreEqual(10f, p.invasionProgress, 1e-5f); // 丁度 threshold
            Assert.IsTrue(p.Captured);                       // >= なので成立
            Assert.IsTrue(r.captured);
            Assert.AreEqual(Faction.同盟, p.owner);
        }

        /// <summary>
        /// 攻撃側が現所有者と同一でも占領フリップは owner=attacker を実行（自勢力で塗り替え＝無害だが分岐を通す）。
        /// captured 遷移は単発で立つ。
        /// </summary>
        [Test]
        public void Capture_SelfSiege_FlipsToSameOwner_StillSingleEvent()
        {
            var p = NewPlanet();
            p.orbitalDefense = 0f;
            p.invasionProgress = 9f;
            var r = PlanetSiegeRules.Tick(p, Faction.帝国, attackerSAV: 5f, deltaTime: 1f); // 攻撃側=現所有者
            Assert.IsTrue(r.captured);
            Assert.AreEqual(Faction.帝国, p.owner);
        }

        /// <summary>
        /// 単調性：制圧中(domain健在)は orbitalDefense は決して増えない／
        /// 侵攻中(ダウン後)は invasionProgress は決して減らない。連続tickで検証。
        /// </summary>
        [Test]
        public void Monotonicity_DefenseNonIncreasing_InvasionNonDecreasing_WhileAttacking()
        {
            // 制圧フェーズ：defense 単調減少
            var p = new Planet(1, Faction.帝国, maxOrbitalDefense: 100f, invasionThreshold: 1000f);
            float prevDef = p.orbitalDefense;
            for (int i = 0; i < 5; i++)
            {
                PlanetSiegeRules.Tick(p, Faction.同盟, attackerSAV: 7f, deltaTime: 1f);
                Assert.LessOrEqual(p.orbitalDefense, prevDef + 1e-5f);
                prevDef = p.orbitalDefense;
            }

            // 侵攻フェーズ：invasion 単調増加
            var q = new Planet(2, Faction.帝国, maxOrbitalDefense: 0f, invasionThreshold: 1000f);
            float prevInv = q.invasionProgress;
            for (int i = 0; i < 5; i++)
            {
                PlanetSiegeRules.Tick(q, Faction.同盟, attackerSAV: 7f, deltaTime: 1f);
                Assert.GreaterOrEqual(q.invasionProgress, prevInv - 1e-5f);
                prevInv = q.invasionProgress;
            }
        }

        /// <summary>
        /// 制圧中(domain健在)は侵略値が一切進まない（フェーズ分離の不変条件）。
        /// 大量SAVでも、抑制が 0 に達するまでの間 invasionProgress は据え置き。
        /// </summary>
        [Test]
        public void DuringSuppression_InvasionProgressStaysZero()
        {
            var p = new Planet(1, Faction.帝国, maxOrbitalDefense: 100f, invasionThreshold: 40f);
            // 30抑制／tick：3tickでは未ダウン（残り10）
            for (int i = 0; i < 3; i++)
                PlanetSiegeRules.Tick(p, Faction.同盟, attackerSAV: 30f, deltaTime: 1f);
            Assert.AreEqual(10f, p.orbitalDefense, 1e-5f);
            Assert.IsFalse(p.DomainDown);
            Assert.AreEqual(0f, p.invasionProgress, 1e-5f); // 制圧中は侵略しない
        }

        /// <summary>
        /// regen の上限クランプの両端：既に max のとき非交戦で叩いても max を超えない。
        /// </summary>
        [Test]
        public void Regen_AtMax_DoesNotExceedMax()
        {
            var p = NewPlanet(); // 既に max(10)
            Assert.AreEqual(10f, p.orbitalDefense, 1e-5f);
            var prm = new SiegeParams(1f, 1f, defenseRegen: 100f);
            PlanetSiegeRules.Tick(p, Faction.同盟, attackerSAV: 0f, deltaTime: 5f, prm);
            Assert.AreEqual(10f, p.orbitalDefense, 1e-5f); // 超えない
        }
    }
}
