using UnityEngine;

namespace Ginei
{
    /// <summary>艦載機（ワルキューレ型）の調整係数。</summary>
    public readonly struct CarrierParams
    {
        /// <summary>艦載機1機あたりの基礎打撃力。</summary>
        public readonly float strikePerCraft;
        /// <summary>防空網が打撃隊を削る最大割合（防空優勢時）。</summary>
        public readonly float maxInterceptRatio;
        /// <summary>出撃1回あたりの艦載機損耗率（防空を抜けても消耗する）。</summary>
        public readonly float sortieAttritionRatio;
        /// <summary>母艦喪失時に帰還できず失われる艦載機の割合。</summary>
        public readonly float orphanLossRatio;

        public CarrierParams(float strikePerCraft, float maxInterceptRatio, float sortieAttritionRatio, float orphanLossRatio)
        {
            this.strikePerCraft = Mathf.Max(0f, strikePerCraft);
            this.maxInterceptRatio = Mathf.Clamp01(maxInterceptRatio);
            this.sortieAttritionRatio = Mathf.Clamp01(sortieAttritionRatio);
            this.orphanLossRatio = Mathf.Clamp01(orphanLossRatio);
        }

        /// <summary>既定＝打撃1/機・迎撃上限80%・出撃損耗5%・母艦喪失時80%喪失。</summary>
        public static CarrierParams Default => new CarrierParams(1f, 0.8f, 0.05f, 0.8f);
    }

    /// <summary>
    /// 艦載機（ワルキューレ型）の純ロジック。打撃力＝機数×搭乗員技量で、敵の防空網（CAP・対空）が
    /// 打撃隊を削る＝航空打撃と防空の綱引き。出撃のたびに消耗し、母艦を失えば艦載機は宿無しになって
    /// 大半が失われる＝母艦は艦載機の命綱。艦種（`ShipClass`＝戦艦/巡航/駆逐の3種）には追加せず
    /// 独立系統で扱う。乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class CarrierRules
    {
        /// <summary>打撃隊の素の打撃力＝機数×単機打撃×搭乗員技量(0..1超可＝エース部隊)。</summary>
        public static float RawStrikePower(int craftCount, float pilotSkill, CarrierParams p)
        {
            return Mathf.Max(0, craftCount) * p.strikePerCraft * Mathf.Max(0f, pilotSkill);
        }

        public static float RawStrikePower(int craftCount, float pilotSkill)
            => RawStrikePower(craftCount, pilotSkill, CarrierParams.Default);

        /// <summary>
        /// 防空網に削られる割合（0..maxInterceptRatio）＝防空力/(防空力＋打撃力)。
        /// 防空が打撃を圧倒すれば上限まで削られ、防空ゼロなら素通し。
        /// </summary>
        public static float InterceptedRatio(float strikePower, float airDefense, CarrierParams p)
        {
            float strike = Mathf.Max(0f, strikePower);
            float def = Mathf.Max(0f, airDefense);
            if (def <= 0f) return 0f;
            if (strike <= 0f) return p.maxInterceptRatio;
            return p.maxInterceptRatio * (def / (def + strike));
        }

        public static float InterceptedRatio(float strikePower, float airDefense)
            => InterceptedRatio(strikePower, airDefense, CarrierParams.Default);

        /// <summary>防空網を抜けた実効打撃＝素の打撃×（1−迎撃割合）。目標へのダメージ計算に使う。</summary>
        public static float EffectiveStrike(float strikePower, float airDefense, CarrierParams p)
        {
            return Mathf.Max(0f, strikePower) * (1f - InterceptedRatio(strikePower, airDefense, p));
        }

        public static float EffectiveStrike(float strikePower, float airDefense)
            => EffectiveStrike(strikePower, airDefense, CarrierParams.Default);

        /// <summary>
        /// 出撃1回の艦載機損耗数＝機数×（出撃損耗率＋迎撃割合×0.5）。防空が固い空へ突っ込むほど帰ってこない。
        /// </summary>
        public static int SortieLosses(int craftCount, float strikePower, float airDefense, CarrierParams p)
        {
            float lossRatio = p.sortieAttritionRatio + InterceptedRatio(strikePower, airDefense, p) * 0.5f;
            return Mathf.FloorToInt(Mathf.Max(0, craftCount) * Mathf.Clamp01(lossRatio));
        }

        public static int SortieLosses(int craftCount, float strikePower, float airDefense)
            => SortieLosses(craftCount, strikePower, airDefense, CarrierParams.Default);

        /// <summary>母艦喪失で失われる艦載機数（宿無し＝大半が回収不能）。残りは他艦・基地に拾われる。</summary>
        public static int OrphanedLosses(int airborneCraft, CarrierParams p)
        {
            return Mathf.FloorToInt(Mathf.Max(0, airborneCraft) * p.orphanLossRatio);
        }

        public static int OrphanedLosses(int airborneCraft) => OrphanedLosses(airborneCraft, CarrierParams.Default);
    }
}
