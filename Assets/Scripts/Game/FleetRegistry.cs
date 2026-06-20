using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace Ginei
{
    /// <summary>
    /// 全 IShipTarget（旗艦 FleetStrength ＋配下艦 EscortShip）の在庫を保持する静的レジストリ。
    /// 各艦が出現時(Register)・破棄/退却時(Unregister)に自己登録／解除し、
    /// 敵探索を毎回の FindObjectsByType から「在庫リストの参照＋敵対判定」に置き換えて軽量化する。
    ///
    /// 多勢力対応：陣営別のバケットは持たず、全艦を1リストで保持する。敵味方の区別は
    /// 探索側が `FactionRelations.IsHostile` で判定する（3勢力以上でもコード変更不要）。
    ///
    /// 注意：
    /// - 静的状態のためシーンを跨いで残る。会戦開始時に `BattleSetup.Awake` が `Clear()` する。
    /// - 返すリストは内部参照（読み取り専用）。列挙中に Register/Unregister しないこと（探索は読み取りのみ）。
    /// </summary>
    public static class FleetRegistry
    {
        // 全攻撃対象（旗艦＋配下艦）。ShipCombat の敵探索が参照。
        private static readonly List<IShipTarget> allTargets = new List<IShipTarget>();

        // 旗艦のみ。FleetAI の接近目標・BattleManager の勝敗カウントが参照。
        private static readonly List<FleetStrength> allFlagships = new List<FleetStrength>();

        /// <summary>全攻撃対象（旗艦＋配下艦）。敵味方は呼び出し側が IsHostile で判定する。</summary>
        public static IReadOnlyList<IShipTarget> AllTargets => allTargets;

        /// <summary>全旗艦（生存中のみ。退却・破棄したものは Unregister 済み）。</summary>
        public static IReadOnlyList<FleetStrength> AllFlagships => allFlagships;

        /// <summary>出現した艦を登録する。</summary>
        public static void Register(IShipTarget t)
        {
            if (t == null) return;
            if (!allTargets.Contains(t)) allTargets.Add(t);
            if (t is FleetStrength fs && !allFlagships.Contains(fs)) allFlagships.Add(fs);
        }

        /// <summary>破棄・退却した艦を登録解除する。</summary>
        public static void Unregister(IShipTarget t)
        {
            if (t == null) return;
            allTargets.Remove(t);
            if (t is FleetStrength fs) allFlagships.Remove(fs);
        }

        /// <summary>全リストを空にする（会戦開始時に呼ぶ）。</summary>
        public static void Clear()
        {
            allTargets.Clear();
            allFlagships.Clear();
        }

        // ===== 会戦ごとの隔離（WIN-2 #2569）=====
        // ウィンドウ化会戦（WIN-1）は会戦ごとに別シーンへ additive ロードされる。距離ベースの探索
        // （ShipCombat/ZoneOfControl 等）は遠方オフセットで自然に隔離されるが、勝敗集計・初期化・カメラ
        // フレーミングのように「範囲を問わず全旗艦を数える」処理は会戦をまたいで混ざってしまう。
        // それらは下のシーン限定列挙を使い、自分の会戦シーンの艦だけを対象にする。
        // 単一会戦（フルスクリーン）では当該シーン＝全艦なので従来と同一（後方互換）。

        /// <summary>指定シーンに属する旗艦のみを返す（会戦ごとの勝敗集計・フレーミング用）。</summary>
        public static IReadOnlyList<FleetStrength> FlagshipsIn(Scene scene)
        {
            var list = new List<FleetStrength>();
            for (int i = 0; i < allFlagships.Count; i++)
            {
                FleetStrength fs = allFlagships[i];
                if (fs != null && fs.gameObject.scene == scene) list.Add(fs);
            }
            return list;
        }

        /// <summary>指定シーンに属する攻撃対象（旗艦＋配下艦）のみを返す。</summary>
        public static IReadOnlyList<IShipTarget> TargetsIn(Scene scene)
        {
            var list = new List<IShipTarget>();
            for (int i = 0; i < allTargets.Count; i++)
            {
                IShipTarget t = allTargets[i];
                if (t != null && t.Transform != null && t.Transform.gameObject.scene == scene) list.Add(t);
            }
            return list;
        }

        /// <summary>指定シーンに属する艦だけを在庫から外す（その会戦の開始時クリア。他会戦は残す）。</summary>
        public static void ClearScene(Scene scene)
        {
            allTargets.RemoveAll(t => t == null || (t.Transform != null && t.Transform.gameObject.scene == scene));
            allFlagships.RemoveAll(fs => fs == null || fs.gameObject.scene == scene);
        }
    }
}
