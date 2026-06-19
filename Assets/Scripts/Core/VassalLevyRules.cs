using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 封臣の軍役（#封臣軍役）の調整値＝門閥貴族・封臣が私兵を率いて参集する封建的動員のパラメータ。
    /// 動員義務の係数・応召の忠誠感度・私兵の質の重み・私利の重み・拒否の感度・連合の結束/指揮の難しさの係数。すべて ctor で安全側へクランプ。
    /// </summary>
    public readonly struct VassalLevyParams
    {
        /// <summary>封土の大きさ→動員義務（兵数）への変換係数（封土1単位あたりの徴募兵数）。</summary>
        public readonly float obligationPerFief;
        /// <summary>応召率の最低保証（忠誠0でもこの割合は参集する＝義務の重み）。</summary>
        public readonly float musterFloor;
        /// <summary>私兵の質に富が効く重み（0..1。残りが武の伝統の重み）。</summary>
        public readonly float wealthWeight;
        /// <summary>動員拒否の感度（(1-忠誠)×主君の弱さ×私利 への倍率）。</summary>
        public readonly float refusalScale;
        /// <summary>封臣間の対立が連合の結束を削る重み（0..1）。</summary>
        public readonly float rivalryWeight;
        /// <summary>連合軍の指揮の難しさが封臣数に効く対数的減衰の基準（この数を境に難度が立つ）。</summary>
        public readonly float commandSpanBase;
        /// <summary>自治の主張が指揮の難しさを増す重み（0..1）。</summary>
        public readonly float autonomyWeight;

        public VassalLevyParams(float obligationPerFief, float musterFloor, float wealthWeight,
            float refusalScale, float rivalryWeight, float commandSpanBase, float autonomyWeight)
        {
            this.obligationPerFief = Mathf.Clamp(obligationPerFief, 0f, 100000f);
            this.musterFloor = Mathf.Clamp01(musterFloor);
            this.wealthWeight = Mathf.Clamp01(wealthWeight);
            this.refusalScale = Mathf.Clamp(refusalScale, 0f, 4f);
            this.rivalryWeight = Mathf.Clamp01(rivalryWeight);
            this.commandSpanBase = Mathf.Clamp(commandSpanBase, 1f, 1000f);
            this.autonomyWeight = Mathf.Clamp01(autonomyWeight);
        }

        /// <summary>既定：封土係数1000・応召最低0.2・質は富0.5/伝統0.5・拒否感度1.0・対立重み0.5・指揮基準8・自治重み0.5。</summary>
        public static VassalLevyParams Default => new VassalLevyParams(
            DefaultObligationPerFief, DefaultMusterFloor, DefaultWealthWeight,
            DefaultRefusalScale, DefaultRivalryWeight, DefaultCommandSpanBase, DefaultAutonomyWeight);

        public const float DefaultObligationPerFief = 1000f;
        public const float DefaultMusterFloor = 0.2f;
        public const float DefaultWealthWeight = 0.5f;
        public const float DefaultRefusalScale = 1.0f;
        public const float DefaultRivalryWeight = 0.5f;
        public const float DefaultCommandSpanBase = 8f;
        public const float DefaultAutonomyWeight = 0.5f;
    }

    /// <summary>
    /// 封臣の軍役の純ロジック（#封臣軍役・唯一の窓口）＝<b>門閥貴族・封臣が私兵を率いて参集する封建的動員</b>。
    /// 封土に応じた動員義務（<see cref="LevyObligation"/>）に対し、封臣の忠誠で応召率が決まり（<see cref="MusterResponse"/>）、
    /// 私兵の質は封臣の富と武の伝統でばらつく（<see cref="RetinueQuality"/>）。封臣には私利があり（<see cref="VassalSelfInterest"/>）、
    /// 忠誠が薄く主君が弱く私利が勝てば動員を拒否する（<see cref="DefiantRefusal"/>）。
    /// 貴族連合（リップシュタット型）は共通の敵で結束するが封臣間の対立で緩み（<see cref="CoalitionCohesion"/>）、
    /// 封臣数が多く各々が自治を主張すれば烏合の衆になり指揮が難しい（<see cref="CommandDifficulty"/>）。
    /// 最終的な封建軍の実効戦力（<see cref="FeudalArmyStrength"/>）は参集×質×結束。
    /// 盤面非依存の plain 引数・実効値パターン（基準値非破壊）・各メソッドに Params 明示版＋Default 委譲版・決定論・test-first。
    /// </summary>
    public static class VassalLevyRules
    {
        /// <summary>ゼロ割回避の微小値。</summary>
        public const float Epsilon = 0.0001f;

        // ── 動員義務 ──────────────────────────────────────────────

        /// <summary>封土の大きさ×軍役契約で動員義務（兵数）。契約は0..1（封建契約の強さ）。</summary>
        public static float LevyObligation(float fiefSize, float feudalContract)
            => LevyObligation(fiefSize, feudalContract, VassalLevyParams.Default);

        /// <summary>動員義務（兵数・非負）＝封土×契約×obligationPerFief。負入力は0扱い。</summary>
        public static float LevyObligation(float fiefSize, float feudalContract, VassalLevyParams p)
        {
            float fief = Mathf.Max(0f, fiefSize);
            float contract = Mathf.Clamp01(feudalContract);
            return fief * contract * p.obligationPerFief;
        }

        // ── 応召 ──────────────────────────────────────────────────

        /// <summary>義務×封臣の忠誠で実際の参集（兵数）。忠誠は0..1・応召率は musterFloor..1。</summary>
        public static float MusterResponse(float levyObligation, float vassalLoyalty)
            => MusterResponse(levyObligation, vassalLoyalty, VassalLevyParams.Default);

        /// <summary>参集（兵数・非負）＝義務×応召率。応召率=Lerp(musterFloor,1,忠誠)。忠誠0でも最低分は来る。</summary>
        public static float MusterResponse(float levyObligation, float vassalLoyalty, VassalLevyParams p)
        {
            float obligation = Mathf.Max(0f, levyObligation);
            float loyalty = Mathf.Clamp01(vassalLoyalty);
            float responseRate = Mathf.Lerp(p.musterFloor, 1f, loyalty);
            return obligation * responseRate;
        }

        // ── 私兵の質 ──────────────────────────────────────────────

        /// <summary>封臣の富×武の伝統で私兵の質（0..1）。富/伝統は0..1。質はばらつく。</summary>
        public static float RetinueQuality(float vassalWealth, float martialTradition)
            => RetinueQuality(vassalWealth, martialTradition, VassalLevyParams.Default);

        /// <summary>私兵の質（0..1）＝富×wealthWeight ＋ 伝統×(1-wealthWeight)。重み付き平均。</summary>
        public static float RetinueQuality(float vassalWealth, float martialTradition, VassalLevyParams p)
        {
            float wealth = Mathf.Clamp01(vassalWealth);
            float tradition = Mathf.Clamp01(martialTradition);
            return Mathf.Clamp01(wealth * p.wealthWeight + tradition * (1f - p.wealthWeight));
        }

        // ── 私的利害 ──────────────────────────────────────────────

        /// <summary>個人の野心×戦の旨味で封臣の私的利害（0..1）。野心/旨味は0..1。</summary>
        public static float VassalSelfInterest(float personalAmbition, float warProfitability)
            => VassalSelfInterest(personalAmbition, warProfitability, VassalLevyParams.Default);

        /// <summary>私的利害（0..1）＝野心×旨味（積＝両方揃って初めて利害が立つ）。</summary>
        public static float VassalSelfInterest(float personalAmbition, float warProfitability, VassalLevyParams p)
        {
            float ambition = Mathf.Clamp01(personalAmbition);
            float profit = Mathf.Clamp01(warProfitability);
            return Mathf.Clamp01(ambition * profit);
        }

        // ── 動員拒否 ──────────────────────────────────────────────

        /// <summary>(1-忠誠)×主君の弱さ×私利で動員拒否のリスク（0..1）。忠誠/弱さ/私利は0..1。</summary>
        public static float DefiantRefusal(float vassalLoyalty, float lordWeakness, float vassalSelfInterest)
            => DefiantRefusal(vassalLoyalty, lordWeakness, vassalSelfInterest, VassalLevyParams.Default);

        /// <summary>動員拒否（0..1）＝(1-忠誠)×弱さ×私利×refusalScale。忠誠が薄く主君が弱く私利が勝てば背く。</summary>
        public static float DefiantRefusal(float vassalLoyalty, float lordWeakness, float vassalSelfInterest, VassalLevyParams p)
        {
            float disloyalty = 1f - Mathf.Clamp01(vassalLoyalty);
            float weakness = Mathf.Clamp01(lordWeakness);
            float selfInterest = Mathf.Clamp01(vassalSelfInterest);
            return Mathf.Clamp01(disloyalty * weakness * selfInterest * p.refusalScale);
        }

        // ── 連合の結束 ────────────────────────────────────────────

        /// <summary>共通の敵×(1-封臣間の対立)で貴族連合の結束（0..1）。共通の敵/対立は0..1。</summary>
        public static float CoalitionCohesion(float commonEnemy, float vassalRivalries)
            => CoalitionCohesion(commonEnemy, vassalRivalries, VassalLevyParams.Default);

        /// <summary>連合の結束（0..1）＝共通の敵 ×(1 - 対立×rivalryWeight)。共通の敵で結束・対立で緩む。</summary>
        public static float CoalitionCohesion(float commonEnemy, float vassalRivalries, VassalLevyParams p)
        {
            float enemy = Mathf.Clamp01(commonEnemy);
            float rivalry = Mathf.Clamp01(vassalRivalries);
            return Mathf.Clamp01(enemy * (1f - rivalry * p.rivalryWeight));
        }

        // ── 指揮の難しさ ──────────────────────────────────────────

        /// <summary>封臣数×自治の主張で連合軍の指揮の難しさ（0..1・烏合の衆）。自治は0..1。</summary>
        public static float CommandDifficulty(int vassalCount, float autonomyInsistence)
            => CommandDifficulty(vassalCount, autonomyInsistence, VassalLevyParams.Default);

        /// <summary>
        /// 指揮の難しさ（0..1）＝封臣数の混み具合 ×(自治の主張で増幅)。
        /// 数の混み具合＝clamp01((count-1)/commandSpanBase)（1人は0・基準数で1へ飽和）。自治は (1+autonomyWeight×自治) で増幅後クランプ。
        /// </summary>
        public static float CommandDifficulty(int vassalCount, float autonomyInsistence, VassalLevyParams p)
        {
            int count = Mathf.Max(0, vassalCount);
            if (count <= 1) return 0f;
            float crowding = Mathf.Clamp01((count - 1f) / p.commandSpanBase);
            float autonomy = Mathf.Clamp01(autonomyInsistence);
            return Mathf.Clamp01(crowding * (1f + p.autonomyWeight * autonomy));
        }

        // ── 封建軍の実効戦力 ──────────────────────────────────────

        /// <summary>参集×質×結束で封建軍の実効戦力（0..1）。各入力は0..1（参集は正規化済みの応召度）。</summary>
        public static float FeudalArmyStrength(float musterResponse, float retinueQuality, float coalitionCohesion)
            => FeudalArmyStrength(musterResponse, retinueQuality, coalitionCohesion, VassalLevyParams.Default);

        /// <summary>封建軍の実効戦力（0..1）＝参集×質×結束（積＝どれか欠ければ崩れる）。</summary>
        public static float FeudalArmyStrength(float musterResponse, float retinueQuality, float coalitionCohesion, VassalLevyParams p)
        {
            float muster = Mathf.Clamp01(musterResponse);
            float quality = Mathf.Clamp01(retinueQuality);
            float cohesion = Mathf.Clamp01(coalitionCohesion);
            return Mathf.Clamp01(muster * quality * cohesion);
        }
    }
}
