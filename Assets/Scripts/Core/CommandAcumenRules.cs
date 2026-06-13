using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 提督の運営・情報を会戦で活かす純ロジック（ADM-1 #2302）。会戦でほぼ死んでいた `operation`（運営）・
    /// `intelligence`（情報）に効き先を与える＝有能な運営官の艦隊は崩れても立て直し、明察な提督は不意打ちを受けにくい。
    /// 数式は既存窓口（`FleetSustainment`/`Squadron` 再編・`DetectionRules`#2180）へ橋渡しする係数を返すだけ。
    /// 実効値パターン（基準非破壊）・能力50で等倍・test-first。
    /// </summary>
    public static class CommandAcumenRules
    {
        /// <summary>運営→継戦補正（50で1.0／100で1.25／0で0.75）。崩れた戦術単位の立て直しに効く。</summary>
        public static float SustainmentFactor(float operation)
            => 1f + (Mathf.Clamp(operation, 0f, 100f) - 50f) / 200f;

        /// <summary>運営→再編・穴埋めの速さ（陣形変更/配下艦の再配置の速度倍率）。50で1.0／100で1.25。</summary>
        public static float ReformSpeedFactor(float operation)
            => 1f + (Mathf.Clamp(operation, 0f, 100f) - 50f) / 200f;

        /// <summary>情報→索敵範囲倍率（50で1.0／100で1.5／0で0.5）。`DetectionRules`#2180 の基準範囲に乗る。</summary>
        public static float DetectionRangeFactor(float intelligence)
            => 1f + (Mathf.Clamp(intelligence, 0f, 100f) - 50f) / 100f;

        /// <summary>
        /// 情報→不意打ち耐性（0..1）。受ける不意打ちボーナス（#2180）をこの割合だけ打ち消す。
        /// 50で0（並）／100で1.0（完全看破）／50未満は0。明察な提督は伏兵を見破る。
        /// </summary>
        public static float AmbushResistance(float intelligence)
            => Mathf.Clamp01((Mathf.Clamp(intelligence, 0f, 100f) - 50f) / 50f);

        /// <summary>不意打ち倍率に耐性を適用した実効倍率（基準 ambushFactor を耐性で 1.0 へ近づける）。</summary>
        public static float ResolveAmbushFactor(float ambushFactor, float intelligence)
            => Mathf.Lerp(ambushFactor, 1f, AmbushResistance(intelligence));
    }
}
