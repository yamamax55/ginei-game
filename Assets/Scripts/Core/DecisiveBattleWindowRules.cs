using UnityEngine;

namespace Ginei
{
    /// <summary>決戦の機会窓口の調整係数。ctor で全値をクランプする。</summary>
    public readonly struct DecisiveBattleWindowParams
    {
        /// <summary>自軍の決戦準備度における「戦力集結」の寄与（重み）。</summary>
        public readonly float concentrationWeight;
        /// <summary>自軍の決戦準備度における「補給充足」の寄与（重み）。</summary>
        public readonly float supplyWeight;
        /// <summary>自軍の決戦準備度における「士気の高揚」の寄与（重み）。</summary>
        public readonly float moraleWeight;
        /// <summary>好機の窓が開いたとみなす発火閾値（0..1）。これを超えると決戦の機会が発火する。</summary>
        public readonly float triggerThreshold;
        /// <summary>好機が時間で去る速さ（窓のフェード率・1秒あたり）。決戦の窓は長く開かない。</summary>
        public readonly float windowDecayRate;

        public DecisiveBattleWindowParams(float concentrationWeight, float supplyWeight,
            float moraleWeight, float triggerThreshold, float windowDecayRate)
        {
            this.concentrationWeight = Mathf.Max(0f, concentrationWeight);
            this.supplyWeight = Mathf.Max(0f, supplyWeight);
            this.moraleWeight = Mathf.Max(0f, moraleWeight);
            this.triggerThreshold = Mathf.Clamp01(triggerThreshold);
            this.windowDecayRate = Mathf.Max(0f, windowDecayRate);
        }

        /// <summary>既定＝集結重み0.4・補給重み0.3・士気重み0.3／発火閾値0.6／窓フェード率0.25。</summary>
        public static DecisiveBattleWindowParams Default =>
            new DecisiveBattleWindowParams(0.4f, 0.3f, 0.3f, 0.6f, 0.25f);
    }

    /// <summary>
    /// 決戦の機会窓口の純ロジック＝『坂の上の雲』型（SKUN-6・#1436）。<b>決戦（decisive battle）の好機＝
    /// 戦力の集結・敵の露出・補給の充足・士気の高揚など複数の条件が揃ったとき、一挙に雌雄を決する決戦の
    /// 機会の窓が開く＝日本海海戦のように、機が熟した瞬間を捉えねば好機は去る</b>。蓄積条件が揃ったとき
    /// 決戦の機会が発火し（<see cref="DecisiveBattleTrigger"/> が <see cref="EventEngine"/> へ「決戦の機会」を
    /// 渡す純ロジック部）、機を逃すと窓は閉じる。<see cref="EscalationRules"/>（紛争の梯子＝望まずとも昇る
    /// 緊張のエスカレーション）とは別＝こちらは決戦の好機の生起判定（条件充足→決戦機会の発火）。
    /// <see cref="CenterOfGravityRules"/>（重心＝叩くべき一点の同定）とも別＝こちらは「いつ叩くか＝機が熟す
    /// 瞬間」の窓。<see cref="SunziDoctrineRules"/>（謀攻＝どの手段で勝つか）とも別＝こちらは決戦に踏み切る
    /// タイミングの判定。倍率・準備度は基準値に掛けて使う（実効値パターン・基準非破壊）。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class DecisiveBattleWindowRules
    {
        /// <summary>
        /// 自軍の決戦準備度（0..1）＝複数条件の充足。forceConcentration（戦力集結0..1）× supplyAdequacy
        /// （補給充足0..1）× moraleHigh（士気の高揚0..1）を重み付き平均する（重み合計で正規化、合計0なら0）。
        /// 戦力が集まり・補給が満ち・士気が高いほど準備が整う＝条件が揃うほど決戦に踏み切れる。
        /// </summary>
        public static float Readiness(float forceConcentration, float supplyAdequacy, float moraleHigh,
            DecisiveBattleWindowParams p)
        {
            float conc = Mathf.Clamp01(forceConcentration);
            float sup = Mathf.Clamp01(supplyAdequacy);
            float mor = Mathf.Clamp01(moraleHigh);
            float weightSum = p.concentrationWeight + p.supplyWeight + p.moraleWeight;
            if (weightSum <= 0f) return 0f;
            return Mathf.Clamp01(
                (p.concentrationWeight * conc + p.supplyWeight * sup + p.moraleWeight * mor) / weightSum);
        }

        /// <summary>既定係数での自軍の決戦準備度（0..1）。</summary>
        public static float Readiness(float forceConcentration, float supplyAdequacy, float moraleHigh)
            => Readiness(forceConcentration, supplyAdequacy, moraleHigh, DecisiveBattleWindowParams.Default);

        /// <summary>
        /// 敵の脆弱性（0..1）＝敵が無防備な好機。enemyExposure（敵の露出0..1＝身を晒している度）×
        /// enemyFatigue（敵の疲弊0..1）の積＝敵が露わで疲れているほど脆い。どちらか一方でも0なら脆弱性0＝
        /// 露出していても疲れていなければ・疲れていても露出していなければ叩く好機にはならない。
        /// </summary>
        public static float EnemyVulnerability(float enemyExposure, float enemyFatigue)
        {
            float exp = Mathf.Clamp01(enemyExposure);
            float fat = Mathf.Clamp01(enemyFatigue);
            return Mathf.Clamp01(exp * fat);
        }

        /// <summary>
        /// 決戦の機会の窓が開く度合い（0..1）＝準備度と敵脆弱性が揃うと窓が開く。readiness（自軍準備度0..1）×
        /// enemyVulnerability（敵脆弱性0..1）の積＝こちらが整い・かつ敵が脆い瞬間にだけ窓が開く。どちらか
        /// 一方でも0なら窓は開かない＝準備万端でも敵が堅ければ・敵が脆くても自軍が未整なら決戦の好機ではない。
        /// </summary>
        public static float WindowOpening(float readiness, float enemyVulnerability)
        {
            float rdy = Mathf.Clamp01(readiness);
            float vuln = Mathf.Clamp01(enemyVulnerability);
            return Mathf.Clamp01(rdy * vuln);
        }

        /// <summary>
        /// 好機の窓の時間更新（0..1）＝窓は条件が保たれる間だけ開き続ける。conditionsHolding（条件が保たれて
        /// いる度0..1）が高いほど窓を維持し、崩れた分だけ窓フェード率 ×（1−保持度）× dt で閉じていく
        /// ＝機を逃すと窓は閉じる。条件が完全に保たれていれば窓は開いたまま、崩れれば速やかに閉じる。
        /// </summary>
        public static float WindowTick(float windowValue, float conditionsHolding, float dt,
            DecisiveBattleWindowParams p)
        {
            float w = Mathf.Clamp01(windowValue);
            float hold = Mathf.Clamp01(conditionsHolding);
            float t = Mathf.Max(0f, dt);
            // 保持度が低いほど速く閉じる（保持1.0なら不変・保持0なら最速でフェード）
            float close = p.windowDecayRate * (1f - hold) * t;
            return Mathf.Clamp01(w - close);
        }

        /// <summary>既定係数での好機の窓の時間更新（0..1）。</summary>
        public static float WindowTick(float windowValue, float conditionsHolding, float dt)
            => WindowTick(windowValue, conditionsHolding, dt, DecisiveBattleWindowParams.Default);

        /// <summary>
        /// 決戦の機会の発火判定＝窓の開きが閾値を超えたか（<see cref="EventEngine"/> へ「決戦の機会」を渡す
        /// 純ロジック部）。windowOpening（窓の開き0..1）が threshold を超えたとき true ＝機が熟した瞬間を
        /// イベントとして発火させる。閾値以下なら好機は未だ熟しておらず発火しない。
        /// </summary>
        public static bool DecisiveBattleTrigger(float windowOpening, float threshold)
        {
            return Mathf.Clamp01(windowOpening) >= Mathf.Clamp01(threshold);
        }

        /// <summary>既定閾値（<see cref="DecisiveBattleWindowParams.triggerThreshold"/>）での発火判定。</summary>
        public static bool DecisiveBattleTrigger(float windowOpening)
            => DecisiveBattleTrigger(windowOpening, DecisiveBattleWindowParams.Default.triggerThreshold);

        /// <summary>
        /// 好機をためらって逃すコスト（0..1）＝機が熟したのに動かない損失。windowOpening（逃しつつある好機の
        /// 大きさ0..1）× hesitation（ためらいの度0..1）の積＝好機が大きいほど・ためらうほど損失が大きい。
        /// 好機が無ければ逃すものも無く（0）、即断すれば（hesitation 0）コストは生じない＝機を逃すは損。
        /// </summary>
        public static float OpportunityCost(float windowOpening, float hesitation)
        {
            float w = Mathf.Clamp01(windowOpening);
            float hes = Mathf.Clamp01(hesitation);
            return Mathf.Clamp01(w * hes);
        }

        /// <summary>
        /// 好機の消失（0..1）＝好機は時間で去る（決戦の窓は長く開かない）。windowValue を窓フェード率 × dt
        /// だけ単調に減衰させる＝条件の保持に依らず時間そのものが好機を蝕む（<see cref="WindowTick"/> が
        /// 条件保持を見るのに対し、これは時間経過だけで否応なく閉じる版＝放置すれば窓は必ず去る）。
        /// </summary>
        public static float FleetingWindow(float windowValue, float dt, DecisiveBattleWindowParams p)
        {
            float w = Mathf.Clamp01(windowValue);
            float t = Mathf.Max(0f, dt);
            return Mathf.Clamp01(w - p.windowDecayRate * t);
        }

        /// <summary>既定係数での好機の消失（0..1）。</summary>
        public static float FleetingWindow(float windowValue, float dt)
            => FleetingWindow(windowValue, dt, DecisiveBattleWindowParams.Default);

        /// <summary>
        /// 好機を捉えた決戦がもたらす戦果の大きさ（0..1）＝機が完璧なほど一挙に雌雄を決する。
        /// windowOpening（窓の開き0..1）× forceRatio（戦力比0..1＝こちらの相対戦力）の積＝好機が完璧で
        /// 戦力も優れば戦果は最大。好機が薄ければ（窓が小さい）一挙には決まらず、戦力比が乏しければ好機を
        /// 捉えても戦果は限られる＝日本海海戦のように機と力が揃ってこそ決戦は決定的になる。
        /// </summary>
        public static float DecisiveOutcomeMagnitude(float windowOpening, float forceRatio)
        {
            float w = Mathf.Clamp01(windowOpening);
            float ratio = Mathf.Clamp01(forceRatio);
            return Mathf.Clamp01(w * ratio);
        }

        /// <summary>
        /// 今が決戦すべき瞬間かの判定＝窓の開きが閾値を超えたか。<see cref="DecisiveBattleTrigger"/> と同型の
        /// 意味判定（発火＝イベント駆動、こちらは AI/UI が「今こそ決戦の時」と読む述語）。機が熟したかを問う。
        /// </summary>
        public static bool IsDecisiveMoment(float windowOpening, float threshold)
        {
            return Mathf.Clamp01(windowOpening) >= Mathf.Clamp01(threshold);
        }

        /// <summary>既定閾値での決戦の瞬間判定。</summary>
        public static bool IsDecisiveMoment(float windowOpening)
            => IsDecisiveMoment(windowOpening, DecisiveBattleWindowParams.Default.triggerThreshold);
    }
}
