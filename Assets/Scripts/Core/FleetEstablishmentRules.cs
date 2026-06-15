using UnityEngine;

namespace Ginei
{
    /// <summary>艦隊編制の建議の種別（設立＝総プールから新たに艦隊を起こす／解散＝現役艦隊を解いて兵力をプールへ戻す）。</summary>
    public enum FleetProposalKind { 設立, 解散 }

    /// <summary>
    /// 艦隊の設立・解散の建議＝<b>稟議の中身</b>（純データ・非 MonoBehaviour）。建艦で増えた勢力プール
    /// （<see cref="FleetPool"/>）を新艦隊へ配分（設立）し、空になった艦隊を解いて兵力をプールへ戻す（解散）動きを、
    /// 官僚機構の稟議（<see cref="Petition"/>/<see cref="RingiPipeline"/>）に載せて承認制で動かすための提案オブジェクト。
    /// 検証・執行・稟議への橋渡しは <see cref="FleetEstablishmentRules"/> が唯一の窓口（並行新設しない）。
    /// </summary>
    public class FleetProposal
    {
        /// <summary>建議する勢力。</summary>
        public Faction faction;
        /// <summary>設立か解散か。</summary>
        public FleetProposalKind kind;
        /// <summary>解散：対象艦隊番号。設立：払い出し後に実番号が書き戻る（0=自動採番）。</summary>
        public int fleetNumber;
        /// <summary>設立：総プールから割く兵力（隻）。解散では使わない。</summary>
        public int strength;
        /// <summary>設立：任意の固有名（空＝「第N艦隊」）。</summary>
        public string fleetName = "";
        /// <summary>起案者の人物 id（稟議の建白者・0=匿名/制度発議）。</summary>
        public int proposerId;

        public FleetProposal() { }

        public FleetProposal(Faction faction, FleetProposalKind kind, int strength = 0, int fleetNumber = 0,
            string fleetName = null, int proposerId = 0)
        {
            this.faction = faction;
            this.kind = kind;
            this.strength = strength;
            this.fleetNumber = fleetNumber;
            this.fleetName = fleetName ?? "";
            this.proposerId = proposerId;
        }

        /// <summary>表示用の要約（稟議カードの題名に使う）。</summary>
        public string Summary => kind == FleetProposalKind.設立
            ? (string.IsNullOrEmpty(fleetName)
                ? $"新艦隊の設立（{strength}隻規模）"
                : $"{fleetName}の設立（{strength}隻規模）")
            : $"第{fleetNumber}艦隊の解散";
    }

    /// <summary>
    /// 艦隊の設立・解散の唯一の窓口（純ロジック・test-first）。検証（プール残・規模・在席）と執行を担い、
    /// 在庫は <see cref="FleetRoster"/>（台帳）・<see cref="FleetPool"/>/<see cref="FleetPoolRules"/>（総艦艇と配分）へ委譲する
    /// （並行レジストリを作らない）。<b>総プール（保有総艦艇）は設立/解散で増減しない</b>＝設立は available から配分し、
    /// 解散は配分を解いて available を回復させるだけ（プール総数は建艦でのみ増える）。承認制で動かすため
    /// <see cref="ToPetition"/>/<see cref="ExecuteFromPetition"/> で <see cref="Petition"/> と相互変換できる（稟議の橋渡し）。
    /// </summary>
    public static class FleetEstablishmentRules
    {
        /// <summary>一個艦隊の標準規模（隻・中将級＝<see cref="CommandCapacityRules.Cap中将"/> 目安）。AIの自動建議が使う。</summary>
        public const int StandardFleetStrength = 12000;
        /// <summary>設立に要る最小兵力（これ未満では艦隊を起こさない＝骨抜きで0隻艦隊を量産しない）。</summary>
        public const int MinFleetStrength = 1000;

        /// <summary>稟議の効果キー接頭辞（直列化・<see cref="Petition.effectKey"/> 用）。</summary>
        public const string EffectEstablish = "fleet.establish";
        public const string EffectDisband = "fleet.disband";

        // ===== 設立 =====

        /// <summary>総プール残（<paramref name="totalPool"/>）から <paramref name="strength"/> 隻を割けるか（兵力&gt;0 かつ available 以内）。</summary>
        public static bool CanEstablish(Faction f, int strength, int totalPool)
            => strength > 0 && FleetPoolRules.Available(f, totalPool) >= strength;

        /// <summary><see cref="FleetPool"/> の総プールを基準に設立可否を判定する。</summary>
        public static bool CanEstablish(Faction f, int strength)
            => CanEstablish(f, strength, FleetPool.Get(f));

        /// <summary>
        /// 新艦隊を設立する（番号自動採番＋プールから兵力を配分）。プール残が足りなければ null（台帳を汚さない）。
        /// 成功すると現役の <see cref="FleetUnitData"/> を返す（指揮官は空席＝別途配属）。
        /// </summary>
        public static FleetUnitData Establish(Faction f, int strength, string name = null, int totalPool = -1)
        {
            int pool = totalPool >= 0 ? totalPool : FleetPool.Get(f);
            if (!CanEstablish(f, strength, pool)) return null;

            var unit = FleetRoster.CreateFleet(f, 0, name);
            if (unit == null) return null;
            if (!FleetPoolRules.SetAllocation(unit, strength, pool))
            {
                // 配分に失敗（理論上 CanEstablish 済みなら起きない）＝起こした艦隊を畳んで巻き戻す。
                FleetRoster.Disband(f, unit.fleetNumber);
                return null;
            }
            return unit;
        }

        // ===== 解散 =====

        /// <summary>その番号の艦隊を解散できるか＝在席し<b>現役</b>であること（永久欠番・解隊済みは不可）。</summary>
        public static bool CanDisband(Faction f, int fleetNumber)
        {
            var u = FleetRoster.GetFleet(f, fleetNumber);
            return u != null && u.IsActive;
        }

        /// <summary>
        /// 現役艦隊を解散する（status=解隊・指揮官は空席化）。割当兵力は <see cref="FleetPoolRules.Available"/> へ
        /// 自動的に戻る（解隊は IsActive=false ＝割当合計から外れる／総プールは不変）。
        /// </summary>
        public static bool Disband(Faction f, int fleetNumber)
            => CanDisband(f, fleetNumber) && FleetRoster.Disband(f, fleetNumber);

        // ===== 建議（FleetProposal）の検証・執行 =====

        /// <summary>建議が今この瞬間に実行可能か（設立=プール残／解散=現役在席）。</summary>
        public static bool CanExecute(FleetProposal p)
        {
            if (p == null) return false;
            return p.kind == FleetProposalKind.設立
                ? CanEstablish(p.faction, p.strength)
                : CanDisband(p.faction, p.fleetNumber);
        }

        /// <summary>
        /// 建議を執行する（承認後）。設立は <paramref name="magnitude"/>（官僚の執行忠実度0..1＝骨抜き）で配分兵力を
        /// 値切る（<see cref="MinFleetStrength"/> 下限・プール残上限でクランプ）＝通っても満額の艦隊は揃わない。
        /// 解散は magnitude 不問の二値。成功で true（設立時は <see cref="FleetProposal.fleetNumber"/> に実番号を書き戻す）。
        /// </summary>
        public static bool Execute(FleetProposal p, float magnitude = 1f)
        {
            if (p == null) return false;
            if (p.kind == FleetProposalKind.解散)
                return Disband(p.faction, p.fleetNumber);

            // 設立：執行忠実度で兵力を値切り、プール残でクランプ。最小規模に満たなければ不発。
            int avail = FleetPoolRules.Available(p.faction);
            int want = Mathf.RoundToInt(p.strength * Mathf.Clamp01(magnitude));
            int eff = Mathf.Min(Mathf.Max(want, MinFleetStrength), avail);
            if (eff < MinFleetStrength) return false;

            var unit = Establish(p.faction, eff, p.fleetName);
            if (unit == null) return false;
            p.fleetNumber = unit.fleetNumber; // 実番号を書き戻す（通知・追跡用）
            return true;
        }

        // ===== 稟議（Petition）への橋渡し =====

        /// <summary>建議の効果キーを直列化する（<see cref="Petition.effectKey"/> 用・"fleet.establish:12000" / "fleet.disband:3"）。</summary>
        public static string EncodeEffectKey(FleetProposal p)
        {
            if (p == null) return "";
            return p.kind == FleetProposalKind.設立
                ? $"{EffectEstablish}:{Mathf.Max(0, p.strength)}"
                : $"{EffectDisband}:{Mathf.Max(0, p.fleetNumber)}";
        }

        /// <summary>効果キーから建議を復元する（不正・非艦隊キーは null）。勢力は呼び出し側（陳情）から渡す。</summary>
        public static FleetProposal Decode(Faction f, string effectKey)
        {
            if (string.IsNullOrEmpty(effectKey)) return null;
            int colon = effectKey.IndexOf(':');
            if (colon <= 0) return null;
            string head = effectKey.Substring(0, colon);
            if (!int.TryParse(effectKey.Substring(colon + 1), out int n)) return null;

            if (head == EffectEstablish) return new FleetProposal(f, FleetProposalKind.設立, strength: n);
            if (head == EffectDisband) return new FleetProposal(f, FleetProposalKind.解散, fleetNumber: n);
            return null;
        }

        /// <summary>建議を承認制の陳情（<see cref="Petition"/>・origin=建白）へ包む。effectKey に内容を直列化する。</summary>
        public static Petition ToPetition(FleetProposal p, int id, BoxKind box = BoxKind.国王)
        {
            if (p == null) return null;
            return new Petition(id, p.Summary, p.faction, box, PetitionOrigin.建白, EncodeEffectKey(p))
            {
                drafterId = p.proposerId,
                carrierId = p.proposerId,
            };
        }

        /// <summary>艦隊系の陳情か（effectKey が fleet.* ＝本ルールが執行する）。</summary>
        public static bool IsFleetPetition(Petition pet)
            => pet != null && !string.IsNullOrEmpty(pet.effectKey)
               && (pet.effectKey.StartsWith(EffectEstablish) || pet.effectKey.StartsWith(EffectDisband));

        /// <summary>承認済みの艦隊陳情を執行する（effectKey を復元して <see cref="Execute"/>）。非艦隊キーは false。</summary>
        public static bool ExecuteFromPetition(Petition pet, float magnitude = 1f)
        {
            if (pet == null) return false;
            var p = Decode(pet.faction, pet.effectKey);
            return p != null && Execute(p, magnitude);
        }

        // ===== AIの自動建議 =====

        /// <summary>
        /// 勢力の現状から「次に挙げるべき建議」を1件選ぶ（無ければ null）。
        /// ①プール残が標準規模以上なら<b>設立</b>（余った総艦艇を新艦隊へ）。
        /// ②そうでなく兵力0の現役艦隊（空の殻）があれば最小番号を<b>解散</b>（台帳を畳む）。
        /// 数値は <see cref="FleetPoolRules"/>/<see cref="FleetRoster"/> を読むだけ（盤面を変えない＝建議の生成のみ）。
        /// </summary>
        public static FleetProposal RecommendProposal(Faction f)
        {
            if (FleetPoolRules.Available(f) >= StandardFleetStrength)
                return new FleetProposal(f, FleetProposalKind.設立, StandardFleetStrength);

            int emptyNumber = -1;
            foreach (var u in FleetRoster.AllFleets(f))
            {
                if (u == null || !u.IsActive || u.baseStrength > 0) continue;
                if (emptyNumber < 0 || u.fleetNumber < emptyNumber) emptyNumber = u.fleetNumber;
            }
            if (emptyNumber > 0)
                return new FleetProposal(f, FleetProposalKind.解散, fleetNumber: emptyNumber);

            return null;
        }
    }
}
