using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ginei
{
    /// <summary>
    /// 会戦ごとの戦場中心（ワールド座標）レジストリ（WIN-3 #2570）。
    /// ウィンドウ化会戦は会戦ごとに別シーンへ additive ロードされ、戦略マップと同一ワールド空間に同居するため、
    /// 各会戦を**固有の遠方オフセット**へ置いて互いに/戦略に映り込まないよう隔離する。
    /// 戦場中心はシーン単位で持ち、FleetMovement の境界・FleetAI の離脱端・CameraController の境界が参照する。
    ///
    /// 単一会戦（フルスクリーン）では登録が無い＝(0,0)＝従来動作（後方互換）。
    /// <see cref="PendingOrigin"/> は次にロードする会戦の中心で、BattleSetup が additive ロード中に読んで
    /// 自分のシーンへ確定登録する（ロード前に BattleWindow/BattleDirector が設定する）。
    /// </summary>
    public static class BattleField
    {
        private static readonly Dictionary<Scene, Vector2> origins = new Dictionary<Scene, Vector2>();

        /// <summary>次にロードする会戦の戦場中心（ロード前に設定）。BattleSetup が自シーンへ確定登録する。</summary>
        public static Vector2 PendingOrigin;

        /// <summary>シーンの戦場中心を確定登録する（BattleSetup が呼ぶ）。</summary>
        public static void Register(Scene scene, Vector2 origin) => origins[scene] = origin;

        /// <summary>シーンの戦場中心を返す（未登録＝フルスクリーン会戦は (0,0)）。</summary>
        public static Vector2 OriginFor(Scene scene)
            => origins.TryGetValue(scene, out Vector2 o) ? o : Vector2.zero;

        /// <summary>シーンの登録を解除する（会戦ウィンドウを閉じてアンロードしたとき）。</summary>
        public static void ClearScene(Scene scene) => origins.Remove(scene);
    }
}
