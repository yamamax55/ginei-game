using UnityEngine;

namespace Ginei
{
    /// <summary>条約種別（外交EPIC #189・DIP-2 #191）。</summary>
    public enum TreatyType
    {
        /// <summary>同盟＝共同防衛・最も親密。</summary>
        同盟,
        /// <summary>不可侵＝互いに攻めない約定。</summary>
        不可侵,
        /// <summary>通商＝交易の取り決め。</summary>
        通商,
        /// <summary>通行＝領内通過の許可。</summary>
        通行,
        /// <summary>属国＝従属関係（宗主-臣従）。</summary>
        属国
    }

    /// <summary>
    /// 外交条約の純ロジック（外交EPIC #189・DIP-2 #191・唯一の窓口）。
    /// 条約別の <b>opinion 修正子</b>(<see cref="OpinionEffect"/>)・<b>外交レバレッジ</b>(<see cref="Leverage"/>)・
    /// <b>違約判定</b>(<see cref="IsBreach"/>) を集約する。既存 <see cref="DiplomacyState"/> は編集せず plain 引数＋独自 <see cref="Treaty"/> 型で完結する。
    /// 調整値は <see cref="TreatyParams"/> に集約（マジックナンバー禁止）。test-first。
    /// </summary>
    public static class TreatyRules
    {
        public const float OpinionMin = -100f;
        public const float OpinionMax = 100f;

        /// <summary>条約の調整値（マジックナンバー禁止＝集約）。</summary>
        public readonly struct TreatyParams
        {
            public readonly float allianceOpinion;     // 同盟がもたらす opinion 修正子
            public readonly float nonAggressionOpinion; // 不可侵
            public readonly float tradeOpinion;         // 通商
            public readonly float passageOpinion;       // 通行
            public readonly float vassalOpinion;        // 属国（宗主視点の安定寄与）

            public readonly float allianceLeverage;     // 同盟の外交レバレッジ
            public readonly float nonAggressionLeverage; // 不可侵
            public readonly float tradeLeverage;        // 通商
            public readonly float passageLeverage;      // 通行
            public readonly float vassalLeverage;       // 属国（最も強い拘束）

            public TreatyParams(
                float allianceOpinion, float nonAggressionOpinion, float tradeOpinion, float passageOpinion, float vassalOpinion,
                float allianceLeverage, float nonAggressionLeverage, float tradeLeverage, float passageLeverage, float vassalLeverage)
            {
                this.allianceOpinion = allianceOpinion;
                this.nonAggressionOpinion = nonAggressionOpinion;
                this.tradeOpinion = tradeOpinion;
                this.passageOpinion = passageOpinion;
                this.vassalOpinion = vassalOpinion;

                this.allianceLeverage = Mathf.Max(0f, allianceLeverage);
                this.nonAggressionLeverage = Mathf.Max(0f, nonAggressionLeverage);
                this.tradeLeverage = Mathf.Max(0f, tradeLeverage);
                this.passageLeverage = Mathf.Max(0f, passageLeverage);
                this.vassalLeverage = Mathf.Max(0f, vassalLeverage);
            }

            /// <summary>既定＝opinion 同盟+40/不可侵+20/通商+15/通行+10/属国+25・レバレッジ 同盟0.6/不可侵0.3/通商0.4/通行0.2/属国1.0。</summary>
            public static TreatyParams Default => new TreatyParams(
                40f, 20f, 15f, 10f, 25f,
                0.6f, 0.3f, 0.4f, 0.2f, 1.0f);
        }

        /// <summary>opinion を [-100,100] に丸める。</summary>
        public static float Clamp(float opinion) => Mathf.Clamp(opinion, OpinionMin, OpinionMax);

        /// <summary>条約種別がもたらす opinion 修正子。[-100,100] に丸める。</summary>
        public static float OpinionEffect(TreatyType type, TreatyParams p)
        {
            switch (type)
            {
                case TreatyType.同盟: return Clamp(p.allianceOpinion);
                case TreatyType.不可侵: return Clamp(p.nonAggressionOpinion);
                case TreatyType.通商: return Clamp(p.tradeOpinion);
                case TreatyType.通行: return Clamp(p.passageOpinion);
                case TreatyType.属国: return Clamp(p.vassalOpinion);
                default: return 0f;
            }
        }

        /// <summary>条約種別の外交レバレッジ（拘束の強さ 0..1）。属国が最も強い。</summary>
        public static float Leverage(TreatyType type, TreatyParams p)
        {
            switch (type)
            {
                case TreatyType.同盟: return Mathf.Clamp01(p.allianceLeverage);
                case TreatyType.不可侵: return Mathf.Clamp01(p.nonAggressionLeverage);
                case TreatyType.通商: return Mathf.Clamp01(p.tradeLeverage);
                case TreatyType.通行: return Mathf.Clamp01(p.passageLeverage);
                case TreatyType.属国: return Mathf.Clamp01(p.vassalLeverage);
                default: return 0f;
            }
        }

        /// <summary>既定 Params で外交レバレッジを返す簡易窓口。</summary>
        public static float Leverage(TreatyType type) => Leverage(type, TreatyParams.Default);

        /// <summary>
        /// 指定の行動が条約違反（違約）か。
        /// 同盟/不可侵/属国＝当事者への攻撃が違反。通商＝交易遮断が違反。通行＝領内通過の妨害が違反。
        /// </summary>
        public static bool IsBreach(TreatyType type, TreatyAction action)
        {
            switch (type)
            {
                case TreatyType.同盟:
                case TreatyType.不可侵:
                case TreatyType.属国:
                    return action == TreatyAction.攻撃;
                case TreatyType.通商:
                    return action == TreatyAction.交易遮断;
                case TreatyType.通行:
                    return action == TreatyAction.通行妨害;
                default:
                    return false;
            }
        }
    }

    /// <summary>条約に対する行動（違約判定の入力）。</summary>
    public enum TreatyAction
    {
        /// <summary>当事者への攻撃（武力行使）。</summary>
        攻撃,
        /// <summary>交易の遮断・封鎖。</summary>
        交易遮断,
        /// <summary>領内通過の妨害。</summary>
        通行妨害,
        /// <summary>害のない通常往来。</summary>
        通常往来
    }
}
