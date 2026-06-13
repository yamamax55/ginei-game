using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 勢力の国家状態＝社会・政治シミュ層の合成（本線の統治モデル）。王朝(<see cref="regime"/>・天命/腐敗)、
    /// 統治体(<see cref="polity"/>・合意)、組織(<see cref="organization"/>・結束)、共同体(<see cref="community"/>・希望)を
    /// 1つに束ね、統治スタイル(<see cref="inclusiveness"/>・収奪0↔包摂1)が抑圧→合意→希望へ連鎖する。
    /// 解決は <see cref="FactionStateRules"/>（static）。per-system の `Province`(内政)とは別＝勢力レベルの合成。
    /// 純データ（非 MonoBehaviour・test-first）。
    /// </summary>
    public class FactionState
    {
        public Faction faction;
        public Regime regime;
        public Polity polity;
        public Organization organization;
        public Community community;

        /// <summary>統治スタイル 0..1（0=収奪的＝抑圧高・即効だが崩れる／1=包摂的＝抑圧低・遅いが安定・GEO-2 #843）。</summary>
        public float inclusiveness = 0.5f;

        /// <summary>政体形態（#117）。首長制スタート→民主(立憲君主制/共和制)or独裁(共産主義/指導者独裁)へ進化。解決は <see cref="GovernmentFormRules"/>。在席状態（セーブ非対象）。</summary>
        public GovernmentForm governmentForm = GovernmentForm.首長制;

        /// <summary>税率レバー 0..1（S5・縦スライス）。高いほど税収↑だが民心(<see cref="community"/>.hope)を蝕む。既定0.3。</summary>
        public float taxRate = 0.3f;

        /// <summary>国庫＝税収の蓄積（S5）。<see cref="CampaignRules.TickEconomy"/> が課税ベース×税率を毎ターン加算。</summary>
        public float treasury = 0f;

        /// <summary>国家予算＝歳出の分野配分（国家予算の基盤）。<see cref="CampaignRules.TickBudget"/> が歳出総額を国庫から引く。
        /// 既定は空＝歳出0（後方互換）。treasury/taxRate と同じく在席のセッション状態（セーブ非対象＝復元時は既定で再構築）。</summary>
        public NationalBudget budget = new NationalBudget();

        /// <summary>形式財政＝債務/利払い/税率/社会保障（#161/#163）。赤字（歳出&gt;歳入）が国債へ繰り越し利払いが翌年に乗る。
        /// 解決は <see cref="FiscalRules"/>。treasury/budget と同じく在席のセッション状態（セーブ非対象）。</summary>
        public FiscalState fiscal = new FiscalState();

        /// <summary>目安箱への信認（箱ごと・MEYASU-2 #1298）。建白が権力者に聞かれるかを左右する“借り物の権威”。解決は <see cref="CredibilityRules"/>。</summary>
        public BoxCredibility credibility = new BoxCredibility();

        /// <summary>政治状態＝政党と衆参の選挙日程（政党システム GOV-6 #159）。民主政治で <see cref="PoliticsTickRules"/> が年次で回す
        /// （二大政党への収束・選挙・分断危機）。budget/fiscal と同じく在席のセッション状態（セーブ非対象・null=未設定）。</summary>
        public PoliticsState politics;

        public FactionState() { }

        public FactionState(Faction faction, float inclusiveness = 0.5f)
        {
            this.faction = faction;
            this.inclusiveness = Mathf.Clamp01(inclusiveness);
            regime = new Regime(0, faction);
            polity = new Polity(0, faction, population: 1000000, rulerForce: 10000);
            organization = new Organization(0, faction);
            community = new Community(0);
            credibility = new BoxCredibility(faction);
        }
    }
}
