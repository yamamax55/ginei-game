namespace Ginei
{
    /// <summary>
    /// ネームド資産の所有者移動の純ロジック（NASSET-4・#2063・台帳 <see cref="NamedAssetRegistry"/> を編集）。
    /// 相続#152（死亡で相続人へ）・没収#154（征服/捕虜で国家へ）・贈与（人望/外交）・譲渡。
    /// 譲渡不可（称号など <see cref="NamedAsset.transferable"/>=false）はゲートで no-op（false 返し）。test-first。
    /// </summary>
    public static class AssetTransferRules
    {
        /// <summary>譲渡できるか＝transferable ゲート。</summary>
        public static bool CanTransfer(NamedAsset a) => a != null && a.transferable;

        /// <summary>人物へ譲渡（成功で true）。</summary>
        public static bool TransferToPerson(NamedAsset a, int newPersonId)
        {
            if (!CanTransfer(a)) return false;
            a.ownerKind = AssetOwnerKind.人物;
            a.ownerPersonId = newPersonId;
            return true;
        }

        /// <summary>国家へ譲渡（成功で true）。</summary>
        public static bool TransferToFaction(NamedAsset a, Faction faction)
        {
            if (!CanTransfer(a)) return false;
            a.ownerKind = AssetOwnerKind.国家;
            a.ownerFaction = faction;
            return true;
        }

        /// <summary>相続（死亡 LIFE-2 #152＝相続人へ）。相続人不在は呼び側が <see cref="Confiscate"/> を選ぶ。</summary>
        public static bool Inherit(NamedAsset a, int heirPersonId)
            => TransferToPerson(a, heirPersonId);

        /// <summary>没収（征服/捕虜 LIFE-4 #154＝国家へ＝国庫帰属）。</summary>
        public static bool Confiscate(NamedAsset a, Faction faction)
            => TransferToFaction(a, faction);

        /// <summary>贈与（人望/外交＝人物へ）。</summary>
        public static bool Gift(NamedAsset a, int toPersonId)
            => TransferToPerson(a, toPersonId);
    }
}
