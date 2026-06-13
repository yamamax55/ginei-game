using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 政体腐化の純ロジック（MONT-2 #1440）のテスト。政体類型ごとの固有の腐敗経路＝共和政の徳の喪失、
    /// 君主政の中間権力の破壊、専制政の恣意の極まりと、堕落の標的・速度・引き返せる窓・末期判定を担保する。
    /// </summary>
    public class PolityCorruptionRulesTests
    {
        const float Eps = 0.0001f;

        /// <summary>共和政＝徳の喪失と不平等で腐敗する（既定値で具体値固定・両極端で0と1）。</summary>
        [Test]
        public void RepublicCorruption_徳の喪失と不平等で腐敗()
        {
            // 0.5*0.6 + 0.5*0.5*0.4 = 0.3 + 0.1 = 0.4
            Assert.AreEqual(0.4f, PolityCorruptionRules.RepublicCorruption(0.5f, 0.5f), Eps);
            Assert.AreEqual(0f, PolityCorruptionRules.RepublicCorruption(0f, 1f), Eps); // 徳が無事なら腐敗0
            Assert.AreEqual(1f, PolityCorruptionRules.RepublicCorruption(1f, 1f), Eps); // 徳全喪失×極端不平等で全腐敗
        }

        /// <summary>君主政＝中間権力の破壊と法の侵食で専制へ堕ちる（緩衝喪失で暴君化）。</summary>
        [Test]
        public void MonarchyCorruption_中間権力の破壊で専制へ()
        {
            // 0.8*0.55 + 0.4*0.25 + 0.8*0.4*0.2 = 0.44 + 0.1 + 0.064 = 0.604
            Assert.AreEqual(0.604f, PolityCorruptionRules.MonarchyCorruption(0.8f, 0.4f), Eps);
            Assert.AreEqual(1f, PolityCorruptionRules.MonarchyCorruption(1f, 1f), Eps);
            // 中間権力が無事なら法が侵食されても腐敗は緩い（0.25のみ）＝緩衝が王を縛る。
            Assert.AreEqual(0.25f, PolityCorruptionRules.MonarchyCorruption(0f, 1f), Eps);
        }

        /// <summary>専制政＝恣意の極まりと恐怖の枯渇で崩壊する（恐怖が効かなくなれば統治不能）。</summary>
        [Test]
        public void DespotismDecay_恣意と恐怖の枯渇で崩壊()
        {
            // 0.6*0.5 + 0.5*0.3 + 0.6*0.5*0.2 = 0.3 + 0.15 + 0.06 = 0.51
            Assert.AreEqual(0.51f, PolityCorruptionRules.DespotismDecay(0.6f, 0.5f), Eps);
            Assert.AreEqual(1f, PolityCorruptionRules.DespotismDecay(1f, 1f), Eps);
            Assert.AreEqual(0f, PolityCorruptionRules.DespotismDecay(0f, 0f), Eps);
        }

        /// <summary>型別経路＝同じストレスでも政体ごとに腐敗の式が違う（共和政は固有経路へ写す）。</summary>
        [Test]
        public void CorruptionPath_型別経路で政体ごとに異なる()
        {
            // 共和政: RepublicCorruption(0.5,0.5) = 0.4
            Assert.AreEqual(0.4f, PolityCorruptionRules.CorruptionPath(PolityType.共和政, 0.5f), Eps);
            // 君主政: MonarchyCorruption(0.5,0.5) = 0.5*0.55+0.5*0.25+0.5*0.5*0.2 = 0.275+0.125+0.05 = 0.45
            Assert.AreEqual(0.45f, PolityCorruptionRules.CorruptionPath(PolityType.君主政, 0.5f), Eps);
            // 専制政: DespotismDecay(0.5,0.5) = 0.25+0.15+0.05 = 0.45
            Assert.AreEqual(0.45f, PolityCorruptionRules.CorruptionPath(PolityType.専制政, 0.5f), Eps);
            // 政体ごとに腐敗の型が異なる＝共和政と君主政で同ストレスでも値が違う。
            Assert.AreNotEqual(
                PolityCorruptionRules.CorruptionPath(PolityType.共和政, 0.5f),
                PolityCorruptionRules.CorruptionPath(PolityType.君主政, 0.5f));
        }

        /// <summary>堕落の向かう先＝共和→寡頭/衆愚（不平等で分岐）・君主→専制・専制→崩壊。</summary>
        [Test]
        public void DegenerateTarget_政体ごとの堕落の標的()
        {
            var p = PolityCorruptionParams.Default; // oligarchyTilt=0.5
            // 共和政：高不平等で寡頭政、低不平等で衆愚政。
            Assert.AreEqual(DegenerationKind.寡頭政,
                PolityCorruptionRules.DegenerateTarget(PolityType.共和政, 0.8f, p));
            Assert.AreEqual(DegenerationKind.衆愚政,
                PolityCorruptionRules.DegenerateTarget(PolityType.共和政, 0.2f, p));
            // 君主政→専制・専制政→崩壊。
            Assert.AreEqual(DegenerationKind.専制政,
                PolityCorruptionRules.DegenerateTarget(PolityType.君主政, 0.5f, p));
            Assert.AreEqual(DegenerationKind.崩壊,
                PolityCorruptionRules.DegenerateTarget(PolityType.専制政, 0.5f, p));
            // 簡易窓口（不平等0.5＝傾き0.5以上で寡頭政）。
            Assert.AreEqual(DegenerationKind.寡頭政,
                PolityCorruptionRules.DegenerateTarget(PolityType.共和政));
        }

        /// <summary>腐敗速度＝歯止めが無いほど速く、専制政が最も速く崩れる（型ごとに固有速度）。</summary>
        [Test]
        public void CorruptionVelocity_歯止めなしほど速く型ごとに違う()
        {
            // 専制政・歯止め0: 0.1*1.4*1*2 = 0.28
            Assert.AreEqual(0.28f, PolityCorruptionRules.CorruptionVelocity(PolityType.専制政, 0f), Eps);
            // 君主政・歯止め0.5: 0.1*0.8*0.5*2 = 0.08
            Assert.AreEqual(0.08f, PolityCorruptionRules.CorruptionVelocity(PolityType.君主政, 0.5f), Eps);
            // 専制政は共和政より速く崩れる（同条件）。
            Assert.Greater(
                PolityCorruptionRules.CorruptionVelocity(PolityType.専制政, 0.3f),
                PolityCorruptionRules.CorruptionVelocity(PolityType.共和政, 0.3f));
            // 歯止めが完全なら腐敗速度0。
            Assert.AreEqual(0f, PolityCorruptionRules.CorruptionVelocity(PolityType.共和政, 1f), Eps);
        }

        /// <summary>引き返せる窓＝腐敗が進む前なら改革で戻れるが、不可逆点を越えると窓は閉じる。</summary>
        [Test]
        public void ReversibilityWindow_進む前なら戻れるが手遅れで不可逆()
        {
            // 進行0.35・改革1.0: room=(0.7-0.35)/0.7=0.5 ×1 = 0.5
            Assert.AreEqual(0.5f, PolityCorruptionRules.ReversibilityWindow(0.35f, 1f), Eps);
            // 不可逆点(0.7)に達したら窓は0＝手遅れ。
            Assert.AreEqual(0f, PolityCorruptionRules.ReversibilityWindow(0.7f, 1f), Eps);
            Assert.AreEqual(0f, PolityCorruptionRules.ReversibilityWindow(0.9f, 1f), Eps);
            // 改革能力が無ければ窓も無い。
            Assert.AreEqual(0f, PolityCorruptionRules.ReversibilityWindow(0.1f, 0f), Eps);
        }

        /// <summary>末期腐敗判定＝腐敗進行が閾値以上で固有の堕落が確定する段階。</summary>
        [Test]
        public void IsTerminalCorruption_閾値以上で末期()
        {
            // 既定末期閾値=0.85
            Assert.IsTrue(PolityCorruptionRules.IsTerminalCorruption(0.9f, PolityType.専制政));
            Assert.IsFalse(PolityCorruptionRules.IsTerminalCorruption(0.5f, PolityType.共和政));
            // 明示閾値版（ちょうど閾値で末期）。
            Assert.IsTrue(PolityCorruptionRules.IsTerminalCorruption(0.6f, PolityType.君主政, 0.6f));
            Assert.IsFalse(PolityCorruptionRules.IsTerminalCorruption(0.59f, PolityType.君主政, 0.6f));
        }
    }
}
