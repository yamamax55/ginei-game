using UnityEngine;

namespace Ginei
{
    /// <summary>分割統治（Divide et Impera）の調整係数。</summary>
    public readonly struct DivideParams
    {
        /// <summary>標的の不満が懐柔効果を増幅する強さ（不満が大きいほど和解に乗る＝0で増幅なし）。</summary>
        public readonly float grievanceLeverage;
        /// <summary>連合の亀裂が離反誘発を増幅する強さ（亀裂が大きいほど割りやすい＝0で増幅なし）。</summary>
        public readonly float faultlineLeverage;
        /// <summary>強硬派の孤立が各個撃破に効く度合いのスケール（割れた残党がどれだけ無力化されるか）。</summary>
        public readonly float isolationScale;
        /// <summary>買収コストの基準額（強く不満の小さい部族ほどこの基準に近く高くつく）。</summary>
        public readonly float baseBribeCost;
        /// <summary>分断工作露見が連合を逆に結束させる逆効果の強さ（露見プレミアム）。</summary>
        public readonly float backlashScale;
        /// <summary>連合が割れたと判定する分断度の既定閾値（これを超えると分断成立）。</summary>
        public readonly float divideThreshold;

        public DivideParams(float grievanceLeverage, float faultlineLeverage, float isolationScale,
            float baseBribeCost, float backlashScale, float divideThreshold)
        {
            this.grievanceLeverage = Mathf.Max(0f, grievanceLeverage);
            this.faultlineLeverage = Mathf.Max(0f, faultlineLeverage);
            this.isolationScale = Mathf.Clamp01(isolationScale);
            this.baseBribeCost = Mathf.Max(0f, baseBribeCost);
            this.backlashScale = Mathf.Max(0f, backlashScale);
            this.divideThreshold = Mathf.Clamp01(divideThreshold);
        }

        /// <summary>既定＝不満てこ0.5・亀裂てこ1.0・孤立スケール0.8・買収基準100・逆効果1.5倍・分断閾値0.5。</summary>
        public static DivideParams Default => new DivideParams(0.5f, 1.0f, 0.8f, 100f, 1.5f, 0.5f);
    }

    /// <summary>
    /// 分割統治（カエサル＝Divide et Impera・GAL-2 #1346）の純ロジック。敵対連合を、選択的な和解・懐柔
    /// （一部の部族とだけ講和し利益を供与する）によって**内部から分断**する。カエサルはガリア部族の一部を
    /// 味方につけ連合を割り、残った強硬派を孤立させて各個撃破した。**標的の不満が大きく利益供与が厚いほど
    /// 懐柔が効き**（<see cref="SelectiveReconciliationAppeal"/>）、**連合の亀裂が大きいほど離反を誘発しやすい**
    /// （<see cref="DefectionInducement"/>）。割れた後は残った強硬派が孤立し（<see cref="IsolationOfHoldouts"/>）
    /// 各個撃破の前提になる。ただし**分断工作が露見すると連合は逆に結束する**（<see cref="BacklashRisk"/>）。
    ///
    /// 分担：<see cref="AlienationStratagemRules"/>（離間の計＝敵同士に不和を植える・既存の不和に付け込む）とは別
    /// ＝こちらは選択的和解で**味方を引き抜いて**連合を割る。<see cref="DiplomacyRules"/>（外交状態の遷移）とは別
    /// ＝分断工作に特化した係数を返すのみ。連合の亀裂スコアは <see cref="CoalitionFaultlineRules"/>（同 EPIC GAL・
    /// 連合の亀裂）の出力を入力に取る想定（平文言及）。<see cref="CooptionRules"/>（招安＝体制が義賊を取り込む）
    /// とは別＝こちらは連合の分断。乱数は roll(0..1) で決定論。基準値は非破壊（実効値パターン）。
    /// 盤面非依存の plain 引数。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class DivideRules
    {
        /// <summary>
        /// 選択的和解の懐柔効果（0..1）＝利益供与 offeredBenefit(0..1)×（1＋標的の不満×不満てこ）。
        /// 不満を抱える部族ほど和解に乗りやすく、厚い利益供与ほど効く＝カエサルがガリア一部族に与えた特権。
        /// </summary>
        public static float SelectiveReconciliationAppeal(float offeredBenefit, float targetGrievance, DivideParams p)
        {
            float benefit = Mathf.Clamp01(offeredBenefit);
            float grievance = Mathf.Clamp01(targetGrievance);
            float leverage = 1f + grievance * p.grievanceLeverage;   // 不満が大きいほど和解に乗る
            return Mathf.Clamp01(benefit * leverage);
        }

        /// <summary>既定パラメータでの懐柔効果。</summary>
        public static float SelectiveReconciliationAppeal(float offeredBenefit, float targetGrievance)
            => SelectiveReconciliationAppeal(offeredBenefit, targetGrievance, DivideParams.Default);

        /// <summary>
        /// 離反誘発（0..1）＝懐柔効果 appeal×（1＋連合の亀裂 faultlineScore×亀裂てこ）。連合に元から
        /// 走る亀裂が大きいほど同じ懐柔でも離反が誘発されやすい＝<see cref="CoalitionFaultlineRules"/> の
        /// 亀裂スコアを入力に取る。亀裂の無い一枚岩の連合は同じ懐柔でも割れにくい。
        /// </summary>
        public static float DefectionInducement(float appeal, float faultlineScore, DivideParams p)
        {
            float a = Mathf.Clamp01(appeal);
            float faultline = Mathf.Clamp01(faultlineScore);
            float leverage = 1f + faultline * p.faultlineLeverage;   // 亀裂が大きいほど割りやすい
            return Mathf.Clamp01(a * leverage);
        }

        /// <summary>既定パラメータでの離反誘発。</summary>
        public static float DefectionInducement(float appeal, float faultlineScore)
            => DefectionInducement(appeal, faultlineScore, DivideParams.Default);

        /// <summary>
        /// 連合の分断度（0..1）＝離反誘発 defectionInducement×（1−連合の結束 coalitionCohesion）。
        /// 結束の固い連合ほど同じ離反誘発でも割れにくい＝結束が防壁。結束ゼロの烏合の衆は誘発がそのまま
        /// 分断に直結する。
        /// </summary>
        public static float CoalitionFragmentation(float defectionInducement, float coalitionCohesion)
        {
            float induce = Mathf.Clamp01(defectionInducement);
            float cohesion = Mathf.Clamp01(coalitionCohesion);
            return Mathf.Clamp01(induce * (1f - cohesion));
        }

        /// <summary>
        /// 割れた後に残った強硬派の孤立度（0..1）＝分断度 fragmentation×孤立スケール。連合が割れるほど
        /// 残党は後ろ盾を失って孤立し、各個撃破の前提になる＝カエサルが連合を割って残りを各個に潰した。
        /// </summary>
        public static float IsolationOfHoldouts(float fragmentation, DivideParams p)
            => Mathf.Clamp01(Mathf.Clamp01(fragmentation) * p.isolationScale);

        /// <summary>既定パラメータでの強硬派孤立度。</summary>
        public static float IsolationOfHoldouts(float fragmentation)
            => IsolationOfHoldouts(fragmentation, DivideParams.Default);

        /// <summary>
        /// 懐柔のコスト（≧0）＝買収基準×標的の戦力 targetStrength(0..1)×（1−標的の不満 targetGrievance(0..1)）。
        /// 強く（戦力が大きく）不満の小さい部族ほど高くつく＝満ち足りた有力部族は容易には寝返らない。
        /// 不満の大きい弱小部族は安く引き抜ける。
        /// </summary>
        public static float BribeCost(float targetStrength, float targetGrievance, DivideParams p)
        {
            float strength = Mathf.Clamp01(targetStrength);
            float grievance = Mathf.Clamp01(targetGrievance);
            return Mathf.Max(0f, p.baseBribeCost * strength * (1f - grievance));
        }

        /// <summary>既定パラメータでの懐柔コスト。</summary>
        public static float BribeCost(float targetStrength, float targetGrievance)
            => BribeCost(targetStrength, targetGrievance, DivideParams.Default);

        /// <summary>
        /// 分断工作の露見による逆結束リスク（0..1）＝懐柔の露骨さ appeal が大きいほど・工作の透明性
        /// transparency(0..1)（敵側の見通し）が高いほど高い＝appeal×transparency×逆効果スケール。
        /// あからさまな引き抜きが見え透くと、割られかけた連合が危機感で逆に結束する。
        /// </summary>
        public static float BacklashRisk(float appeal, float transparency, DivideParams p)
            => Mathf.Clamp01(Mathf.Clamp01(appeal) * Mathf.Clamp01(transparency) * p.backlashScale);

        /// <summary>既定パラメータでの逆結束リスク。</summary>
        public static float BacklashRisk(float appeal, float transparency)
            => BacklashRisk(appeal, transparency, DivideParams.Default);

        /// <summary>
        /// 分断の正味効果（0..1）＝分断度 fragmentation×（1−露見リスク backlashRisk）。工作が見え透くほど
        /// 逆結束で正味の分断が目減りする＝隠密に割れば効き、露見すれば帳消しになりうる。
        /// </summary>
        public static float DivideEffectiveness(float fragmentation, float backlashRisk)
        {
            float frag = Mathf.Clamp01(fragmentation);
            float backlash = Mathf.Clamp01(backlashRisk);
            return Mathf.Clamp01(frag * (1f - backlash));
        }

        /// <summary>
        /// 連合が割れたと判定するか＝分断度 fragmentation が閾値を超えたら true。各個撃破に移れる目安。
        /// </summary>
        public static bool IsCoalitionDivided(float fragmentation, float threshold)
            => Mathf.Clamp01(fragmentation) > Mathf.Clamp01(threshold);

        /// <summary>既定閾値での分断成立判定。</summary>
        public static bool IsCoalitionDivided(float fragmentation)
            => IsCoalitionDivided(fragmentation, DivideParams.Default.divideThreshold);
    }
}
