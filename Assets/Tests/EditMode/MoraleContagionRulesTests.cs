using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>士気伝播（勝ち戦の高揚・敗報の動揺の伝染）の純ロジックを既定Paramsの具体値で固定するテスト。</summary>
    public class MoraleContagionRulesTests
    {
        const float Eps = 0.0001f;
        const float DivEps = 0.001f; // 除算箇所の緩めの許容

        /// <summary>伝播強度＝士気変化が 1/(1+0.5×距離) で代数減衰し符号を保つ。</summary>
        [Test]
        public void ContagionStrength_距離で代数減衰し符号を保つ()
        {
            // 1.0 / (1 + 0.5*2) = 0.5
            Assert.AreEqual(0.5f, MoraleContagionRules.ContagionStrength(1f, 2f), DivEps);
            // 距離0は源そのまま（負も保つ）
            Assert.AreEqual(-1f, MoraleContagionRules.ContagionStrength(-1f, 0f), Eps);
            // 0.6 / 1.5 = 0.4
            Assert.AreEqual(0.4f, MoraleContagionRules.ContagionStrength(0.6f, 1f), DivEps);
            // 離れるほど薄れる（単調）
            Assert.Greater(MoraleContagionRules.ContagionStrength(1f, 1f),
                           MoraleContagionRules.ContagionStrength(1f, 4f), "遠いほど伝播は弱い");
        }

        /// <summary>勝利の高揚＝局所勝利×0.6（既定）＝正の士気源。</summary>
        [Test]
        public void VictoryElation_勝利の大きさに比例した正の高揚()
        {
            Assert.AreEqual(0.6f, MoraleContagionRules.VictoryElation(1f), Eps);
            Assert.AreEqual(0.3f, MoraleContagionRules.VictoryElation(0.5f), Eps);
            Assert.Greater(MoraleContagionRules.VictoryElation(1f), 0f, "高揚は正");
        }

        /// <summary>敗報の動揺＝局所敗北×0.7（既定）の負値＝負の士気源。</summary>
        [Test]
        public void DefeatDismay_敗北の大きさに比例した負の動揺()
        {
            Assert.AreEqual(-0.7f, MoraleContagionRules.DefeatDismay(1f), Eps);
            Assert.AreEqual(-0.35f, MoraleContagionRules.DefeatDismay(0.5f), Eps);
            Assert.Less(MoraleContagionRules.DefeatDismay(1f), 0f, "動揺は負");
        }

        /// <summary>伝播＝隣の士気に伝播ぶんを足す（正で上がり負で下がる・両方向）。</summary>
        [Test]
        public void Propagate_隣接士気に両方向で伝わる()
        {
            Assert.AreEqual(0.7f, MoraleContagionRules.Propagate(0.5f, 0.2f), Eps);   // 高揚で上昇
            Assert.AreEqual(0.2f, MoraleContagionRules.Propagate(0.5f, -0.3f), Eps);  // 動揺で低下
            Assert.AreEqual(0f, MoraleContagionRules.Propagate(0.1f, -0.5f), Eps);    // 下限クランプ
            Assert.AreEqual(1f, MoraleContagionRules.Propagate(0.9f, 0.5f), Eps);     // 上限クランプ
        }

        /// <summary>結束は動揺（負）の伝染を弱め、高揚（正）はそのまま通す。</summary>
        [Test]
        public void CohesionResistance_固い部隊は動揺に抗うが高揚は通す()
        {
            // 負: -0.4 * (1 - 0.5*0.8) = -0.4*0.6 = -0.24
            Assert.AreEqual(-0.24f, MoraleContagionRules.CohesionResistance(0.5f, -0.4f), Eps);
            // 正はそのまま
            Assert.AreEqual(0.4f, MoraleContagionRules.CohesionResistance(0.5f, 0.4f), Eps);
            // 結束最大: -0.5 * (1-0.8) = -0.1
            Assert.AreEqual(-0.1f, MoraleContagionRules.CohesionResistance(1f, -0.5f), Eps);
            // 結束が固いほど動揺は弱まる（絶対値が小さい）
            float soft = MoraleContagionRules.CohesionResistance(0.2f, -0.5f);
            float hard = MoraleContagionRules.CohesionResistance(0.9f, -0.5f);
            Assert.Greater(hard, soft, "固いほど負の伝染が浅い");
        }

        /// <summary>威信ある指揮官は高揚を増幅し動揺を抑える。</summary>
        [Test]
        public void CommanderAmplification_威信が高揚を増し動揺を抑える()
        {
            // 正: 0.4 * (1 + 0.5*0.5) = 0.4*1.25 = 0.5
            Assert.AreEqual(0.5f, MoraleContagionRules.CommanderAmplification(0.5f, 0.4f), Eps);
            // 負: -0.4 * (1 - 0.5*0.5) = -0.4*0.75 = -0.3
            Assert.AreEqual(-0.3f, MoraleContagionRules.CommanderAmplification(0.5f, -0.4f), Eps);
            // 威信0なら素通り
            Assert.AreEqual(0.4f, MoraleContagionRules.CommanderAmplification(0f, 0.4f), Eps);
            Assert.AreEqual(-0.4f, MoraleContagionRules.CommanderAmplification(0f, -0.4f), Eps);
        }

        /// <summary>波＝震源から環の距離で減衰して届く（外側ほど薄い・伝播強度と同式）。</summary>
        [Test]
        public void WaveSpread_震源から同心円状に減衰して広がる()
        {
            // ring0 は震源そのまま
            Assert.AreEqual(-0.8f, MoraleContagionRules.WaveSpread(-0.8f, 0f), Eps);
            // -0.8 / (1+0.5*1) = -0.5333...
            Assert.AreEqual(-0.5333f, MoraleContagionRules.WaveSpread(-0.8f, 1f), DivEps);
            // 外環ほど浅い（単調・絶対値で比較）
            float inner = MoraleContagionRules.WaveSpread(-0.8f, 1f);
            float outer = MoraleContagionRules.WaveSpread(-0.8f, 3f);
            Assert.Greater(outer, inner, "外環ほど動揺が浅い");
        }

        /// <summary>有意判定＝伝播の絶対値が閾値を超えれば伝染とみなす（正負どちらでも）。</summary>
        [Test]
        public void IsContagionSignificant_絶対値が閾値超で有意()
        {
            Assert.IsTrue(MoraleContagionRules.IsContagionSignificant(0.3f, 0.2f));
            Assert.IsTrue(MoraleContagionRules.IsContagionSignificant(-0.3f, 0.2f)); // 負方向も
            Assert.IsFalse(MoraleContagionRules.IsContagionSignificant(0.1f, 0.2f));
            Assert.IsFalse(MoraleContagionRules.IsContagionSignificant(0.2f, 0.2f)); // 同値は不成立
        }

        /// <summary>
        /// 物語テスト：一角の勝利が高揚を広げ一角の崩壊が動揺を伝播するが、結束と威信がそれに抗う。
        /// </summary>
        [Test]
        public void 物語_高揚は広がり動揺は伝わるが結束と威信が抗う()
        {
            // --- 一角が勝つ：高揚が隣へ広がる ---
            float elation = MoraleContagionRules.VictoryElation(1f);                 // 0.6
            float winContagion = MoraleContagionRules.ContagionStrength(elation, 1f); // 0.6/1.5 = 0.4
            float allyBefore = 0.5f;
            float allyAfter = MoraleContagionRules.Propagate(allyBefore, winContagion);
            Assert.Greater(allyAfter, allyBefore, "勝利の高揚で隣接部隊の士気が上がる");

            // --- 一角が崩れる：動揺が隣へ伝播 ---
            float dismay = MoraleContagionRules.DefeatDismay(1f);                    // -0.7
            float lossContagion = MoraleContagionRules.ContagionStrength(dismay, 1f); // -0.7/1.5
            float naiveBefore = 0.5f;
            float naiveAfter = MoraleContagionRules.Propagate(naiveBefore, lossContagion);
            Assert.Less(naiveAfter, naiveBefore, "敗報の動揺で隣接部隊の士気が下がる");

            // --- 結束の固い部隊は同じ動揺に抗う（崩れが浅い） ---
            float resisted = MoraleContagionRules.CohesionResistance(0.9f, lossContagion);
            float steadyAfter = MoraleContagionRules.Propagate(naiveBefore, resisted);
            Assert.Greater(steadyAfter, naiveAfter, "結束が固いほど動揺の伝染に抗う");

            // --- 威信ある指揮官は動揺をさらに抑える ---
            float reassured = MoraleContagionRules.CommanderAmplification(0.8f, resisted);
            float ledAfter = MoraleContagionRules.Propagate(naiveBefore, reassured);
            Assert.Greater(ledAfter, steadyAfter, "威信ある指揮官が動揺を抑える");
            Assert.Less(ledAfter, naiveBefore, "それでも動揺は残り士気は完全には戻らない");

            // --- 威信は高揚を増幅もする（裏返し） ---
            float amplifiedWin = MoraleContagionRules.CommanderAmplification(0.8f, winContagion);
            Assert.Greater(amplifiedWin, winContagion, "威信ある指揮官は高揚を増幅する");
        }
    }
}
