using UnityEngine;

namespace Ginei
{
    /// <summary>自動財務運用が選ぶ行動（優先順位の高い順に評価する）。</summary>
    public enum TreasuryAction
    {
        何もしない,
        起債,       // 準備金割れを国債発行で埋める（債務余地がある間）
        借換,       // 金利が下がったので借り換えて利払いを減らす
        緊縮支出,   // 債務上限に接近＝起債に頼れず支出を切る
        増税,       // 歳入不足の恒久対処（緊縮で足りないとき）
        準備金取崩  // 債務余地が尽きて起債不能＝最後の手段で取り崩す
    }

    /// <summary>
    /// 自律財務運用の調整値（マジックナンバー禁止＝集約）。判断のしきい値（タッチレスの作動域）。
    /// </summary>
    public readonly struct AutoTreasuryParams
    {
        public readonly float refinanceThreshold;   // この幅以上の金利低下で借換に動く
        public readonly float austerityHeadroom;     // 債務余地がこの割合以下なら緊縮へ（上限接近）
        public readonly float austerityMaxDepth;     // 緊縮の最大深さ（これ以上は支出を切れない）
        public readonly float touchlessFloor;        // 自動運用の信頼度の下限（激動でもゼロにはしない）
        public readonly float volatilityFreeZone;    // この変動率までは平時＝信頼度満点

        public AutoTreasuryParams(float refinanceThreshold, float austerityHeadroom, float austerityMaxDepth, float touchlessFloor, float volatilityFreeZone)
        {
            this.refinanceThreshold = Mathf.Max(0f, refinanceThreshold);
            this.austerityHeadroom = Mathf.Clamp01(austerityHeadroom);
            this.austerityMaxDepth = Mathf.Clamp01(austerityMaxDepth);
            this.touchlessFloor = Mathf.Clamp01(touchlessFloor);
            this.volatilityFreeZone = Mathf.Clamp01(volatilityFreeZone);
        }

        /// <summary>既定＝借換は金利0.5%低下から・債務余地15%以下で緊縮・緊縮上限50%・信頼度下限0.2・変動0.2まで平時。</summary>
        public static AutoTreasuryParams Default => new AutoTreasuryParams(0.005f, 0.15f, 0.5f, 0.2f, 0.2f);
    }

    /// <summary>
    /// 自律財務運用＝準備金割れで自動起債/借換/支払（#1014・タッチレス財務）。財務ポリシーの逸脱を入力に、
    /// AIが自動で財務行動を選ぶ＝人手を介さない自動操縦。「平時はAIが枠の中で回すが、危機（高変動）では枠を超える
    /// 判断＝人の手が要る」を <see cref="TouchlessConfidence"/> で式に出す（完全自動の限界）。
    /// <b>分担</b>：債務/準備金/金利を実際に増減する財政の実体は <see cref="FiscalRules"/>（PB・国債・債務スパイラル）が、
    /// 枠の逸脱検知（#1013＝この入力）は <see cref="FiscalPolicyRules"/> が担う。ここは<b>行動の選択のみ</b>＝
    /// 逸脱という入力から「次に何をするか」を優先順位つきで決める（実体は動かさない＝決めるだけ）。
    /// 全入力クランプ・乱数なし決定論。test-first。
    /// </summary>
    public static class AutoTreasuryRules
    {
        /// <summary>
        /// 状況に応じた自動行動を選ぶ（優先順位つきの意思決定）。
        /// ①準備金割れ＝資金繰りの危機を最優先で埋める（債務余地があれば起債／尽きていれば取崩）。
        /// ②債務上限に接近（余地が austerityHeadroom 以下）＝起債に頼れず緊縮支出。
        /// ③金利が refinanceThreshold 以上下がっている＝借換で利払いを減らす（平時の最適化）。
        /// どれにも当たらなければ何もしない（タッチレスは静観が基本）。dt&lt;=0 は無作動。
        /// </summary>
        public static TreasuryAction DecideAction(float reserves, float reserveFloor, float debtRatio, float debtCeiling, float interestRate, float dt, AutoTreasuryParams p)
        {
            if (dt <= 0f) return TreasuryAction.何もしない;

            float r = Mathf.Max(0f, reserves);
            float floor = Mathf.Max(0f, reserveFloor);
            float ceiling = Mathf.Max(0f, debtCeiling);
            float headroomRatio = DebtHeadroomRatio(debtRatio, debtCeiling);

            // ①資金繰りの危機（準備金割れ）＝最優先。
            if (r < floor)
                return DebtHeadroom(debtRatio, debtCeiling) > 0f ? TreasuryAction.起債 : TreasuryAction.準備金取崩;

            // ②債務上限に接近＝起債余地が乏しい＝支出を切る。
            if (ceiling > 0f && headroomRatio <= p.austerityHeadroom)
                return TreasuryAction.緊縮支出;

            return TreasuryAction.何もしない;
        }

        /// <summary><see cref="DecideAction"/> の既定パラメータ版。</summary>
        public static TreasuryAction DecideAction(float reserves, float reserveFloor, float debtRatio, float debtCeiling, float interestRate, float dt)
            => DecideAction(reserves, reserveFloor, debtRatio, debtCeiling, interestRate, dt, AutoTreasuryParams.Default);

        /// <summary>
        /// 自動起債額＝準備金不足を埋めるが、債務余地の範囲を超えない（上限を破ってまで起債しない）。
        /// 不足も余地も非負にクランプ。余地が不足より小さければ「埋めきれない」＝余地ぶんだけ起債する。
        /// </summary>
        public static float BondIssuanceAmount(float reserveShortfall, float debtHeadroom)
        {
            float shortfall = Mathf.Max(0f, reserveShortfall);
            float headroom = Mathf.Max(0f, debtHeadroom);
            return Mathf.Min(shortfall, headroom);
        }

        /// <summary>
        /// 借換の利得＝（旧金利−新金利）×債務（金利が下がったぶんだけ年間利払いが減る）。
        /// 金利が下がっていない（新≧旧）なら利得0＝借り換えない。債務は非負にクランプ。
        /// </summary>
        public static float RefinanceBenefit(float oldRate, float newRate, float debt)
        {
            float drop = Mathf.Max(0f, oldRate - newRate);
            return drop * Mathf.Max(0f, debt);
        }

        /// <summary>
        /// 借換に動くべきか＝金利低下幅が refinanceThreshold 以上か（小さな低下で頻繁に借り換えない）。
        /// </summary>
        public static bool ShouldRefinance(float oldRate, float newRate, AutoTreasuryParams p)
            => (Mathf.Max(0f, oldRate) - Mathf.Max(0f, newRate)) >= p.refinanceThreshold;

        /// <summary>
        /// 緊縮の深さ 0..austerityMaxDepth＝債務上限に近いほど深く支出を切る。
        /// 余地が尽きる（ratio=0）で最大・余地 austerityHeadroom 以上で0（まだ起債できる＝緊縮不要）。
        /// 上限0は余地概念なし＝最大深さで切る。
        /// </summary>
        public static float AusterityDepth(float debtRatio, float debtCeiling, AutoTreasuryParams p)
        {
            if (debtCeiling <= 0f) return p.austerityMaxDepth;
            float headroomRatio = DebtHeadroomRatio(debtRatio, debtCeiling);
            if (p.austerityHeadroom <= 0f)
                return headroomRatio <= 0f ? p.austerityMaxDepth : 0f;
            // 余地が austerityHeadroom→0 へ近づくほど 0→max へ線形に深くなる。
            float t = Mathf.Clamp01(1f - headroomRatio / p.austerityHeadroom);
            return t * p.austerityMaxDepth;
        }

        /// <summary><see cref="AusterityDepth"/> の既定パラメータ版。</summary>
        public static float AusterityDepth(float debtRatio, float debtCeiling)
            => AusterityDepth(debtRatio, debtCeiling, AutoTreasuryParams.Default);

        /// <summary>
        /// 支払能力の経路＝自動運用で破綻を回避できるか（詰みの早期判定）。
        /// 見込み赤字（次期の準備金流出）を、現在の準備金＋まだ起債できる余地で吸収できれば true。
        /// 吸収しきれない＝起債しても取り崩しても払いきれない＝自動運用では詰み（人の判断＝枠超えが要る）。
        /// 赤字が0以下（黒字）なら常に true。
        /// </summary>
        public static bool IsSolventPath(float reserves, float projectedDeficit, float debtHeadroom)
        {
            float deficit = Mathf.Max(0f, projectedDeficit);
            if (deficit <= 0f) return true;
            float capacity = Mathf.Max(0f, reserves) + Mathf.Max(0f, debtHeadroom);
            return capacity >= deficit;
        }

        /// <summary>
        /// 自動運用の信頼度 0..1＝平時はAIに任せられるが激動期は人の判断が要る（完全自動の限界）。
        /// 変動率が volatilityFreeZone 以下なら1（平時＝任せきり）、そこから上昇するほど touchlessFloor へ
        /// 線形に低下する（激動でも下限は割らない＝何もしないよりはマシだが、低いほど人手が要るサイン）。
        /// </summary>
        public static float TouchlessConfidence(float volatility, AutoTreasuryParams p)
        {
            float v = Mathf.Clamp01(volatility);
            if (v <= p.volatilityFreeZone) return 1f;
            float span = 1f - p.volatilityFreeZone;
            if (span <= 0f) return p.touchlessFloor; // 平時域が全域＝域外は下限
            float t = Mathf.Clamp01((v - p.volatilityFreeZone) / span);
            return Mathf.Lerp(1f, p.touchlessFloor, t);
        }

        /// <summary><see cref="TouchlessConfidence"/> の既定パラメータ版。</summary>
        public static float TouchlessConfidence(float volatility)
            => TouchlessConfidence(volatility, AutoTreasuryParams.Default);

        // ===== 内部ヘルパ（債務余地＝上限まであと何ぶん起債できるか） =====

        /// <summary>債務余地（絶対量）＝上限比率−現在比率（0未満は上限到達で0）。</summary>
        private static float DebtHeadroom(float debtRatio, float debtCeiling)
            => Mathf.Max(0f, Mathf.Max(0f, debtCeiling) - Mathf.Max(0f, debtRatio));

        /// <summary>債務余地の割合 0..1＝余地を上限で正規化（上限0なら余地なし＝0）。</summary>
        private static float DebtHeadroomRatio(float debtRatio, float debtCeiling)
        {
            float ceiling = Mathf.Max(0f, debtCeiling);
            if (ceiling <= 0f) return 0f;
            return Mathf.Clamp01(DebtHeadroom(debtRatio, debtCeiling) / ceiling);
        }
    }
}
