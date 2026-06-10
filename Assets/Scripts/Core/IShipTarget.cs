using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 攻撃対象になれる個艦（旗艦＝FleetStrength／配下艦＝EscortShip）の共通インターフェイス。
    /// FleetWeapon はこのインターフェイス越しに最寄りの敵個艦を撃つ。
    /// </summary>
    public interface IShipTarget
    {
        /// <summary>対象の Transform（位置・向きの取得用）。</summary>
        Transform Transform { get; }

        /// <summary>所属陣営（旧 enum。後方互換・UI・セーブ用）。</summary>
        Faction Faction { get; }

        /// <summary>所属勢力データ（多勢力対応の出所。未割り当てなら null＝enum にフォールバック）。</summary>
        FactionData FactionData { get; }

        /// <summary>生存しているか（破棄予定・艦艇数0なら false）。</summary>
        bool IsAlive { get; }

        /// <summary>ダメージ（艦艇数の減少）を与える。</summary>
        void TakeDamage(int damage);
    }
}
