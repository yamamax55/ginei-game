using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// SchismRules（教団分裂＝宗教・思想組織の分派と内部対立）の純ロジック検証。
    /// 既定 Params（教義鋭さ1.2/争い飽和3.0/争奪鋭さ0.5/和解効き0.7）の具体値で期待値を固定し、
    /// 教義対立・指導権争い・分派の形成・正統性の争奪・組織の弱体化・和解の見込み・分裂の深刻さ・
    /// 決定的か（修復不能）の判定を担保する。物語テストで分派独立までの一連の流れも追う。
    /// </summary>
    public class SchismRulesTests
    {
        private const float Eps = 1e-4f;
        private const float PowEps = 1e-3f; // Pow/Sqrt を含む箇所

        /// <summary>教義対立＝解釈の隔たり×硬直。Pow で鋭さを掛けた具体値を固定。</summary>
        [Test]
        public void DoctrinalDispute_既定Paramsで具体値()
        {
            // gap0.8,rigid0.7 → Lerp(0.4,1,0.7)=0.82 → core=0.656 → Pow(0.656,1/1.2)≈0.7039
            Assert.AreEqual(0.7039f, SchismRules.DoctrinalDispute(0.8f, 0.7f), PowEps);
            // 隔たり0 なら対立0
            Assert.AreEqual(0f, SchismRules.DoctrinalDispute(0f, 1f), Eps);
            // 入力は clamp（過大入力でも 0..1）
            Assert.AreEqual(1f, SchismRules.DoctrinalDispute(5f, 5f), Eps);
        }

        /// <summary>指導権争い＝後継候補数×野心。候補1名以下なら争い無し、複数で飽和。</summary>
        [Test]
        public void LeadershipRivalry_候補数と野心で増し単独はゼロ()
        {
            // n=4 → rivals=3 → 3/6=0.5 → Lerp(0.3,1,0.6)=0.72 → 0.36
            Assert.AreEqual(0.36f, SchismRules.LeadershipRivalry(4, 0.6f), Eps);
            // 単独後継（n=1）は争い無し
            Assert.AreEqual(0f, SchismRules.LeadershipRivalry(1, 1f), Eps);
            // 負の候補数も clamp（0扱い）で争い無し
            Assert.AreEqual(0f, SchismRules.LeadershipRivalry(-3, 1f), Eps);
        }

        /// <summary>分派の形成＝教義対立×指導権争い。両輪が揃うほど結晶化（積が主因）。</summary>
        [Test]
        public void FactionFormation_両輪が揃うと割れる()
        {
            // doc0.6,riv0.4 → both=0.24*0.7=0.168, either=0.6*0.3=0.18 → 0.348
            Assert.AreEqual(0.348f, SchismRules.FactionFormation(0.6f, 0.4f), Eps);
            // 片方ゼロなら either の弱い底上げのみ（0.7*0+0.3*0.8=0.24）
            Assert.AreEqual(0.24f, SchismRules.FactionFormation(0.8f, 0f), Eps);
        }

        /// <summary>正統性の争奪＝正統×改革の主張。拮抗（接戦）ほど深刻に。</summary>
        [Test]
        public void LegitimacyContest_拮抗ほど深刻()
        {
            // ortho0.8,reform0.6 → intensity=0.48, balance=0.8 → Lerp(1,0.8,0.5)=0.9 → 0.432
            Assert.AreEqual(0.432f, SchismRules.LegitimacyContest(0.8f, 0.6f), Eps);
            // 完全拮抗（共に0.5）→ intensity=0.25, balance=1 → weighted=1 → 0.25
            Assert.AreEqual(0.25f, SchismRules.LegitimacyContest(0.5f, 0.5f), Eps);
        }

        /// <summary>組織の弱体化＝分派×資源分散。分派が無ければ資源が割れても弱らない。</summary>
        [Test]
        public void OrganizationalWeakening_分派が主因()
        {
            // faction0.5,split0.8 → Lerp(0.5,1,0.8)=0.9 → 0.45
            Assert.AreEqual(0.45f, SchismRules.OrganizationalWeakening(0.5f, 0.8f), Eps);
            // 分派ゼロなら弱体化ゼロ
            Assert.AreEqual(0f, SchismRules.OrganizationalWeakening(0f, 1f), Eps);
        }

        /// <summary>和解の見込み＝共通の核×調停努力。幾何平均（核が薄いと調停が空回り）。</summary>
        [Test]
        public void ReconciliationChance_共通の核と調停の幾何平均()
        {
            // core0.64,effort0.81 → Sqrt(0.5184)=0.72
            Assert.AreEqual(0.72f, SchismRules.ReconciliationChance(0.64f, 0.81f), PowEps);
            // 共通の核ゼロなら和解の見込みゼロ
            Assert.AreEqual(0f, SchismRules.ReconciliationChance(0f, 1f), Eps);
        }

        /// <summary>分裂の深刻さ＝分派−和解（和解効きを掛ける）。-1..1 に clamp。</summary>
        [Test]
        public void SchismSeverity_分派から和解を引く()
        {
            // faction0.8, reconcile0.5 → 0.8 - 0.5*0.7 = 0.45
            Assert.AreEqual(0.45f, SchismRules.SchismSeverity(0.8f, 0.5f), Eps);
            // 和解が分派を上回れば負（結束へ）：0.2 - 1*0.7 = -0.5
            Assert.AreEqual(-0.5f, SchismRules.SchismSeverity(0.2f, 1f), Eps);
        }

        /// <summary>決定的（修復不能）判定＝深刻さが閾値以上か。既定閾値0.6。</summary>
        [Test]
        public void IsSchismTerminal_閾値で決裂判定()
        {
            Assert.IsFalse(SchismRules.IsSchismTerminal(0.45f));          // 既定0.6 未満
            Assert.IsTrue(SchismRules.IsSchismTerminal(0.7f));           // 既定0.6 以上＝決裂
            Assert.IsTrue(SchismRules.IsSchismTerminal(0.6f));           // 境界は決裂側
            Assert.IsTrue(SchismRules.IsSchismTerminal(0.3f, 0.3f));     // 明示閾値
            Assert.IsFalse(SchismRules.IsSchismTerminal(0.3f, 0.5f));    // 明示閾値未満
        }

        /// <summary>
        /// 物語：教義の隔たりと後継者争いが分派を生み、調停で和解を試みるが核が薄く決裂、分派が独立する。
        /// 一連の関数を繋いで分裂の深刻さが閾値を超え修復不能になることを追う。
        /// </summary>
        [Test]
        public void Story_教義対立から分派独立まで()
        {
            // 深い教義対立（解釈の隔たり0.9・硬直0.9）
            float dispute = SchismRules.DoctrinalDispute(0.9f, 0.9f);
            Assert.Greater(dispute, 0.6f);

            // 後継者が乱立（7名）し野心も極めて高い → 激しい指導権争い
            float rivalry = SchismRules.LeadershipRivalry(7, 0.95f);
            Assert.Greater(rivalry, 0.5f);

            // 両輪が揃い分派が結晶化
            float faction = SchismRules.FactionFormation(dispute, rivalry);
            Assert.Greater(faction, 0.6f);

            // 改革派が正統と拮抗して正統性を争奪
            float contest = SchismRules.LegitimacyContest(0.8f, 0.8f);
            Assert.Greater(contest, 0.4f);

            // 資源が割れ組織が弱体化
            float weaken = SchismRules.OrganizationalWeakening(faction, 0.8f);
            Assert.Greater(weaken, 0.4f);

            // 共通の核が失われ（0.0）調停も拒まれる → 和解の見込みは皆無
            float reconcile = SchismRules.ReconciliationChance(0f, 0.3f);
            Assert.AreEqual(0f, reconcile, Eps);

            // 分派が和解を大きく上回り深刻（和解ゼロなので severity≈faction）
            float severity = SchismRules.SchismSeverity(faction, reconcile);
            Assert.Greater(severity, 0.6f);

            // 決定的＝分派が独立（修復不能・既定閾値0.6 以上）
            Assert.IsTrue(SchismRules.IsSchismTerminal(severity));

            // 反実仮想：太い共通の核（0.9）と十分な調停（0.9）なら和解が勝り結束へ向かう
            float reconcileHigh = SchismRules.ReconciliationChance(0.9f, 0.9f);
            float severityHealed = SchismRules.SchismSeverity(0.4f, reconcileHigh);
            Assert.Less(severityHealed, 0f);
            Assert.IsFalse(SchismRules.IsSchismTerminal(severityHealed));
        }
    }
}
