using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 戦術要塞（イゼルローン型・#76／回廊要塞 #40）。Battle シーンの固定拠点を「動かない拠点」として実装する。
    /// 構成＝<b>施設本体</b>（コア＝IShipTarget・巨大耐久・移動/士気なし）＋外周の砲台群（<see cref="FortressTurret"/>）。
    ///
    /// ★<b>基本は占領</b>（#40）：施設本体と守備は別勘定で、<b>通常の艦隊攻撃では施設は破壊されない</b>。
    /// コアの耐久が尽きたら「陥落・爆散」ではなく<b>守備制圧</b>（<see cref="FortressControl.守備制圧"/>）＝
    /// 応射が止まり索敵・勝敗の対象から外れるが、<b>施設（3Dモデル・名前）はその場に残る</b>。
    /// 攻撃側が制圧線へ到達すると <see cref="ApplyCapture"/> で<b>所属だけ</b>が移る（<see cref="CorridorFortressArena"/> が判定）。
    /// 破壊は明示的な特殊手段（<see cref="FortressDamageSource.特殊破壊"/>）専用の別経路＝
    /// <see cref="DestroyFacility"/> でしか到達しない（現状ゲーム内に入口は無い）。判定は Core の
    /// <see cref="FortressCaptureRules"/>＝test-first。
    ///
    /// - 砲台は個別破壊可能で、稼働数が減るほどコアのシールドが弱まり被ダメが増える（段階攻略＝<see cref="FortressBatteryRules"/>）。
    /// - コアは円形＝背面が無いため側背面ボーナスは自然に相殺される（攻撃が全周から来て平均化）。
    /// - 見た目は戦略マップと同じ FBX（<c>Models/Fortress/SphericalFortress</c>）＋自己陰影シェーダー
    ///   （<c>Ginei/FortressSelfLit</c>）。未納品ならランタイム生成のスプライトへフォールバックする。
    /// </summary>
    public class FortressUnit : MonoBehaviour, IShipTarget
    {
        [Header("コア（施設本体）")]
        [Tooltip("コアの耐久（艦艇数相当）。0で守備制圧（施設は破壊されない）")]
        public int coreStrength = 9000;

        [Tooltip("コアの被ダメ軽減（0..1）。要塞コアは固い")]
        [Range(0f, 0.9f)]
        public float coreDamageReduction = 0.3f;

        [Tooltip("通常戦闘では施設を破壊しない（基本は占領・#40）。false にすると旧来の爆散に戻る")]
        public bool captureOnly = true;

        [Header("シールド（稼働砲台依存・FortressBatteryRules）")]
        [Tooltip("全砲台稼働時のコア被ダメ倍率（小さいほど固い）")]
        public float shieldedCoreDamageFactor = 0.25f;
        [Tooltip("稼働砲台比がこの値以下でシールド消失（コア被ダメ→等倍）")]
        public float exposedTurretRatio = 0.3f;

        [Header("砲台群")]
        [Tooltip("外周に配置する砲台の数")]
        public int turretCount = 8;
        [Tooltip("砲台を並べる外周リングの半径（ワールド単位）")]
        public float turretRingRadius = 3.0f;
        [Tooltip("各砲台の見た目スケール")]
        public float turretScale = 0.9f;
        [Tooltip("砲台1基の耐久（艦艇数相当）")]
        public int turretStrength = 400;
        [Tooltip("砲台1基の射程")]
        public float turretRange = 14f;
        [Tooltip("砲台1基の1発ダメージ")]
        public int turretDamage = 28;

        [Header("主砲（#77・トールハンマー型）")]
        [Tooltip("チャージ式の超長射程・最小射程つき主砲を1門装備するか")]
        public bool hasMainCannon = true;

        [Header("ビジュアル")]
        [Tooltip("コアの表示半径（ワールド単位）")]
        public float coreVisualRadius = 1.6f;
        [Tooltip("外周リングの表示太さ（防御陣の可視化）")]
        public float perimeterRingRadius = 3.4f;
        [Tooltip("陣営色フォールバック（帝国）")]
        public Color imperialColor = new Color(0.9f, 0.2f, 0.2f);
        [Tooltip("陣営色フォールバック（同盟）")]
        public Color allianceColor = new Color(0.2f, 0.5f, 0.9f);

        [Header("3Dモデル（戦略マップと同じ FBX を戦術マップでも使う）")]
        [Tooltip("Resources 内の要塞メッシュ（空なら従来のスプライト表示）")]
        public string modelResourcePath = "Models/Fortress/SphericalFortress";
        [Tooltip("モデルのワールド直径（既定。CorridorFortressArena が封鎖半径から上書きする）")]
        public float modelWorldDiameter = 8f;
        [Tooltip("モデルの初期姿勢（FBX の軸取りに依らず主砲は AimMainGun が向け直す）")]
        public Vector3 modelEuler = new Vector3(16f, 208f, -6f);
        [Tooltip("主砲を向けたい方向（カメラ側＝-Z へ斜めに）")]
        public Vector3 modelAimDirection = new Vector3(-0.42f, 0.30f, -1f);
        [Tooltip("自己陰影シェーダーのキーライト方向（戦略マップと同じ値＝同じ陰影に見える）")]
        public Vector3 modelKeyLightDir = new Vector3(-0.45f, 0.72f, -0.53f);

        // ── 陣営（生成時に設定）──
        private Faction faction = Faction.帝国;
        private FactionData factionData;
        private string fortressName = "要塞";

        // ── ランタイム状態 ──
        private readonly System.Collections.Generic.List<FortressTurret> turrets = new System.Collections.Generic.List<FortressTurret>();
        private int activeTurrets;
        // 施設本体と守備を分けて持つ：suppressed＝守備が沈黙（施設は健在）／destroyed＝施設そのものが消えた。
        private bool suppressed = false;
        private bool destroyed = false;
        private bool captured = false;
        private SpriteRenderer coreRenderer;
        private SpriteRenderer ringRenderer;
        private TextMesh nameLabel;
        private Transform modelRoot;
        // 実行時生成マテリアルは OnDestroy で破棄する（規約）。
        private readonly System.Collections.Generic.Dictionary<string, Material> modelMaterials =
            new System.Collections.Generic.Dictionary<string, Material>();

        // 共有スプライト（アプリ寿命）。インスタンス固有マテリアルは持たない（SpriteRenderer.color で着色）。
        private static Sprite sharedDisc;
        private static Sprite sharedRing;

        private const int CoreSortingOrder = -5;
        private const int RingSortingOrder = -6;
        private const int TurretSortingOrder = -4;
        private const int ModelSortingOrder = -7; // 艦・砲台より奥
        // 守備制圧のときに残った砲台へ通す「確実に沈黙させる」ダメージ（耐久より十分大きい値）。
        private const int SilenceDamage = 1000000;

        // ── IShipTarget 実装 ──
        public Transform Transform => transform;
        public Faction Faction => faction;
        public FactionData FactionData => factionData;
        /// <summary>
        /// 攻撃対象・勝敗集計に載るか。守備が制圧された時点で false（施設は残るが戦闘要素としては終わり）。
        /// 「施設が存在するか」は <see cref="FacilityExists"/> で別に問い合わせる。
        /// </summary>
        public bool IsAlive => !destroyed && !suppressed && coreStrength > 0;

        /// <summary>稼働中の砲台数（シールド計算・#78 制圧判定・Result 表示用）。</summary>
        public int ActiveTurretCount => activeTurrets;
        /// <summary>総砲台数。</summary>
        public int TurretCount => turrets.Count;
        /// <summary>コアの残存割合（0..1・Result 表示用）。</summary>
        public float CoreRatio => maxCoreStrength > 0 ? Mathf.Clamp01((float)coreStrength / maxCoreStrength) : 0f;
        /// <summary>要塞名。</summary>
        public string FortressName => fortressName;

        /// <summary>守備が制圧されたか（施設は健在＝占領できる状態）。</summary>
        public bool IsGarrisonSuppressed => suppressed;
        /// <summary>施設そのものが失われたか（特殊破壊のみ）。</summary>
        public bool IsFacilityDestroyed => destroyed;
        /// <summary>施設が盤面に残っているか（守備制圧されていても true）。</summary>
        public bool FacilityExists => !destroyed;
        /// <summary>占領されたか（所属が攻撃側へ移った）。</summary>
        public bool IsCaptured => captured;

        /// <summary>現在の制御状態（<see cref="FortressCaptureRules"/> の語彙で答える・表示の出所）。</summary>
        public FortressControl Control
        {
            get
            {
                if (destroyed) return FortressControl.破壊;
                if (captured) return FortressControl.占領;
                return suppressed ? FortressControl.守備制圧 : FortressControl.守備健在;
            }
        }

        private int maxCoreStrength = 1;
        private FortressBatteryParams Params => new FortressBatteryParams(shieldedCoreDamageFactor, exposedTurretRatio);

        /// <summary>
        /// 要塞を初期化する（BattleSetup から呼ぶ）。陣営・規模を反映し、ビジュアルと砲台群を生成、
        /// 索敵レジストリ（IShipTarget）と要塞レジストリへ登録する。
        /// </summary>
        public void Setup(Faction faction, FactionData factionData, string fortressName,
            int turretCountOverride = -1, int coreStrengthOverride = -1)
        {
            this.faction = factionData != null ? factionData.legacyFaction : faction;
            this.factionData = factionData;
            if (!string.IsNullOrEmpty(fortressName)) this.fortressName = fortressName;
            if (turretCountOverride >= 0) turretCount = turretCountOverride;
            if (coreStrengthOverride > 0) coreStrength = coreStrengthOverride;
            maxCoreStrength = Mathf.Max(1, coreStrength);

            transform.rotation = Quaternion.identity; // 円形＝向きは持たない

            Color color = ResolveColor();
            BuildVisuals(color);
            BuildModel();           // 3Dモデル（戦略マップと同じ FBX）。無ければスプライトのまま
            BuildTurrets(color);
            RefreshLabel();

            // 主砲（#77）：同一GameObjectに装備（RequireComponent(FortressUnit) を満たす）。
            if (hasMainCannon && GetComponent<FortressMainCannon>() == null)
                gameObject.AddComponent<FortressMainCannon>();

            FleetRegistry.Register(this);   // 攻撃対象（IShipTarget）として索敵に載せる
            FortressRegistry.Register(this); // 勝敗判定・攻城目標の照会用
        }

        private Color ResolveColor()
        {
            if (factionData != null) return factionData.color;
            return faction == Faction.帝国 ? imperialColor : allianceColor;
        }

        // ── ビジュアル生成（外周リング＋コア＋名前ラベル）──
        private void BuildVisuals(Color color)
        {
            // 防御陣を表す外周リング（薄く陣営色）。所属が読めるのはこのリングと名前ラベル＝モデルより外に置く。
            GameObject ringObj = new GameObject("FortressRing");
            ringObj.transform.SetParent(transform, false);
            ringObj.transform.localScale = Vector3.one * (perimeterRingRadius * 2f);
            ringRenderer = ringObj.AddComponent<SpriteRenderer>();
            ringRenderer.sprite = GetRingSprite();
            ringRenderer.sortingOrder = RingSortingOrder;
            ringRenderer.color = new Color(color.r, color.g, color.b, 0.22f);

            // コア（暗い金属＋陣営色）。3Dモデルを出せた場合は隠す（モデルが本体になる）。
            GameObject coreObj = new GameObject("FortressCore");
            coreObj.transform.SetParent(transform, false);
            coreObj.transform.localScale = Vector3.one * (coreVisualRadius * 2f);
            coreRenderer = coreObj.AddComponent<SpriteRenderer>();
            coreRenderer.sprite = GetDiscSprite();
            coreRenderer.sortingOrder = CoreSortingOrder;
            coreRenderer.color = Color.Lerp(color, new Color(0.15f, 0.15f, 0.18f), 0.4f);

            // 名前ラベル（legacy TextMesh・日本語フォントは FontProvider 経由）
            GameObject labelObj = new GameObject("FortressLabel");
            labelObj.transform.SetParent(transform, false);
            labelObj.transform.localPosition = new Vector3(0f, perimeterRingRadius + 0.8f, 0f);
            nameLabel = labelObj.AddComponent<TextMesh>();
            nameLabel.text = fortressName;
            nameLabel.anchor = TextAnchor.MiddleCenter;
            nameLabel.alignment = TextAlignment.Center;
            nameLabel.fontSize = 48;
            nameLabel.characterSize = 0.08f;
            nameLabel.color = color;
            Font jp = FontProvider.JapaneseFont;
            if (jp != null) { nameLabel.font = jp; nameLabel.GetComponent<MeshRenderer>().material = jp.material; }
        }

        // ── 砲台群の生成（外周に等間隔・外向き）──
        private void BuildTurrets(Color color)
        {
            int n = Mathf.Max(0, turretCount);
            for (int i = 0; i < n; i++)
            {
                float angDeg = FortressBatteryRules.TurretAngleDeg(i, n);
                float angRad = angDeg * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(angRad), Mathf.Sin(angRad)); // 外向き
                Vector3 localPos = new Vector3(dir.x, dir.y, 0f) * turretRingRadius;

                GameObject tObj = new GameObject($"Turret_{i}");
                tObj.transform.SetParent(transform, false);
                tObj.transform.localPosition = localPos;
                // transform.up を外向きに（2D：Atan2-90°）
                float zRot = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
                tObj.transform.localRotation = Quaternion.Euler(0f, 0f, zRot);
                tObj.transform.localScale = Vector3.one * turretScale;

                SpriteRenderer sr = tObj.AddComponent<SpriteRenderer>();
                sr.sprite = GetDiscSprite();
                sr.sortingOrder = TurretSortingOrder;
                sr.color = color;

                FortressTurret turret = tObj.AddComponent<FortressTurret>();
                turret.shipCount = turretStrength;
                turret.range = turretRange;
                turret.damage = turretDamage;
                turret.Setup(this, sr, color);
                turrets.Add(turret);
            }
            activeTurrets = turrets.Count;
        }

        /// <summary>砲台が沈黙したとき（稼働数を減らす＝コアのシールドが弱まる）。</summary>
        public void OnTurretSilenced()
        {
            activeTurrets = Mathf.Max(0, activeTurrets - 1);
        }

        /// <summary>
        /// コアにダメージ（通常戦闘）。コアの被ダメ軽減＋稼働砲台比のシールド倍率を掛けて適用（基準ダメージは非破壊）。
        /// 砲台を削るほどシールドが弱まりコアが脆くなる（段階攻略）。
        /// <b>耐久が尽きても施設は壊れない</b>＝守備制圧になる（#40 基本は占領）。
        /// </summary>
        public void TakeDamage(int damage) => TakeDamage(damage, FortressDamageSource.通常戦闘);

        /// <summary>
        /// 出所つきのダメージ。<see cref="FortressDamageSource.特殊破壊"/> のときだけ、耐久が尽きた際に
        /// 施設そのものを失わせる（<see cref="FortressCaptureRules.AllowsDestruction"/> が唯一の窓口）。
        /// </summary>
        public void TakeDamage(int damage, FortressDamageSource source)
        {
            if (destroyed || suppressed || damage <= 0) return;
            float reduced = damage * (1f - Mathf.Clamp01(coreDamageReduction));
            float shield = FortressBatteryRules.CoreDamageMultiplier(activeTurrets, turrets.Count, Params);
            int applied = Mathf.Max(1, Mathf.RoundToInt(reduced * shield));
            coreStrength -= applied;
            FlashCore();
            if (coreStrength > 0) return;

            coreStrength = 0;
            // 破壊が許されるのは特殊手段だけ。captureOnly=false（旧挙動）でも通常戦闘は制圧止まり…ではなく
            // 従来どおり爆散させる（後方互換のための逃がし）。既定は captureOnly=true＝必ず守備制圧。
            bool allowDestroy = FortressCaptureRules.AllowsDestruction(source) || !captureOnly;
            if (allowDestroy) DestroyFacilityInternal();
            else SuppressGarrison();
        }

        /// <summary>
        /// 守備を制圧された状態にする＝残った砲台を沈黙させ、索敵・勝敗の対象から外す。
        /// <b>施設（モデル・名前）はその場に残す</b>（爆散させない）。所属はまだ移らない＝占領は別（#40）。
        /// </summary>
        public void SuppressGarrison()
        {
            if (suppressed || destroyed) return;
            suppressed = true;
            coreStrength = 0;

            // 残っている砲台を確実に沈黙させる（FortressTurret.Silence は private なので致死ダメージで通す）。
            for (int i = 0; i < turrets.Count; i++)
            {
                FortressTurret t = turrets[i];
                if (t != null && t.IsActive) t.TakeDamage(SilenceDamage);
            }
            activeTurrets = 0;

            // 主砲（FortressMainCannon）は owner.IsAlive を見て自動的に沈黙する（ここでは触らない）。
            FleetRegistry.Unregister(this);   // これ以上撃たれない（施設は残るが戦闘要素ではない）
            FortressRegistry.Unregister(this); // 勝敗集計から外す（#78 の要塞攻略はここで成立）

            DimVisualsForSuppression();
            RefreshLabel();

            NotificationCenter.Push(NotificationCategory.戦闘, NotificationSeverity.注意,
                FortressCaptureRules.DescribeControl(FortressControl.守備制圧, fortressName));
            Debug.Log($"FortressUnit: 要塞「{fortressName}」({faction}) の守備を制圧（施設は健在）。");
        }

        /// <summary>
        /// 占領（制圧完了）＝所属だけが移る。施設・モデル・名前はそのまま引き継がれる（#40 基本は占領）。
        /// 成立条件の判定は <see cref="CorridorFortressArena"/>／<see cref="FortressCaptureRules.CanCapture"/> が持つ。
        /// </summary>
        public void ApplyCapture(Faction newOwner, FactionData newOwnerData)
        {
            if (destroyed) return;
            if (!suppressed) SuppressGarrison(); // 守備が残ったまま占領はしない（保険）
            captured = true;
            faction = newOwnerData != null ? newOwnerData.legacyFaction : newOwner;
            factionData = newOwnerData;

            // 陣営色を新しい所有者へ塗り替える（戦略マップの所有表示と一致させる）。
            Color color = ResolveColor();
            if (ringRenderer != null) ringRenderer.color = new Color(color.r, color.g, color.b, 0.35f);
            if (nameLabel != null) nameLabel.color = color;
            RefreshLabel();
        }

        /// <summary>
        /// 施設そのものを失わせる（<b>特殊手段専用</b>）。通常戦闘（<see cref="FortressDamageSource.通常戦闘"/>）では
        /// 何も起きない＝要塞は通常手段では破壊されない。現状ゲーム内にこの入口を呼ぶ経路は用意していない。
        /// </summary>
        public void DestroyFacility(FortressDamageSource source, string reason = null)
        {
            if (destroyed) return;
            if (!FortressCaptureRules.AllowsDestruction(source))
            {
                Debug.LogWarning($"FortressUnit: 要塞「{fortressName}」は通常手段では破壊できない（占領のみ）。");
                return;
            }
            DestroyFacilityInternal(reason);
        }

        private void FlashCore()
        {
            // 被弾の明滅は Juice に集約（元の色へ自動復帰＝白へ恒久ドリフトしない）。
            // 3Dモデル表示ではコアのスプライトを隠しているので、外周リングを光らせる。
            if (coreRenderer != null && coreRenderer.enabled) Juice.Flash(coreRenderer, Color.white);
            else if (ringRenderer != null) Juice.Flash(ringRenderer, Color.white);
        }

        /// <summary>守備制圧の見た目＝発光を落として「沈黙した施設」に見せる（施設は残す）。</summary>
        private void DimVisualsForSuppression()
        {
            if (ringRenderer != null)
            {
                Color c = ringRenderer.color;
                ringRenderer.color = new Color(c.r, c.g, c.b, 0.12f);
            }
            if (coreRenderer != null)
                coreRenderer.color = Color.Lerp(coreRenderer.color, new Color(0.22f, 0.22f, 0.25f), 0.7f);

            // モデルの発光（主砲の青白・窓の暖色）を落とす＝「灯が消えた要塞」。
            foreach (var kv in modelMaterials)
            {
                Material m = kv.Value;
                if (m == null) continue;
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.black);
            }
        }

        /// <summary>要塞ラベルを現在の所属・状態へ更新する（戦略マップのラベルと同じ読み方にする）。</summary>
        private void RefreshLabel()
        {
            if (nameLabel == null) return;
            string state;
            switch (Control)
            {
                case FortressControl.占領: state = "占領"; break;
                case FortressControl.守備制圧: state = "守備制圧（施設健在）"; break;
                case FortressControl.破壊: state = "破壊"; break;
                default: state = "封鎖中"; break;
            }
            nameLabel.text = $"{fortressName}\n{faction}　{state}";
        }

        /// <summary>
        /// 施設を失わせる実処理（特殊破壊のみが到達する）：爆発演出＋画面シェイク＋通知。
        /// 砲台ごと破棄し、レジストリから外す。
        /// </summary>
        private void DestroyFacilityInternal(string reason = null)
        {
            if (destroyed) return;
            destroyed = true;
            coreStrength = 0;

            FleetRegistry.Unregister(this);
            FortressRegistry.Unregister(this);

            SpawnExplosion(transform.position);
            if (AudioManager.Instance != null) AudioManager.Instance.PlayExplosion();
            CameraController cam = Object.FindAnyObjectByType<CameraController>();
            if (cam != null) cam.Shake();

            string tail = string.IsNullOrEmpty(reason) ? "" : $"：{reason}";
            NotificationCenter.Push(NotificationCategory.戦闘, NotificationSeverity.警告,
                FortressCaptureRules.DescribeControl(FortressControl.破壊, fortressName) + tail);
            Debug.Log($"FortressUnit: 要塞「{fortressName}」({faction}) が破壊された{tail}。");

            Destroy(gameObject); // 子の砲台も破棄（各 OnDestroy で Unregister）
        }

        private void SpawnExplosion(Vector3 pos)
        {
            GameObject go = new GameObject("FortressExplosion");
            go.transform.position = pos;
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop();
            var main = ps.main;
            main.duration = 0.8f;
            main.loop = false;
            main.startLifetime = 0.8f;
            main.startSpeed = 8f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.9f);
            main.startColor = new Color(1f, 0.7f, 0.25f, 1f);
            main.stopAction = ParticleSystemStopAction.Destroy;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 80) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = coreVisualRadius;
            var psr = go.GetComponent<ParticleSystemRenderer>();
            psr.material = new Material(Shader.Find("Sprites/Default"));
            psr.sortingOrder = 20;
            ps.Play();
        }

        private void OnDestroy()
        {
            FleetRegistry.Unregister(this);
            FortressRegistry.Unregister(this);
            // 実行時生成マテリアルは必ず破棄（規約：リーク防止）
            foreach (var kv in modelMaterials) if (kv.Value != null) Destroy(kv.Value);
            modelMaterials.Clear();
        }

        // ===== 3Dモデル（戦略マップと同じ FBX＋自己陰影シェーダー）=====

        /// <summary>
        /// 戦略マップ（<c>GalaxyView.AttachCorridorFortressModels</c>）と同じ作法でメッシュを載せる：
        /// FBX に紛れたライト/カメラを止め、スロット名から <c>Ginei/FortressSelfLit</c> のマテリアルを割り当て、
        /// 主砲がカメラ側へ斜めに向くよう回し、実測して所定のワールド直径へ合わせる。
        /// FBX が無ければ何もしない＝従来のスプライト表示のまま（素材未納品でも壊れない）。
        /// </summary>
        private void BuildModel()
        {
            if (string.IsNullOrEmpty(modelResourcePath)) return;
            GameObject prefab = Resources.Load<GameObject>(modelResourcePath);
            if (prefab == null) return;

            GameObject model = Instantiate(prefab, transform, false);
            model.name = "FortressModel";
            model.transform.localRotation = Quaternion.Euler(modelEuler);
            modelRoot = model.transform;

            // 盤面の見え方を乱すライト/カメラは使わない（2D Renderer では 3D ライトが効かない）。
            var lights = model.GetComponentsInChildren<Light>(true);
            for (int i = 0; i < lights.Length; i++) lights[i].enabled = false;
            var cams = model.GetComponentsInChildren<Camera>(true);
            for (int i = 0; i < cams.Length; i++) cams[i].enabled = false;

            var renderers = model.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].sortingOrder = ModelSortingOrder; // 艦・砲台より奥
                renderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderers[i].receiveShadows = false;
                ApplyModelMaterials(renderers[i]);
            }

            AimMainGun(modelRoot, renderers);
            FitModel(modelWorldDiameter);

            // モデルが本体になるので、スプライトのコアは隠す（所属を示すリングと名前は残す）。
            if (coreRenderer != null) coreRenderer.enabled = false;
        }

        /// <summary>
        /// 表示サイズを指定のワールド直径へ合わせ、砲台・リング・ラベルもその大きさへ追従させる。
        /// <see cref="CorridorFortressArena"/> が封鎖半径（＝艦が入れない円）から呼ぶ＝
        /// 「見えている要塞の大きさ＝通せんぼしている範囲」になる。
        /// </summary>
        public void SetPresentationDiameter(float worldDiameter)
        {
            if (worldDiameter <= 0.01f) return;
            float newRadius = worldDiameter * 0.5f;
            float factor = newRadius / Mathf.Max(0.01f, perimeterRingRadius);
            if (Mathf.Abs(factor - 1f) < 0.01f) return;

            modelWorldDiameter = worldDiameter;
            FitModel(worldDiameter);

            // 砲台は要塞の縁へ（大きさも一緒に拡大＝巨大な要塞に点の砲台が付く違和感を避ける）。
            turretRingRadius *= factor;
            turretScale *= factor;
            for (int i = 0; i < turrets.Count; i++)
            {
                FortressTurret t = turrets[i];
                if (t == null) continue;
                t.transform.localPosition *= factor;
                t.transform.localScale *= factor;
            }

            // 所属を示すリングはモデルの外へ（隠れると陣営が読めなくなる）。
            perimeterRingRadius = newRadius;
            if (ringRenderer != null)
                ringRenderer.transform.localScale = Vector3.one * (newRadius * 2f * 1.06f);
            if (nameLabel != null)
            {
                nameLabel.transform.localPosition = new Vector3(0f, newRadius * 1.12f, 0f);
                nameLabel.characterSize = 0.08f * Mathf.Max(1f, factor * 0.5f);
            }
            coreVisualRadius = newRadius * 0.5f; // 爆発演出の半径も追従
            // FBX 未納品でスプライト表示のままの場合は、コアの円も要塞の大きさへ追従させる
            // （リングだけ巨大で中身が点、という見え方を避ける）。
            if (coreRenderer != null && coreRenderer.enabled)
                coreRenderer.transform.localScale = Vector3.one * (coreVisualRadius * 2f);
        }

        /// <summary>
        /// メッシュの外接直径を実測して所定のワールド直径に合わせる（FBX のインポート倍率に依存しない）。
        /// 併せて、球の前面がカメラの手前へ飛び出さないよう奥へ逃がす（正射影なので奥へ置いても大きさは不変）。
        /// </summary>
        private void FitModel(float targetWorldDiameter)
        {
            if (modelRoot == null) return;
            var renderers = modelRoot.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;

            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            float current = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            if (current <= 1e-4f) return;

            float target = Mathf.Max(0.05f, targetWorldDiameter);
            modelRoot.localScale *= target / current;
            // 2D カメラ（正射影・-Z から +Z を見る）の手前に球の前半分がはみ出すと near clip で欠ける。
            // 直径ぶん奥へ置けば球全体が z>0 に収まる＝欠けない・スプライトより奥に描かれる。
            modelRoot.localPosition = new Vector3(0f, 0f, target);
        }

        /// <summary>
        /// 主砲（発光スロット <c>Reactor_Cyan</c>）の向きを実測し、カメラ側へ斜めに向くようモデルを回す。
        /// 読み取り不可なメッシュや発光スロットが無い場合は <see cref="modelEuler"/> の既定角のまま。
        /// </summary>
        private void AimMainGun(Transform model, Renderer[] renderers)
        {
            if (model == null || renderers == null) return;
            if (!TryFindMainGunLocalDirection(model, renderers, out Vector3 gunLocal)) return;

            Vector3 target = modelAimDirection.sqrMagnitude > 1e-4f
                ? modelAimDirection.normalized
                : new Vector3(-0.42f, 0.30f, -1f).normalized;
            Vector3 gunWorld = model.TransformDirection(gunLocal).normalized;
            model.rotation = Quaternion.FromToRotation(gunWorld, target) * model.rotation;
        }

        /// <summary>主砲方向（モデルのローカル空間）を探す。発光スロットの重心－モデル中心。</summary>
        private static bool TryFindMainGunLocalDirection(Transform model, Renderer[] renderers, out Vector3 dir)
        {
            dir = Vector3.zero;
            for (int r = 0; r < renderers.Length; r++)
            {
                MeshFilter mf = renderers[r].GetComponent<MeshFilter>();
                Mesh mesh = mf != null ? mf.sharedMesh : null;
                if (mesh == null || !mesh.isReadable) continue;

                Material[] mats = renderers[r].sharedMaterials;
                if (mats == null) continue;

                for (int s = 0; s < mats.Length && s < mesh.subMeshCount; s++)
                {
                    string name = mats[s] != null ? mats[s].name : SlotNameByIndex(s);
                    if (name == null || name.IndexOf("Reactor", System.StringComparison.OrdinalIgnoreCase) < 0) continue;

                    int[] tris = mesh.GetTriangles(s);
                    if (tris == null || tris.Length == 0) continue;
                    Vector3[] verts = mesh.vertices;

                    Vector3 sum = Vector3.zero;
                    int n = 0;
                    for (int t = 0; t < tris.Length; t++)
                    {
                        int vi = tris[t];
                        if (vi < 0 || vi >= verts.Length) continue;
                        sum += verts[vi]; n++;
                    }
                    if (n == 0) continue;

                    Vector3 world = renderers[r].transform.TransformPoint(sum / n);
                    Vector3 local = model.InverseTransformPoint(world);
                    if (local.sqrMagnitude < 1e-6f) continue;
                    dir = local.normalized;
                    return true;
                }
            }
            return false;
        }

        /// <summary>FBX のマテリアルスロット名から専用マテリアルを割り当てる（戦略マップと同じ配色）。</summary>
        private void ApplyModelMaterials(Renderer r)
        {
            Material[] slots = r.sharedMaterials;
            if (slots == null || slots.Length == 0) return;
            var next = new Material[slots.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                string slotName = slots[i] != null ? slots[i].name : "";
                if (string.IsNullOrEmpty(slotName)) slotName = SlotNameByIndex(i);
                next[i] = ModelMaterial(slotName) ?? slots[i];
            }
            r.sharedMaterials = next;
        }

        /// <summary>FBX のマテリアルスロット順（納品仕様）。名前が取れないときの予備。</summary>
        private static readonly string[] SlotOrder =
        {
            "Recess_Graphite", "Armor_Silver", "Armor_Light", "Armor_Dark",
            "Gunmetal", "Reactor_Cyan", "Windows_Amber",
        };

        private static string SlotNameByIndex(int i)
            => (i >= 0 && i < SlotOrder.Length) ? SlotOrder[i] : "Armor_Silver";

        /// <summary>
        /// スロット名→マテリアル（インスタンスごとに生成して使い回す。OnDestroy で破棄）。
        /// このプロジェクトの URP は 2D Renderer で 3D ライトが効かないため、自己陰影シェーダー
        /// <c>Ginei/FortressSelfLit</c> を使う（戦略マップと同じ＝同じ見た目になる）。
        /// </summary>
        private Material ModelMaterial(string slotName)
        {
            slotName = FortressMaterialFactory.NormalizeSlot(slotName);
            if (modelMaterials.TryGetValue(slotName, out Material cached) && cached != null) return cached;

            // 質感（スロット名→色/金属感/発光）は FortressMaterialFactory が唯一の定義
            // ＝浮遊砲台（TurretModelRig）と同じ見た目になる。キャッシュと破棄はインスタンス側で持つ。
            Material m = FortressMaterialFactory.Create(slotName, modelKeyLightDir);
            if (m == null) return null;
            modelMaterials[slotName] = m;
            return m;
        }

        // ── 共有スプライト（アプリ寿命で1個ずつ生成）──
        private static Sprite GetDiscSprite()
        {
            if (sharedDisc != null) return sharedDisc;
            const int size = 64;
            const float c = (size - 1) / 2f;
            const float r = size / 2f;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    float a = Mathf.Clamp01((r - d) / 1.5f); // 縁を軽くアンチエイリアス
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            sharedDisc = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return sharedDisc;
        }

        private static Sprite GetRingSprite()
        {
            if (sharedRing != null) return sharedRing;
            const int size = 128;
            const float c = (size - 1) / 2f;
            const float outerR = size / 2f;
            const float innerR = size * 0.42f;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    if (d > outerR || d < innerR) { tex.SetPixel(x, y, new Color(1f, 1f, 1f, 0f)); continue; }
                    float mid = (innerR + outerR) * 0.5f;
                    float half = (outerR - innerR) * 0.5f;
                    float a = Mathf.Clamp01(1f - Mathf.Abs(d - mid) / half);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            tex.Apply();
            sharedRing = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return sharedRing;
        }
    }
}
