using System;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 惑星攻城の戦術アリーナにおける「制空権（軌道防衛＝S-AV／アルテミスの首飾り）」の被弾受け（IShipTarget）。
    /// 攻城艦隊はこれを射撃して削り、0 で <see cref="IsDomainDown"/>＝ドメイン・ダウン（接近限界が解ける）。
    /// FleetRegistry に守備側勢力の個艦として登録され、攻城艦隊の通常索敵（ShipCombat）で自動的に標的になる。
    /// 敵対判定・ダメージ式は ShipCombat / FactionRelations に集約（ここに重複実装しない）。
    /// 戦略側の数値モデル（<see cref="PlanetSiegeRules"/>/<see cref="Planet"/>）とは独立した戦術可視化用ラッパ。
    /// 中心の惑星オブジェクトに <see cref="SiegeArena"/> が付与する。
    /// </summary>
    public class OrbitalDefense : MonoBehaviour, IShipTarget
    {
        private Faction faction;
        private float maxDefense = 1f;
        private float current;
        private bool registered;

        /// <summary>制空権が崩壊したか（ドメイン・ダウン＝接近限界の解除条件）。</summary>
        public bool IsDomainDown => current <= 0f;

        /// <summary>制空権の残り割合(0..1)。リング演出・ラベル表示用。</summary>
        public float Ratio => maxDefense > 0f ? Mathf.Clamp01(current / maxDefense) : 0f;

        /// <summary>制空権が 0 になった瞬間に一度だけ呼ばれる（SiegeArena が接近限界を解く）。</summary>
        public event Action OnDomainDown;

        // ---- IShipTarget ----
        public Transform Transform => transform;
        public Faction Faction => faction;
        public FactionData FactionData => null; // 守備側は enum 判定で十分（攻城側 factionData も null）
        public bool IsAlive => current > 0f;

        /// <summary>
        /// 守備側勢力・制空権の最大値・開始割合(0..1＝戦略側の残り制空権)で初期化し、レジストリへ登録する。
        /// 攻城途中で突入すれば <paramref name="startRatio"/> ぶんだけ既に削れた状態で始まる。
        /// </summary>
        public void Initialize(Faction owner, float max, float startRatio = 1f)
        {
            faction = owner;
            maxDefense = Mathf.Max(1f, max);
            current = maxDefense * Mathf.Clamp01(startRatio);
            if (current <= 0f) current = maxDefense; // 0 はフォールバック扱い（接近限界が即解けないように満タン開始）
            if (!registered) { FleetRegistry.Register(this); registered = true; }
        }

        public void TakeDamage(int damage)
        {
            if (current <= 0f) return;
            current = Mathf.Max(0f, current - Mathf.Max(0, damage));
            if (current <= 0f)
            {
                // ドメイン・ダウン：以後は標的・索敵から除外し、接近限界を解く
                if (registered) { FleetRegistry.Unregister(this); registered = false; }
                OnDomainDown?.Invoke();
            }
        }

        private void OnDestroy()
        {
            if (registered) { FleetRegistry.Unregister(this); registered = false; }
        }
    }
}
