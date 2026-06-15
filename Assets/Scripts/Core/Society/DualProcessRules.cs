using UnityEngine;

namespace Ginei
{
    /// <summary>二重過程（System1/System2）の調整係数。</summary>
    public readonly struct DualProcessParams
    {
        /// <summary>System1（直感）の基準速度（直感レベル0で出る最低速度＝努力不要ゆえ高め）。</summary>
        public readonly float intuitionBaseSpeed;
        /// <summary>直感レベル1.0で基準速度に上乗せされる速さ（直感が鋭いほど速く即断する）。</summary>
        public readonly float intuitionSpeedGain;
        /// <summary>System2（熟慮）の遅さ＝熟慮的なスタイルほど判断速度を削る最大割合（スタイル1.0でこれだけ遅い）。</summary>
        public readonly float deliberationSlowdown;
        /// <summary>認知負荷1.0で System2 の正確さを削る最大割合（負荷が高いほど熟慮しきれない）。</summary>
        public readonly float loadAccuracyPenalty;
        /// <summary>System1 の基礎誤り率（慣れた問題でも直感が犯す系統的バイアスの下限）。</summary>
        public readonly float intuitionBaseError;
        /// <summary>不慣れ1.0で直感の誤り率に上乗せされる最大幅（不慣れな問題ほど直感は外す）。</summary>
        public readonly float unfamiliarityErrorGain;
        /// <summary>時間切迫1.0でスタイルを System1 側へ寄せる最大幅（熟慮する暇がない）。</summary>
        public readonly float pressureShiftMax;

        public DualProcessParams(float intuitionBaseSpeed, float intuitionSpeedGain, float deliberationSlowdown,
            float loadAccuracyPenalty, float intuitionBaseError, float unfamiliarityErrorGain, float pressureShiftMax)
        {
            this.intuitionBaseSpeed = Mathf.Clamp01(intuitionBaseSpeed);
            this.intuitionSpeedGain = Mathf.Clamp01(intuitionSpeedGain);
            this.deliberationSlowdown = Mathf.Clamp01(deliberationSlowdown);
            this.loadAccuracyPenalty = Mathf.Clamp01(loadAccuracyPenalty);
            this.intuitionBaseError = Mathf.Clamp01(intuitionBaseError);
            this.unfamiliarityErrorGain = Mathf.Clamp01(unfamiliarityErrorGain);
            this.pressureShiftMax = Mathf.Clamp01(pressureShiftMax);
        }

        /// <summary>既定＝直感基準速0.6/直感速度増0.4/熟慮遅延0.5/負荷罰0.5/直感基礎誤り0.1/不慣れ誤り増0.5/切迫寄せ0.7。</summary>
        public static DualProcessParams Default =>
            new DualProcessParams(0.6f, 0.4f, 0.5f, 0.5f, 0.1f, 0.5f, 0.7f);
    }

    /// <summary>
    /// 二重過程理論の純ロジック（カーネマン KAHN-6）。判断は直感型 System1（速い・自動・努力不要だが
    /// 系統的誤りを犯す）と熟慮型 System2（遅い・努力を要するが正確）に分かれる。提督の認知スタイル
    /// cognitiveStyle（0=直感的〜1=熟慮的）が両過程の配合を決め、緊急時は System1 が頼り、複雑な判断は
    /// System2 が要る。時間切迫は熟慮する暇を奪い System1 へ寄せる＝速いが粗い判断になる。
    /// 分担：<see cref="AdmiralData"/> の能力（統率/攻撃/防御/機動）とは別＝速さ vs 正確さという認知スタイルの軸／
    /// <see cref="OperationPlanRules"/>（立案の質＝能力×準備）とは別＝判断過程そのものの速度/正確さに特化／
    /// 同 EPIC KAHN の <see cref="OverconfidenceBiasRules"/>（過信・計画錯誤）の校正に絡む＝System2 が
    /// バイアスへ抵抗する側（呼び出し側が DecisionAccuracy 等を抵抗値として使う想定）。基準値は非破壊＝
    /// 主観的な速度/正確さ/誤り率を返す（実効値パターン）。盤面非依存の plain 引数。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class DualProcessRules
    {
        /// <summary>
        /// System1（直感）の判断速度（0..1）＝基準速度 + 直感レベル×速度増。直感が鋭いほど即断が速い。
        /// </summary>
        public static float System1Speed(float intuitionLevel, DualProcessParams p)
        {
            float iv = Mathf.Clamp01(intuitionLevel);
            return Mathf.Clamp01(p.intuitionBaseSpeed + iv * p.intuitionSpeedGain);
        }

        public static float System1Speed(float intuitionLevel)
            => System1Speed(intuitionLevel, DualProcessParams.Default);

        /// <summary>
        /// System2（熟慮）の正確さ（0..1）＝熟慮レベルを認知負荷が削る＝deliberation×(1 − 負荷×loadAccuracyPenalty)。
        /// 熟慮が深いほど正確だが、認知負荷が高いと熟慮しきれず正確さが落ちる。
        /// </summary>
        public static float System2Accuracy(float deliberationLevel, float cognitiveLoad, DualProcessParams p)
        {
            float dl = Mathf.Clamp01(deliberationLevel);
            float load = Mathf.Clamp01(cognitiveLoad);
            return Mathf.Clamp01(dl * (1f - load * p.loadAccuracyPenalty));
        }

        public static float System2Accuracy(float deliberationLevel, float cognitiveLoad)
            => System2Accuracy(deliberationLevel, cognitiveLoad, DualProcessParams.Default);

        /// <summary>
        /// 認知スタイル（0=直感的〜1=熟慮的）から System2 の配合比を返す（=スタイルそのもの、0..1）。
        /// System1 の配合は 1 − これ。両過程の混ざり具合の素。
        /// </summary>
        public static float ProcessBlend(float cognitiveStyle)
        {
            return Mathf.Clamp01(cognitiveStyle);
        }

        /// <summary>
        /// スタイルに応じた判断速度（0..1）。直感的（スタイル0）は System1 の基準速度で速く、熟慮的（スタイル1）は
        /// deliberationSlowdown だけ遅い＝System1基準速度×(1 − スタイル×熟慮遅延)。考えるほど遅い。
        /// </summary>
        public static float DecisionSpeed(float cognitiveStyle, DualProcessParams p)
        {
            float s = Mathf.Clamp01(cognitiveStyle);
            return Mathf.Clamp01(p.intuitionBaseSpeed * (1f - s * p.deliberationSlowdown));
        }

        public static float DecisionSpeed(float cognitiveStyle)
            => DecisionSpeed(cognitiveStyle, DualProcessParams.Default);

        /// <summary>
        /// 判断の正確さ（0..1）。単純な問題は直感（System1）で十分だが、複雑な問題は熟慮（System2）が要る＝
        /// 問題複雑度の重みで「直感の素の正確さ(=1−基礎誤り)」と「熟慮スタイル」を混ぜる。複雑度0なら誰でも当たり、
        /// 複雑度1ならスタイルが熟慮的なほど正確。直感型は複雑な問題で正確さを落とす。
        /// </summary>
        public static float DecisionAccuracy(float cognitiveStyle, float problemComplexity, DualProcessParams p)
        {
            float s = Mathf.Clamp01(cognitiveStyle);
            float c = Mathf.Clamp01(problemComplexity);
            float intuitiveAccuracy = 1f - p.intuitionBaseError; // 単純な問題での直感の当たりやすさ
            // 複雑度0＝直感で十分（intuitiveAccuracy）、複雑度1＝スタイル（熟慮的なほど正確）が支配
            return Mathf.Clamp01(Mathf.Lerp(intuitiveAccuracy, s, c));
        }

        public static float DecisionAccuracy(float cognitiveStyle, float problemComplexity)
            => DecisionAccuracy(cognitiveStyle, problemComplexity, DualProcessParams.Default);

        /// <summary>
        /// 時間切迫がスタイルを System1（直感）側へ寄せた実効スタイル（0..1）。熟慮する暇がないほど
        /// スタイルを 0（直感的）へ引く＝style×(1 − timePressure×pressureShiftMax)。切迫1で最も直感に頼る。
        /// </summary>
        public static float TimePressureShift(float cognitiveStyle, float timePressure, DualProcessParams p)
        {
            float s = Mathf.Clamp01(cognitiveStyle);
            float tp = Mathf.Clamp01(timePressure);
            return Mathf.Clamp01(s * (1f - tp * p.pressureShiftMax));
        }

        public static float TimePressureShift(float cognitiveStyle, float timePressure)
            => TimePressureShift(cognitiveStyle, timePressure, DualProcessParams.Default);

        /// <summary>
        /// 直感の系統的誤り率（0..1）＝基礎誤り + 直感レベル×不慣れ度×不慣れ誤り増。不慣れな問題ほど直感は外し、
        /// 直感に頼る（直感レベルが高い）ほどその誤りが効く。慣れた問題（familiarity≈1）では基礎誤りに収束。
        /// </summary>
        public static float IntuitionErrorRate(float intuitionLevel, float problemFamiliarity, DualProcessParams p)
        {
            float iv = Mathf.Clamp01(intuitionLevel);
            float unfamiliarity = 1f - Mathf.Clamp01(problemFamiliarity);
            return Mathf.Clamp01(p.intuitionBaseError + iv * unfamiliarity * p.unfamiliarityErrorGain);
        }

        public static float IntuitionErrorRate(float intuitionLevel, float problemFamiliarity)
            => IntuitionErrorRate(intuitionLevel, problemFamiliarity, DualProcessParams.Default);

        /// <summary>
        /// 速さと正確さのトレードオフ指標（-1..1）。速度−正確さ＝正なら「速いが粗い」寄り、負なら「遅いが正確」寄り、
        /// 0 なら均衡。両者をどちらも引数で受ける（呼び出し側が DecisionSpeed と DecisionAccuracy を渡す）。
        /// </summary>
        public static float SpeedAccuracyTradeoff(float decisionSpeed, float decisionAccuracy)
        {
            return Mathf.Clamp(Mathf.Clamp01(decisionSpeed) - Mathf.Clamp01(decisionAccuracy), -1f, 1f);
        }

        /// <summary>直感型の意思決定者か＝スタイルが閾値より直感寄り（threshold 未満で true）。</summary>
        public static bool IsIntuitiveDecider(float cognitiveStyle, float threshold)
        {
            return Mathf.Clamp01(cognitiveStyle) < threshold;
        }
    }
}
