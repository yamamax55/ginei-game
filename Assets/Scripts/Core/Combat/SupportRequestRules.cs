using UnityEngine;

namespace Ginei
{
    /// <summary>支援要請の顛末。</summary>
    public enum SupportRequestOutcome
    {
        /// <summary>まだ相手が返事をしていない。</summary>
        検討中,
        /// <summary>相手が応じた＝要請どおり動く。</summary>
        承諾,
        /// <summary>相手が断った。</summary>
        拒否,
        /// <summary>返事が来ないまま期限切れ＝流れた。</summary>
        失効,
    }

    /// <summary>要請できる命令の種類（会戦で他系統へ頼めるもの）。</summary>
    public enum SupportOrderKind
    {
        移動,
        攻撃,
        陣形変更,
    }

    /// <summary>支援要請の調整値。</summary>
    public readonly struct SupportRequestParams
    {
        /// <summary>相手が返事をするまでの秒数（game-time）。</summary>
        public readonly float replySeconds;
        /// <summary>返事が来ないまま流れるまでの秒数（<see cref="replySeconds"/> より後）。</summary>
        public readonly float expireSeconds;
        /// <summary>これ以上の「応じる気」で承諾する（0..1）。</summary>
        public readonly float acceptThreshold;

        public SupportRequestParams(float replySeconds, float expireSeconds, float acceptThreshold)
        {
            this.replySeconds = Mathf.Max(0f, replySeconds);
            this.expireSeconds = Mathf.Max(this.replySeconds, expireSeconds);
            this.acceptThreshold = Mathf.Clamp01(acceptThreshold);
        }

        /// <summary>既定＝4秒で返事・12秒で失効・応じる気0.5以上で承諾。</summary>
        public static SupportRequestParams Default => new SupportRequestParams(4f, 12f, 0.5f);
    }

    /// <summary>
    /// 会戦で<b>指揮系統の外</b>の部隊へ出す支援要請（GitHub #67）。
    ///
    /// <b>通知だけでは未完成</b>：受理し、相手の指揮官が判断し、応じたら<b>実際にその命令が実行され</b>、
    /// 断られた／流れたらそう返る、までが要請。ここはその判断部分（純ロジック）。
    ///
    /// <b>約束</b>
    /// <list type="bullet">
    ///   <item>応否は<b>相手の資質と状況</b>から決める（乱数ではない＝同じ状況なら同じ答え）。</item>
    ///   <item>同じ部隊への同種の要請は<b>1件だけ</b>（重ねて出しても増えない＝重複実行しない）。</item>
    ///   <item>相手が戦闘不能・撤退したら要請は<b>失効</b>（勝手に代理で動かさない）。</item>
    /// </list>
    /// </summary>
    public static class SupportRequestRules
    {
        /// <summary>
        /// 相手が応じる気（0..1・決定論）。
        /// 統率が高いほど戦線の都合を汲んで応じ、士気が低い・自分が交戦中だと応じにくい。
        /// </summary>
        public static float Willingness(int leadership, float morale01, bool busyFighting)
        {
            float ability = Mathf.Clamp01(leadership / 100f);
            float calm = Mathf.Clamp01(morale01);
            float w = ability * 0.6f + calm * 0.4f;
            if (busyFighting) w -= 0.25f;      // 自分が交戦中なら余裕がない
            return Mathf.Clamp01(w);
        }

        /// <summary>
        /// 要請の顛末を判定する。<paramref name="elapsedSeconds"/>＝要請してからの game-秒。
        /// <paramref name="targetAlive"/>＝要請先がまだ戦えるか（false＝失効）。
        /// </summary>
        public static SupportRequestOutcome Judge(bool targetAlive, float elapsedSeconds, float willingness,
                                                  in SupportRequestParams p)
            => Judge(targetAlive, true, elapsedSeconds, willingness, p);

        /// <summary>
        /// 顛末を判定する（命令そのものの対象も見る版・#67 第5次）。
        /// <paramref name="orderTargetValid"/>＝要請した命令の<b>対象</b>（攻撃目標など）がまだ有効か。
        ///
        /// ★対象が消えた要請は<b>返事を待たずに失効</b>させる。
        /// 承諾してから「実は目標が無い」と分かるより、流れたと返すほうが実際に近く、
        /// 「応じました」だけ出て何も起きない事故も起きない。
        /// </summary>
        public static SupportRequestOutcome Judge(bool targetAlive, bool orderTargetValid,
                                                  float elapsedSeconds, float willingness,
                                                  in SupportRequestParams p)
        {
            if (!targetAlive) return SupportRequestOutcome.失効;
            if (!orderTargetValid) return SupportRequestOutcome.失効;
            if (elapsedSeconds >= p.expireSeconds) return SupportRequestOutcome.失効;
            if (elapsedSeconds < p.replySeconds) return SupportRequestOutcome.検討中;
            return willingness >= p.acceptThreshold ? SupportRequestOutcome.承諾 : SupportRequestOutcome.拒否;
        }

        /// <summary>顛末を知らせる1行。</summary>
        public static string OutcomeText(SupportRequestOutcome outcome, string targetName, SupportOrderKind kind)
        {
            string who = string.IsNullOrEmpty(targetName) ? "指揮系統外の部隊" : targetName;
            switch (outcome)
            {
                case SupportRequestOutcome.承諾: return $"{who} が{kind}の要請に応じました";
                case SupportRequestOutcome.拒否: return $"{who} は{kind}の要請を断りました";
                case SupportRequestOutcome.失効: return $"{who} への{kind}の要請は流れました";
                default: return $"{who} が{kind}の要請を検討しています";
            }
        }

        /// <summary>要請を出したときの1行（命令ではないと分かる言い方）。</summary>
        public static string SentText(string targetName, SupportOrderKind kind)
        {
            string who = string.IsNullOrEmpty(targetName) ? "指揮系統外の部隊" : targetName;
            return $"{who} へ{kind}を要請しました（応じるかは相手の判断）";
        }

        /// <summary>同じ要請を重ねて出したときの1行（増やさない）。</summary>
        public static string DuplicateText(string targetName, SupportOrderKind kind)
        {
            string who = string.IsNullOrEmpty(targetName) ? "その部隊" : targetName;
            return $"{who} への{kind}の要請はすでに出しています（返事待ち）";
        }
    }
}
