using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 国営放送パラメータ（公式報道機関の運営と効果）。readonly struct・トップレベル。
    /// ctor で全値をクランプ。Default で標準値。
    /// </summary>
    public readonly struct StateMediaParams
    {
        /// <summary>到達範囲の伸び（インフラ×人口に掛ける利得・1で素の積）。</summary>
        public readonly float reachGain;
        /// <summary>公式発表の信頼度における実績の重み（残りは透明性）。</summary>
        public readonly float trackRecordWeight;
        /// <summary>娯楽による懐柔の利得（パンとサーカスの効き）。</summary>
        public readonly float pacificationGain;
        /// <summary>視聴者馴致の利得（到達×娯楽がどれだけ依存を生むか）。</summary>
        public readonly float habituationGain;
        /// <summary>動員放送の利得（到達×信頼×緊急度の総合効果）。</summary>
        public readonly float mobilizationGain;
        /// <summary>露骨さの受容ペナルティ係数（プロパガンダの露骨さが受容を割る重み）。</summary>
        public readonly float crudenessPenalty;

        public StateMediaParams(
            float reachGain,
            float trackRecordWeight,
            float pacificationGain,
            float habituationGain,
            float mobilizationGain,
            float crudenessPenalty)
        {
            this.reachGain = Mathf.Clamp(reachGain, 0f, 4f);
            this.trackRecordWeight = Mathf.Clamp01(trackRecordWeight);
            this.pacificationGain = Mathf.Clamp(pacificationGain, 0f, 4f);
            this.habituationGain = Mathf.Clamp(habituationGain, 0f, 4f);
            this.mobilizationGain = Mathf.Clamp(mobilizationGain, 0f, 4f);
            this.crudenessPenalty = Mathf.Clamp(crudenessPenalty, 0f, 4f);
        }

        public static StateMediaParams Default => new StateMediaParams(
            reachGain: 1f,
            trackRecordWeight: 0.6f,
            pacificationGain: 1f,
            habituationGain: 1f,
            mobilizationGain: 1f,
            crudenessPenalty: 1f);
    }

    /// <summary>
    /// 国営放送ロジック＝国営放送・公式報道機関の運営と国民への効果の純数値モデル。
    /// 放送インフラと人口で到達範囲が決まり、実績と透明性が公式発表の信頼度を支える。娯楽の質と到達は
    /// 「パンとサーカス」的な懐柔を生み、報道と娯楽のバランス（報道比率）が偏りを示す。到達と娯楽は視聴者を
    /// 馴致（依存）させ、緊急時には到達×信頼×緊急度で動員放送が効く。最終的に信頼と馴致が、露骨なプロパガンダで
    /// 割り引かれつつメッセージ受容を決める。
    ///
    /// 盤面非依存・plain 引数・決定論・実効値パターン。報道統制（支配構造）の MediaControlRules とは別系統＝
    /// 本ルールは「国営メディアの運営と国民への効果」の係数モデルに特化する。
    /// </summary>
    public static class StateMediaRules
    {
        /// <summary>
        /// 放送の到達範囲 0..1＝放送インフラ×人口（到達母数）に reachGain を掛けてクランプ。
        /// インフラと人口の両方が無いと届かない（積で効く）。
        /// </summary>
        public static float BroadcastReach(float infrastructure, float population, StateMediaParams p)
        {
            float infra = Mathf.Clamp01(infrastructure);
            float pop = Mathf.Clamp01(population);
            return Mathf.Clamp01(infra * pop * p.reachGain);
        }

        public static float BroadcastReach(float infrastructure, float population)
            => BroadcastReach(infrastructure, population, StateMediaParams.Default);

        /// <summary>
        /// 公式発表の信頼度 0..1＝実績と透明性の重み付き合成。
        /// 嘘の前科（低実績）か不透明さがあると信頼は積み上がらない。
        /// </summary>
        public static float OfficialCredibility(float trackRecord, float transparency, StateMediaParams p)
        {
            float track = Mathf.Clamp01(trackRecord);
            float trans = Mathf.Clamp01(transparency);
            return Mathf.Clamp01(track * p.trackRecordWeight + trans * (1f - p.trackRecordWeight));
        }

        public static float OfficialCredibility(float trackRecord, float transparency)
            => OfficialCredibility(trackRecord, transparency, StateMediaParams.Default);

        /// <summary>
        /// 娯楽による懐柔 0..1＝娯楽の質×放送到達（パンとサーカス）。
        /// 良質な娯楽が広く届くほど不満をそらす。pacificationGain で利得。
        /// </summary>
        public static float EntertainmentPacification(float entertainmentQuality, float broadcastReach, StateMediaParams p)
        {
            float quality = Mathf.Clamp01(entertainmentQuality);
            float reach = Mathf.Clamp01(broadcastReach);
            return Mathf.Clamp01(quality * reach * p.pacificationGain);
        }

        public static float EntertainmentPacification(float entertainmentQuality, float broadcastReach)
            => EntertainmentPacification(entertainmentQuality, broadcastReach, StateMediaParams.Default);

        /// <summary>
        /// 報道比率のバランス指標 -1..1＝報道に偏れば +（プロパガンダ過多）、娯楽に偏れば -（娯楽過多）。
        /// newsShare 0.5 で中庸 0。2×newsShare-1 で符号付きに写す。
        /// </summary>
        public static float NewsToEntertainmentRatio(float newsShare, StateMediaParams p)
        {
            float share = Mathf.Clamp01(newsShare);
            return Mathf.Clamp(2f * share - 1f, -1f, 1f);
        }

        public static float NewsToEntertainmentRatio(float newsShare)
            => NewsToEntertainmentRatio(newsShare, StateMediaParams.Default);

        /// <summary>
        /// 視聴者の馴致（依存）0..1＝放送到達×娯楽懐柔。
        /// 広く届く娯楽漬けほど視聴が習慣化し依存する。habituationGain で利得。
        /// </summary>
        public static float ViewerHabituation(float broadcastReach, float entertainmentPacification, StateMediaParams p)
        {
            float reach = Mathf.Clamp01(broadcastReach);
            float pac = Mathf.Clamp01(entertainmentPacification);
            return Mathf.Clamp01(reach * pac * p.habituationGain);
        }

        public static float ViewerHabituation(float broadcastReach, float entertainmentPacification)
            => ViewerHabituation(broadcastReach, entertainmentPacification, StateMediaParams.Default);

        /// <summary>
        /// 動員放送の効果 0..1＝放送到達×公式信頼×緊急度。
        /// 広く届き、信じられ、事態が切迫しているほど国民を動かせる（積で効く）。mobilizationGain で利得。
        /// </summary>
        public static float MobilizationBroadcast(float broadcastReach, float officialCredibility, float urgency, StateMediaParams p)
        {
            float reach = Mathf.Clamp01(broadcastReach);
            float cred = Mathf.Clamp01(officialCredibility);
            float urg = Mathf.Clamp01(urgency);
            return Mathf.Clamp01(reach * cred * urg * p.mobilizationGain);
        }

        public static float MobilizationBroadcast(float broadcastReach, float officialCredibility, float urgency)
            => MobilizationBroadcast(broadcastReach, officialCredibility, urgency, StateMediaParams.Default);

        /// <summary>
        /// メッセージの受容 0..1＝公式信頼×視聴者馴致を、露骨なプロパガンダで割り引く。
        /// 信頼され馴致した視聴者ほど受け入れるが、露骨さ（propagandaIntensity）が大きいほど分母で割られる。
        /// </summary>
        public static float MessageAcceptance(float officialCredibility, float viewerHabituation, float propagandaIntensity, StateMediaParams p)
        {
            float cred = Mathf.Clamp01(officialCredibility);
            float hab = Mathf.Clamp01(viewerHabituation);
            float crude = Mathf.Clamp01(propagandaIntensity);
            float denom = 1f + crude * p.crudenessPenalty;
            return Mathf.Clamp01(cred * hab / denom);
        }

        public static float MessageAcceptance(float officialCredibility, float viewerHabituation, float propagandaIntensity)
            => MessageAcceptance(officialCredibility, viewerHabituation, propagandaIntensity, StateMediaParams.Default);

        /// <summary>
        /// 国営メディアが機能しているか＝メッセージ受容が閾値を超えるか（true＝国民に届き受容されている）。
        /// </summary>
        public static bool IsStateMediaEffective(float messageAcceptance, float threshold)
        {
            float acc = Mathf.Clamp01(messageAcceptance);
            return acc > threshold;
        }

        public static bool IsStateMediaEffective(float messageAcceptance)
            => IsStateMediaEffective(messageAcceptance, 0.5f);
    }
}
