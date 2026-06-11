using UnityEngine;

namespace Ginei
{
    /// <summary>グレーゾーン作戦の調整係数。</summary>
    public readonly struct GreyZoneParams
    {
        /// <summary>否認可能性に占める「帰属の困難さ」の重み（誰がやったか証拠で辿れない度合い）。</summary>
        public readonly float attributionWeight;
        /// <summary>否認可能性に占める「代理勢力の使用」の重み（自国の手を見せない度合い）。</summary>
        public readonly float proxyWeight;
        /// <summary>サラミ1スライスが「反撃に値しない小ささ」となる上限（incrementSize がこれ以下なら反撃を招かない）。</summary>
        public readonly float sliceTolerance;
        /// <summary>既成事実が時間で現状変更へ熟成する速度（per dt）。</summary>
        public readonly float faitAccompliRate;
        /// <summary>グレーゾーン侵略と見なす閾値の近さ・否認可能性の積の下限（既定の判定しきい）。</summary>
        public readonly float aggressionFloor;

        public GreyZoneParams(float attributionWeight, float proxyWeight, float sliceTolerance, float faitAccompliRate, float aggressionFloor)
        {
            this.attributionWeight = Mathf.Clamp01(attributionWeight);
            this.proxyWeight = Mathf.Clamp01(proxyWeight);
            this.sliceTolerance = Mathf.Clamp01(sliceTolerance);
            this.faitAccompliRate = Mathf.Max(0f, faitAccompliRate);
            this.aggressionFloor = Mathf.Clamp01(aggressionFloor);
        }

        /// <summary>既定＝帰属重み0.6・代理重み0.4・スライス許容0.3・熟成速度0.5・侵略しきい0.5。</summary>
        public static GreyZoneParams Default => new GreyZoneParams(0.6f, 0.4f, 0.3f, 0.5f, 0.5f);
    }

    /// <summary>
    /// グレーゾーン作戦（閾値以下・戦争未満の曖昧な攻撃）の純ロジック（#1392・ULW-4 限定戦争）。
    /// 明確な戦争（武力行使）に至らない曖昧な領域で、宣戦布告なき損害を与えつつ否認可能性（deniability）を保ち、
    /// 報復・全面戦争の口実を相手に与えない。サラミ戦術のように小さな既成事実（一回一回は反撃に値しない小ささ）を
    /// 積み重ね、相手のレッドライン（反撃ライン）以下に挑発を抑え続けて、気づけば大きな現状変更を成し遂げる。
    /// 被害側は「反撃すれば過剰反応・見過ごせば既成事実」のジレンマに置かれ、否認可能性がエスカレーションを抑え込む。
    /// 分担：<see cref="EscalationRules"/>（紛争の梯子＝戦争へ昇る力学）／
    /// <see cref="SecurityDilemmaRules"/>（安全保障ジレンマ＝防衛軍備が相互不安を生む構造・生成済み）／
    /// <see cref="HybridCampaignRules"/>（同 EPIC ULW のハイブリッド戦＝多領域の合成・生成済み）／
    /// <see cref="DeterrenceRules"/>（抑止＝力の顕示が開戦の損得を変える）とは別系統＝
    /// こちらは「戦争未満の曖昧な攻撃」そのもの（否認可能性モデル）を解く。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class GreyZoneRules
    {
        /// <summary>
        /// 閾値への近さ（0..1）＝行動の大きさが戦争の閾値にどれだけ迫っているか＝Min(行動規模/戦争閾値, 1)。
        /// 閾値以下に留めるのがグレーゾーンの肝＝1.0で閾値到達（もはやグレーゾーンでない＝公然の戦争）。
        /// </summary>
        public static float ThresholdProximity(float actionMagnitude, float warThreshold)
        {
            float threshold = Mathf.Max(0.01f, Mathf.Clamp01(warThreshold));
            return Mathf.Clamp01(Mathf.Clamp01(actionMagnitude) / threshold);
        }

        /// <summary>
        /// 否認可能性（0..1）＝帰属の困難さ(0..1)×帰属重み＋代理勢力の使用(0..1)×代理重み。
        /// 攻撃の帰属を曖昧にする（誰がやったか分からない）ほど・代理勢力や偽装を使うほど、否認できる。
        /// </summary>
        public static float Deniability(float attributionDifficulty, float proxyUse, GreyZoneParams p)
        {
            return Mathf.Clamp01(
                Mathf.Clamp01(attributionDifficulty) * p.attributionWeight +
                Mathf.Clamp01(proxyUse) * p.proxyWeight);
        }

        public static float Deniability(float attributionDifficulty, float proxyUse)
            => Deniability(attributionDifficulty, proxyUse, GreyZoneParams.Default);

        /// <summary>
        /// サラミ戦術の利得（0..1）＝既存の累積利得＋今回のスライス。ただしスライスが sliceTolerance を超える
        /// （反撃に値する大きさ）と、反撃を招くペナルティで利得が削られる＝一回一回は小さく刻むのが肝。
        /// </summary>
        public static float SalamiSlicing(float incrementSize, float accumulatedGain, GreyZoneParams p)
        {
            float slice = Mathf.Clamp01(incrementSize);
            float gain = Mathf.Clamp01(accumulatedGain) + slice;
            // 反撃に値する大きさ（許容超過）は招いた反撃ぶん利得を相殺する＝大きく刻めば積み上がらない。
            if (slice > p.sliceTolerance)
            {
                float overshoot = slice - p.sliceTolerance;
                gain -= overshoot;
            }
            return Mathf.Clamp01(gain);
        }

        public static float SalamiSlicing(float incrementSize, float accumulatedGain)
            => SalamiSlicing(incrementSize, accumulatedGain, GreyZoneParams.Default);

        /// <summary>
        /// 反撃ライン以下の挑発か＝行動規模が相手のレッドライン（targetRedline）未満なら true。
        /// 反撃を招かない絶妙な小ささ＝レッドラインを踏まない限り相手は撃ち返せない。
        /// </summary>
        public static bool ProvocationBelowResponse(float actionMagnitude, float targetRedline)
        {
            return Mathf.Clamp01(actionMagnitude) < Mathf.Clamp01(targetRedline);
        }

        /// <summary>
        /// 反撃のジレンマ（0..1・大きいほど見過ごす方へ傾く）＝否認可能性(0..1)×(1−閾値への近さ)。
        /// 否認可能性が高く（誰がやったか曖昧）・閾値から遠い（小さい挑発）ほど、反撃は過剰反応に見え、
        /// 被害側は反撃をためらい既成事実を許す＝攻撃側が突きたいジレンマの深さ。
        /// </summary>
        public static float ResponseDilemma(float deniability, float thresholdProximity)
        {
            return Mathf.Clamp01(deniability) * (1f - Mathf.Clamp01(thresholdProximity));
        }

        /// <summary>
        /// 既成事実の累積（0..1）＝積み重ねたサラミ利得が時間で大きな現状変更へ熟成する1tick後の値。
        /// 利得が大きいほど・dt が経つほど現状変更が進む＝気づけば手遅れ（小さな積み重ねが不可逆な大局へ）。
        /// </summary>
        public static float CumulativeFaitAccompli(float salamiSlicing, float dt, GreyZoneParams p)
        {
            float grow = Mathf.Clamp01(salamiSlicing) * p.faitAccompliRate * Mathf.Max(0f, dt);
            return Mathf.Clamp01(Mathf.Clamp01(salamiSlicing) + grow);
        }

        public static float CumulativeFaitAccompli(float salamiSlicing, float dt)
            => CumulativeFaitAccompli(salamiSlicing, dt, GreyZoneParams.Default);

        /// <summary>
        /// エスカレーション制御（0..1・大きいほど全面戦争を回避できる）＝否認可能性(0..1)と
        /// 退避路（offRamp＝相手が面子を保って引ける余地・0..1）の合成。
        /// 否認可能性が口実を奪い、退避路が相手に出口を残す＝両方そろうほど梯子を昇らせない。
        /// 1−(1−deniability)×(1−offRamp)＝どちらか高ければ制御が効く（積の補集合）。
        /// </summary>
        public static float EscalationControl(float deniability, float offRamp)
        {
            float d = Mathf.Clamp01(deniability);
            float o = Mathf.Clamp01(offRamp);
            return Mathf.Clamp01(1f - (1f - d) * (1f - o));
        }

        /// <summary>
        /// グレーゾーン侵略の判定＝閾値以下（thresholdProximity が 1 未満＝公然の戦争でない）かつ
        /// 「閾値への近さの低さ×否認可能性」が threshold 以上＝戦争未満なのに否認の影で着実に損害を与えている。
        /// 小さく（閾値から遠い）・曖昧に（否認可能）侵食するほど成立＝サラミ戦術の核を式に出す。
        /// </summary>
        public static bool IsGreyZoneAggression(float thresholdProximity, float deniability, float threshold)
        {
            float prox = Mathf.Clamp01(thresholdProximity);
            // 閾値到達＝もはやグレーゾーンでない（公然の戦争）。
            if (prox >= 1f) return false;
            // 戦争未満（閾値から遠い＝小さい挑発）×否認可能性＝グレーゾーン侵略の強度。
            float intensity = (1f - prox) * Mathf.Clamp01(deniability);
            return intensity >= Mathf.Clamp01(threshold);
        }

        public static bool IsGreyZoneAggression(float thresholdProximity, float deniability, GreyZoneParams p)
            => IsGreyZoneAggression(thresholdProximity, deniability, p.aggressionFloor);
    }
}
