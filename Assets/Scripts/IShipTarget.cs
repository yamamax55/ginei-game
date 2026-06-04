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

        /// <summary>所属陣営。</summary>
        Faction Faction { get; }

        /// <summary>生存しているか（破棄予定・艦艇数0なら false）。</summary>
        bool IsAlive { get; }

        /// <summary>ダメージ（艦艇数の減少）を与える。</summary>
        void TakeDamage(int damage);
    }
}
