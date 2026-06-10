using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 艦隊の兵力（旗艦部隊の艦艇数）を管理するクラス。
    /// 0以下になると艦隊が消滅します。
    /// 攻撃対象としては IShipTarget を実装し、個艦（旗艦）として被弾します。
    /// </summary>
    public class FleetStrength : MonoBehaviour, IShipTarget
    {
        [Header("兵力設定")]
        [Tooltip("提督データ (割り当てると各能力値が反映されます)")]
        public AdmiralData admiralData;

        [Tooltip("提督名")]
        public string admiralName = "ラインハルト";

        [Tooltip("現在の兵力")]
        public int strength = 10000;

        [Tooltip("最大兵力")]
        public int maxStrength = 10000;

        [Header("陣営設定")]
        public Faction faction;

        [Header("役割（#128・戦闘/非戦闘）")]
        [Tooltip("部隊の役割。非戦闘艦（偵察/入植/輸送）は攻撃せず交戦を避け、勝敗カウントから外れる（撃沈はされうる）。既定=戦闘艦で従来動作")]
        public ShipRole shipRole = ShipRole.戦闘艦;

        /// <summary>戦闘艦か（#128。非戦闘艦は発砲・勝敗カウントの対象外）。</summary>
        public bool IsCombatant => shipRole == ShipRole.戦闘艦;

        [Header("旗幟（#817 関ヶ原型・任意）")]
        [Tooltip("自軍への忠誠 0..1。1=従来動作（必ず戦う）。低いと趨勢次第で静観・寝返りが起きる")]
        [Range(0f, 1f)]
        public float loyalty = 1f;

        [Tooltip("敵の調略の浸透 0..1。高いほど劣勢時に寝返りやすい（#819 家康の手紙）")]
        [Range(0f, 1f)]
        public float intrigue = 0f;

        /// <summary>会戦中の旗幟（BattleAllegianceManager が解決して書き込む）。静観中は発砲しない。</summary>
        public Stance battleStance = Stance.戦う;

        /// <summary>実際に戦うか（静観＝フリーライダー #820 は発砲しない）。</summary>
        public bool IsFighting => battleStance != Stance.静観;

        [Header("艦隊編制（#146・任意）")]
        [Tooltip("艦隊番号（0＝未指定＝従来どおり提督名のみ表示）")]
        public int fleetNumber = 0;
        [Tooltip("艦隊の固有名（無ければ「第N艦隊」）")]
        public string fleetUnitName = "";

        [Tooltip("所属軍団名（#147・表示用。空＝なし）")]
        public string corpsName = "";
        [Tooltip("所属軍集団名（#147・表示用。空＝なし）")]
        public string armyGroupName = "";

        /// <summary>艦隊番号が割り当てられているか（#146）。</summary>
        public bool HasFleetNumber => fleetNumber > 0;
        /// <summary>艦隊表示名（固有名→「第N艦隊」。番号未指定なら空）。</summary>
        public string FleetLabel => !HasFleetNumber ? "" : (!string.IsNullOrEmpty(fleetUnitName) ? fleetUnitName : $"第{fleetNumber}艦隊");

        /// <summary>梯団に所属しているか（#147）。</summary>
        public bool HasEchelon => !string.IsNullOrEmpty(corpsName) || !string.IsNullOrEmpty(armyGroupName);
        /// <summary>梯団パス「軍集団 ⊃ 軍団」（あるものだけ・#147）。無ければ空。</summary>
        public string EchelonPath
        {
            get
            {
                bool g = !string.IsNullOrEmpty(armyGroupName), c = !string.IsNullOrEmpty(corpsName);
                if (g && c) return $"{armyGroupName} ⊃ {corpsName}";
                if (c) return corpsName;
                return g ? armyGroupName : "";
            }
        }

        [Tooltip("所属勢力データ（多勢力対応の出所。割り当てると敵対判定・色がこれを優先。未割当なら enum faction で従来動作）")]
        public FactionData factionData;

        [Header("演出設定")]
        [Tooltip("被弾フラッシュの時間 (秒)")]
        public float flashDuration = 0.1f;

        [Header("退却設定")]
        [Tooltip("旗艦喪失（艦艇数0）時に離脱する距離")]
        public float retreatDistance = 50f;

        [Header("得意陣形ボーナス（#104）")]
        [Tooltip("提督の得意陣形と現在陣形が一致する間の被ダメージ軽減率（0=無効, 0.15で被ダメージ15%カット）。実効値パターン＝基準ダメージ・防御計算は非破壊")]
        [Range(0f, 0.9f)]
        public float preferredFormationDamageReduction = 0.15f;

        // 頭上ラベルのズーム追従の基準ズーム（CameraController.startZoom と揃える）
        private const float LabelReferenceOrthoSize = 16f;

        private TextMesh strengthDisplay;
        private FleetMorale moraleComponent;
        private FleetMovement movement;
        private Squadron squadron;

        /// <summary>旗艦を失い退却中か（true なら戦闘・カウントから除外）。</summary>
        public bool IsRetreating { get; private set; }

        /// <summary>この部隊（旗艦＋配下艦）が敵に与えた累計ダメージ。リザルトのMVP集計用。</summary>
        public int DamageDealt { get; private set; }

        // 被弾フラッシュ用
        private SpriteRenderer[] bodyRenderers;
        private Color[] originalColors;
        private bool isFlashing = false;
        private Coroutine flashRoutine;

        // 爆発パーティクルの共有マテリアル（アプリ寿命で1個だけ生成、艦ごとに使い回す）
        private static Material explosionMaterial;

        // IShipTarget 実装（旗艦＝個艦としての攻撃対象）。退却したら標的・カウント対象から外れる。
        public Transform Transform => transform;
        public Faction Faction => faction;
        public FactionData FactionData => factionData;
        public bool IsAlive => !IsRetreating;

        private void Awake()
        {
            moraleComponent = GetComponent<FleetMorale>();
            movement = GetComponent<FleetMovement>();
            squadron = GetComponent<Squadron>();
        }

        private void Start()
        {
            ApplyAdmiralData();
            
            // 艦隊の右下に情報表示テキストを用意
            // プレハブに焼き込まれた既存 "StrengthDisplay" があれば再利用（二重表示を防ぐ）
            Transform existingDisp = transform.Find("StrengthDisplay");
            GameObject textObj;
            if (existingDisp != null)
            {
                textObj = existingDisp.gameObject;
            }
            else
            {
                textObj = new GameObject("StrengthDisplay");
                textObj.transform.SetParent(transform);
                // 位置を右下付近に配置
                textObj.transform.localPosition = new Vector3(0.6f, -0.6f, 0);
                textObj.transform.localScale = Vector3.one * 0.2f; // サイズ調整
            }

            strengthDisplay = textObj.GetComponent<TextMesh>();
            if (strengthDisplay == null) strengthDisplay = textObj.AddComponent<TextMesh>();
            
            // 日本語フォントの読み込み（文字化け対策）。解決は FontProvider に集約。
            Font jaFont = FontProvider.JapaneseFont;
            strengthDisplay.font = jaFont;
            if (jaFont != null)
            {
                textObj.GetComponent<MeshRenderer>().sharedMaterial = jaFont.material;
            }

            // 右下へ向かって表示されるようアンカーを調整
            strengthDisplay.anchor = TextAnchor.UpperLeft;
            strengthDisplay.alignment = TextAlignment.Left;
            strengthDisplay.characterSize = 0.5f;
            strengthDisplay.fontSize = 50;
            // 色の設定は FactionColor コンポーネントに一任するため削除

            UpdateDisplay();

            // 頭上ラベルの文字サイズをズームに追従させる（ズームアウト時の極小化・密集時の重なりを軽減）。
            // 基準ズームは CameraController.startZoom(16) に合わせ、現在のスケールを基準スケールとして保持。
            LabelZoomScaler labelScaler = textObj.GetComponent<LabelZoomScaler>();
            if (labelScaler == null) labelScaler = textObj.AddComponent<LabelZoomScaler>();
            labelScaler.Configure(textObj.transform.localScale, LabelReferenceOrthoSize);

            // 索敵レジストリに登録（faction はここまでに確定済み）
            FleetRegistry.Register(this);
        }

        private void OnDestroy()
        {
            // レジストリ取りこぼし防止（シーン破棄・手置き艦の除去など）
            FleetRegistry.Unregister(this);
        }



        /// <summary>
        /// 提督データから艦隊ステータスを初期化します。
        /// </summary>
        public void ApplyAdmiralData()
        {
            if (admiralData == null) return;

            admiralName = admiralData.EpithetName;
            faction = admiralData.faction;

            // 統率によって兵力上限を決定 (baseStrength を基準に補正)
            // 例：統率100で baseStrength * 1.5, 統率0で baseStrength * 0.5
            // 参謀補完を反映した実効統率を使用（基準値は非破壊）
            float leadershipBonus = (admiralData.EffectiveLeadership - 50) / 100f; // -0.5 ~ +0.5
            maxStrength = Mathf.RoundToInt(admiralData.baseStrength * (1.0f + leadershipBonus));
            strength = maxStrength;

            // 陣営色コンポーネントがあれば色を更新
            FactionColor factionColor = GetComponent<FactionColor>();
            if (factionColor != null)
            {
                factionColor.ApplyColors();
            }
        }

        /// <summary>
        /// ダメージを受けます。
        /// </summary>
        /// <param name="rawDamage">元のダメージ量</param>
        public void TakeDamage(int rawDamage)
        {
            if (IsRetreating) return;

            // 防御力によるダメージ軽減（参謀補完を反映した実効防御）
            float defenseValue = admiralData != null ? admiralData.EffectiveDefense : 0f;
            // 防御100でダメージ50%カット
            float reduction = 1.0f - Mathf.Clamp(defenseValue / 200f, 0, 0.9f);
            int finalDamage = Mathf.RoundToInt(rawDamage * reduction);

            // 得意陣形ボーナス：現在陣形が提督の得意陣形と一致する間だけ被ダメージをさらに軽減（実効値パターン）
            if (admiralData != null && squadron != null
                && admiralData.IsPreferredFormation(squadron.currentFormation)
                && preferredFormationDamageReduction > 0f)
            {
                finalDamage = Mathf.RoundToInt(finalDamage * (1f - Mathf.Clamp(preferredFormationDamageReduction, 0f, 0.9f)));
            }

            strength -= finalDamage;
            
            if (moraleComponent != null)
            {
                moraleComponent.OnTakeDamage(finalDamage);
            }

            UpdateDisplay();

            // 被弾フラッシュ（陣営色は終了時に復元）
            Flash();

            AudioManager.Instance.PlayHit();
            if (strength <= 0)
            {
                BeginRetreat();
            }
        }

        /// <summary>
        /// この部隊（旗艦・配下艦）が敵に与えたダメージを加算します（MVP集計用）。
        /// </summary>
        public void AddDamageDealt(int amount)
        {
            if (amount > 0) DamageDealt += amount;
        }

        /// <summary>
        /// 被弾時に艦体スプライトを一瞬白くフラッシュさせます。
        /// 開始時に現在の色（陣営色）をキャッシュし、終了時にその色へ戻します。
        /// </summary>
        private void Flash()
        {
            if (bodyRenderers == null) CacheBodyRenderers();
            if (bodyRenderers.Length == 0) return;

            // フラッシュ中でなければ現在の色（FactionColor が設定した陣営色）をキャッシュ
            if (!isFlashing)
            {
                originalColors = new Color[bodyRenderers.Length];
                for (int i = 0; i < bodyRenderers.Length; i++)
                {
                    originalColors[i] = bodyRenderers[i] != null ? bodyRenderers[i].color : Color.white;
                }
            }

            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(FlashRoutine());
        }

        private IEnumerator FlashRoutine()
        {
            isFlashing = true;
            for (int i = 0; i < bodyRenderers.Length; i++)
            {
                if (bodyRenderers[i] != null) bodyRenderers[i].color = Color.white;
            }

            yield return new WaitForSeconds(flashDuration);

            // キャッシュした陣営色へ復元（固定デフォルト色には戻さない）
            for (int i = 0; i < bodyRenderers.Length; i++)
            {
                if (bodyRenderers[i] != null) bodyRenderers[i].color = originalColors[i];
            }
            isFlashing = false;
            flashRoutine = null;
        }

        /// <summary>
        /// 艦体スプライト（"SelectionRing" を除く子の SpriteRenderer 全部）を収集します。
        /// </summary>
        private void CacheBodyRenderers()
        {
            SpriteRenderer[] all = GetComponentsInChildren<SpriteRenderer>(true);
            List<SpriteRenderer> list = new List<SpriteRenderer>();
            foreach (var sr in all)
            {
                if (sr.gameObject.name == "SelectionRing") continue;
                if (sr.gameObject.name == "FlagshipMarker") continue;      // 旗艦マーカーは艦体ではないのでフラッシュ対象外
                if (sr.gameObject.name == "FlagshipMarkerGlow") continue;  // 旗艦の発光ハロー(陣営色)もフラッシュしない
                if (sr.GetComponent<EscortShip>() != null) continue;   // 配下艦は別個艦なので旗艦被弾フラッシュの対象外
                list.Add(sr);
            }
            bodyRenderers = list.ToArray();
        }

        private void UpdateDisplay()
        {
            if (strengthDisplay != null)
            {
                string head = HasFleetNumber ? $"{FleetLabel} {admiralName}" : admiralName;
                strengthDisplay.text = IsRetreating
                    ? $"{head}\n退却"
                    : $"{head}\n兵力: {Mathf.Max(0, strength)}";
            }
        }

        /// <summary>
        /// 寝返り（#817）：陣営を敵側へ変更し、色・表示を再適用する。兵力・能力は非破壊（実効値パターンと同思想）。
        /// 敵対判定は FactionRelations が毎クエリ計算するため、以後の索敵・勝敗カウントは自動で新陣営に従う。
        /// </summary>
        public void Defect(FactionData newData, Faction newLegacy)
        {
            factionData = newData;
            faction = newLegacy;
            FactionColor color = GetComponent<FactionColor>();
            if (color != null) color.ApplyColors();
            UpdateDisplay();
            Debug.Log($"{admiralName} の艦隊が {newLegacy} へ寝返った（#817）。");
        }

        /// <summary>
        /// 旗艦喪失（艦艇数0）時の処理。破棄せず「部隊退却」へ移行する。
        /// 以降は IsAlive=false となり、攻撃・被弾・勝敗カウントから除外される。
        /// 静観組の「戦わずして去る」（#817・BattleAllegianceManager）からも呼ばれる。
        /// </summary>
        public void BeginRetreat(bool withEffects = true)
        {
            if (IsRetreating) return;
            IsRetreating = true;
            strength = 0;
            UpdateDisplay();

            // 退却＝戦闘除外。レジストリから外す（配下艦は各自の Update で外れる）
            FleetRegistry.Unregister(this);

            // 旗艦喪失の演出（爆発＋カメラシェイク）。静観退き（#817・戦わず去る）では出さない
            if (withEffects)
            {
                AudioManager.Instance.PlayExplosion();
                SpawnExplosion(transform.position);
                CameraController cam = Object.FindAnyObjectByType<CameraController>();
                if (cam != null) cam.Shake();
            }

            // AI を停止（退却移動を毎フレーム上書きしないように）。
            // 旗艦の FleetWeapon と配下艦 EscortShip は各 Update で IsRetreating を見て発砲を止める。
            FleetAI ai = GetComponent<FleetAI>();
            if (ai != null) ai.enabled = false;

            // 敵と反対方向の遠方へ離脱移動
            if (movement != null)
            {
                Vector2 dir = ComputeRetreatDirection();
                movement.SetDestination((Vector2)transform.position + dir * retreatDistance);
            }

            Debug.Log($"{admiralName} 提督の艦隊 ({faction}) は旗艦を失い退却した。");
        }

        /// <summary>
        /// 退却方向（最寄りの生存敵旗艦と反対方向）を求める。敵がいなければ陣営ごとの自陣側へ。
        /// </summary>
        private Vector2 ComputeRetreatDirection()
        {
            FleetStrength nearest = null;
            float minDist = float.MaxValue;
            IReadOnlyList<FleetStrength> flagships = FleetRegistry.AllFlagships;
            for (int i = 0; i < flagships.Count; i++)
            {
                FleetStrength fs = flagships[i];
                if (fs == null || fs == this || !fs.IsAlive) continue;
                if (!FactionRelations.IsHostile(this, fs)) continue; // 敵対勢力の旗艦のみ
                float d = Vector2.Distance(transform.position, fs.transform.position);
                if (d < minDist) { minDist = d; nearest = fs; }
            }

            if (nearest != null)
            {
                Vector2 dir = (Vector2)(transform.position - nearest.transform.position);
                if (dir.sqrMagnitude > 0.0001f) return dir.normalized;
            }

            // 敵不在時のフォールバック（自陣側）。帝国は左、同盟は右へ離脱。
            return faction == Faction.帝国 ? Vector2.left : Vector2.right;
        }

        /// <summary>
        /// 指定位置に一発限りの爆発パーティクルを生成します。再生後に自動破棄されます。
        /// </summary>
        private void SpawnExplosion(Vector3 pos)
        {
            GameObject go = new GameObject("Explosion");
            go.transform.position = pos;

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop();

            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.startLifetime = 0.5f;
            main.startSpeed = 5f;
            main.startSize = 0.3f;
            main.startColor = new Color(1f, 0.6f, 0.1f); // 橙
            main.stopAction = ParticleSystemStopAction.Destroy; // 終了時に自動破棄

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 30) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.1f;

            // 共有マテリアル（毎回生成せずアプリ寿命で1個を使い回し、リークを防ぐ）
            if (explosionMaterial == null)
            {
                explosionMaterial = new Material(Shader.Find("Sprites/Default"));
            }
            ParticleSystemRenderer psr = go.GetComponent<ParticleSystemRenderer>();
            psr.material = explosionMaterial;
            psr.sortingOrder = 50;

            ps.Play();
        }
    }
}

