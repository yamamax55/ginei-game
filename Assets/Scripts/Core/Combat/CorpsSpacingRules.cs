using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 軍団スロット間隔の算出（#軍団集結 非重複・純ロジック）。
    /// 実間隔＝Max(最小間隔, 2×最大占有半径＋余白)。<b>調整できるのは最小間隔だけ</b>で、
    /// 占有半径の下限を割って実間隔を詰める（隣の艦隊の配下艦と重ねる）ことはしない。
    /// </summary>
    public static class CorpsSpacingRules
    {
        /// <summary>最小間隔の既定（旧 BattlefieldCommandManager の private const と同値）。</summary>
        public const float DefaultMinSpacing = 7f;

        /// <summary>占有半径の下限に足す余白（旧 private const と同値）。</summary>
        public const float FootprintMargin = 3f;

        /// <summary>占有半径から決まる実間隔の下限（2×最大占有半径＋余白）。</summary>
        public static float FootprintFloor(float maxFootprint) => 2f * maxFootprint + FootprintMargin;

        /// <summary>最小間隔として受け付けられる値か（有限かつ0より大）。</summary>
        public static bool IsValidMinSpacing(float minSpacing)
            => !float.IsNaN(minSpacing) && !float.IsInfinity(minSpacing) && minSpacing > 0f;

        /// <summary>不正な最小間隔は既定へ戻す（実効値パターン＝基準の項目は書き換えない）。</summary>
        public static float EffectiveMinSpacing(float minSpacing)
            => IsValidMinSpacing(minSpacing) ? minSpacing : DefaultMinSpacing;

        /// <summary>指定最小間隔と最大占有半径から実間隔を求め、どちらが効いたかも返す。</summary>
        public static CorpsSpacingResult Resolve(float minSpacing, float maxFootprint)
        {
            float floor = FootprintFloor(maxFootprint);
            return new CorpsSpacingResult(minSpacing, maxFootprint, floor, Mathf.Max(minSpacing, floor));
        }
    }
}
