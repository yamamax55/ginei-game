namespace Ginei
{
    /// <summary>
    /// UnityEngine.Object の実行時同一性キー（64bit）を取る単一窓口。成長 <see cref="GrowthRegistry"/>／武名
    /// <see cref="FameRegistry"/>／叙勲 <see cref="MedalRegistry"/> の台帳キー、および会戦中の艦隊紐付け
    /// （<see cref="Allegiance"/>／CommandCandidate／DeploymentCandidate）は<b>すべてこの窓口</b>から採る＝
    /// 台帳とセーブ復元で同じキーになることを保証する（二重実装しない）。
    ///
    /// Unity 6000.6 で <c>Object.GetInstanceID()</c> が obsolete（CS0619＝エラー）になり <c>GetEntityId()</c> へ
    /// 置き換わった。<c>EntityId</c> は 64bit（<c>ulong</c> raw）で、<c>int</c> への暗黙変換は<b>切り詰め</b>になるため
    /// 使わない。ここでは raw を <c>long</c> のまま返し、人物・艦隊の識別が衝突しないようにする。
    /// 6000.4 以前（<c>GetEntityId</c> 非存在）と TestHarness（Unity 無し・Stubs）では従来の
    /// <c>GetInstanceID()</c>（int）へフォールバックし、long へ拡大変換する＝どちらでも同じ意味のキーになる。
    /// </summary>
    public static class EntityKey
    {
        /// <summary>未割当（null・破棄済み）を表すキー。台帳の「登録なし」判定に使う。</summary>
        public const long None = 0L;

        /// <summary>obj の同一性キー（null／破棄済みは <see cref="None"/>）。</summary>
        public static long Of(UnityEngine.Object obj)
        {
            if (obj == null) return None; // Unity の == オーバーロード＝破棄済みも null 扱い
#if UNITY_6000_6_OR_NEWER
            return unchecked((long)UnityEngine.EntityId.ToULong(obj.GetEntityId()));
#else
            return obj.GetInstanceID();
#endif
        }
    }
}
