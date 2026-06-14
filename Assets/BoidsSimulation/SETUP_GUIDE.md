# Boids シミュレーション セットアップガイド

このガイドは、UnityプロジェクトへのBoidsSimulationの組み込みから動作確認まで、**全操作を順番に**記述する。

---

## 前提環境

| 項目 | 要件 |
|------|------|
| Unity | 2022.3 LTS 以上（URP 14+） |
| レンダーパイプライン | Universal Render Pipeline (URP) |
| Graphics API | DX11 / Metal / Vulkan（SM 4.5以上） |
| OS | Windows 10/11, macOS, Linux |

> **注意**: Built-in Render Pipeline では動作しない。必ずURPプロジェクトを使うこと。

---

## STEP 1: Unityプロジェクトの作成

### 1-1. Unity Hubでプロジェクト作成

1. Unity Hub を開く → **New project**
2. テンプレートに **"Universal 3D"** を選択（URPが自動セットアップされる）
3. プロジェクト名・保存先を指定して **Create project**

> 既存プロジェクトにURPを後付けする場合は [STEP 1-2](#1-2-既存プロジェクトにurpを追加する場合) を参照。

### 1-2. 既存プロジェクトにURPを追加する場合

1. **Window → Package Manager** を開く
2. **Unity Registry** タブで `Universal RP` を検索 → **Install**
3. **Assets → Create → Rendering → URP Asset (with Universal Renderer)** でアセットを生成
4. **Edit → Project Settings → Graphics** → Pipeline Asset に上記アセットをセット
5. **Edit → Project Settings → Quality** → 各Qualityレベルにも同じアセットをセット

---

## STEP 2: Graphics API の確認

Compute Shader と DrawMeshInstancedIndirect には SM 4.5 以上が必要。

1. **Edit → Project Settings → Player → Other Settings**
2. **Graphics APIs** の確認:
   - Windows: `Direct3D11` または `Vulkan` が含まれていること（`Direct3D9` は除外）
   - macOS: `Metal` があること
3. `Auto Graphics API` がONならそのままでOK（Unity が自動選択）

---

## STEP 3: ファイルの配置

1. このリポジトリの `Assets/BoidsSimulation/` フォルダを、  
   そのまま **Unityプロジェクトの `Assets/` 直下にコピー**する

   ```
   <UnityProject>/
   └─ Assets/
      └─ BoidsSimulation/      ← ここに丸ごと置く
         ├─ Scripts/
         │   ├─ BoidsManager.cs
         │   └─ BoidsRenderer.cs
         └─ Shaders/
             ├─ BoidsCompute.compute
             └─ BoidRenderURP.shader
   ```

2. Unity Editorがファイルを検出してコンパイルを始める（下部ステータスバーを確認）
3. コンパイルエラーがないことを確認する（Consoleに赤いエラーがないこと）

---

## STEP 4: マテリアルの作成

`BoidRenderURP` シェーダーからマテリアルを作成する。

1. **Project ウィンドウ** で右クリック → **Create → Material**
2. マテリアル名を `BoidMaterial`（任意）とする
3. Inspector の **Shader** ドロップダウンをクリック
4. `Boids/BoidRenderURP` を選択する

   > 見つからない場合: Unity Editorを再起動するか、Assets → Reimport All を実行する

5. マテリアルのプロパティが以下のように表示されることを確認:

   | プロパティ | 既定値 | 説明 |
   |----------|--------|------|
   | Color (低速) | 青系 `(0.1, 0.4, 0.9)` | 低速時の個体色 |
   | Color (高速) | 白系 `(0.9, 0.95, 1.0)` | 高速時の個体色 |
   | 色変化の基準速度 | `8.0` | この速度でColorFastに切り替わる |
   | 個体スケール | `0.5` | メッシュの大きさ |

---

## STEP 5: シーンのセットアップ

### 5-1. GameObjectの作成

1. Hierarchyウィンドウで右クリック → **Create Empty**
2. 名前を `BoidsSystem`（任意）とする
3. Inspectorで **Transform → Position** をすべて `0, 0, 0` に設定する

### 5-2. コンポーネントのアタッチ

1. `BoidsSystem` オブジェクトを選択
2. **Add Component** → `BoidsManager` と入力して選択
3. 同様に **Add Component** → `BoidsRenderer` を追加

   > `BoidsRenderer` は内部で `[RequireComponent(typeof(BoidsManager))]` を持つため、`BoidsManager` なしでは動作しない

### 5-3. Compute Shaderのアサイン

1. `BoidsSystem` を選択 → Inspector の **BoidsManager** コンポーネントを確認
2. **Compute Shader** フィールドに、Projectウィンドウの  
   `Assets/BoidsSimulation/Shaders/BoidsCompute` をドラッグ＆ドロップ

### 5-4. マテリアルのアサイン

1. Inspector の **BoidsRenderer** コンポーネントを確認
2. **Boid Material** フィールドに、STEP 4で作った `BoidMaterial` をドラッグ＆ドロップ
3. **Boid Mesh** は空のままでOK（起動時に自動で低ポリConeを生成する）

---

## STEP 6: カメラのセットアップ

マウスインタラクションは `Camera.main`（タグが `MainCamera` のカメラ）を使う。

1. Hierarchyの **Main Camera** を選択
2. Tag が **MainCamera** になっていることを確認
3. カメラを群れが見える位置に配置する（推奨例）:
   - Position: `(0, 0, -60)`
   - Rotation: `(0, 0, 0)`
   - Field of View: `60`

---

## STEP 7: Inspectorパラメータの設定

`BoidsManager` のパラメータを目的に合わせて調整する。

### BoidsManager の全パラメータ

**個体数**

| フィールド | 既定値 | 説明 |
|----------|--------|------|
| Num Boids | `4096` | 個体数。256の倍数を推奨。8192〜16384は高負荷 |

**初期配置**

| フィールド | 既定値 | 説明 |
|----------|--------|------|
| Spawn Radius | `20` | 初期スポーン球の半径 |
| Initial Speed | `4` | 初速 |

**ルールの重み（Boids 3原則）**

| フィールド | 既定値 | 説明 | 大きくすると |
|----------|--------|------|------------|
| Separation Weight | `2.0` | 分離の強さ | 個体が密集しなくなる |
| Alignment Weight | `1.0` | 整列の強さ | 向きが揃いやすくなる |
| Cohesion Weight | `1.0` | 結合の強さ | 群れが固まりやすくなる |

**近傍半径**

| フィールド | 既定値 | 説明 |
|----------|--------|------|
| Neighbor Radius | `4.0` | 整列・結合の参照距離 |
| Separation Radius | `1.5` | 分離の参照距離（Neighbor Radius より小さく設定） |

**速度・操舵の制限**

| フィールド | 既定値 | 説明 |
|----------|--------|------|
| Min Speed | `2.0` | 速度の下限（止まらない） |
| Max Speed | `8.0` | 速度の上限 |
| Max Steer Force | `6.0` | 1フレームで曲がれる最大量 |

**行動範囲**

| フィールド | 既定値 | 説明 |
|----------|--------|------|
| Bounds Radius | `25` | 行動範囲の球の半径。Spawn Radius より大きく設定 |
| Bounds Weight | `4.0` | 範囲外に出たときに中心へ押し戻す力 |

**マウスインタラクション**

| フィールド | 既定値 | 説明 |
|----------|--------|------|
| Interaction Enabled | ON | マウス追従のON/OFF |
| Predator Weight | `6.0` | 既定状態の捕食者強度（カーソルから逃げる） |
| Predator Click Multiplier | `1.8` | 左クリック時の捕食者強度倍率 |
| Food Weight | `4.0` | 右クリック時にカーソルへ集まる強さ |
| Target Radius | `10.0` | マウスの影響が届く半径 |
| Weight Lerp Speed | `8.0` | 捕食者⇔エサ切替の補間速度 |
| Cursor Visual | 空 | カーソル位置に追従させる見た目用GameObject（任意） |

### BoidsRenderer のパラメータ

| フィールド | 既定値 | 説明 |
|----------|--------|------|
| Boid Mesh | 空 | 個体のメッシュ（空なら自動で低ポリConeを生成） |
| Boid Material | - | STEP 4で作ったマテリアルをセット |
| Draw Bounds Size | `100` | カリング用境界ボックス。シミュレーション範囲より大きく |

---

## STEP 8: 動作確認

### 8-1. Play Mode で起動

1. **Ctrl+P** (Windows) / **Cmd+P** (macOS) でPlay Mode開始
2. 群れが動き始めることを確認する
3. Consoleにエラーが出ていないことを確認する

### 8-2. マウスインタラクションの確認

| 操作 | 期待される挙動 |
|------|-------------|
| カーソルを動かす | 群れがカーソルを避けて穴ができる |
| 左クリック（ホールド） | 群れが勢いよく四散する |
| 右クリック（ホールド） | 群れがカーソルへ集まってくる |

---

## STEP 9: オプション設定

### 9-1. カーソルビジュアルの追加

カーソル位置に球を表示するとインタラクションが視覚的にわかりやすくなる。

1. Hierarchy → **Create → 3D Object → Sphere**
2. Scale を `(1, 1, 1)` 程度に縮小する
3. `BoidsSystem` の **BoidsManager → Cursor Visual** に上記Sphereをセット

### 9-2. ポストプロセスの追加（仕上げ）

1. Hierarchy → **Create → Volume → Global Volume**
2. **New** でVolumeProfileを作成
3. **Add Override → Post-processing → Bloom** を追加
4. Intensityを `0.3〜0.8` 程度にすると群れが光って見える

### 9-3. 背景の設定

1. Main Camera の Inspector → **Background Type** を `Solid Color` に変更
2. **Background** カラーを黒（`#000000`）にすると群れが映える

---

## トラブルシューティング

### エラー: シェーダーが見つからない / ピンクになる

**原因**: URP未設定、またはBuilt-in Render Pipelineプロジェクト  
**対処**: STEP 1-2 を実施してURPに切り替える

### エラー: `Compute Shader` が動かない / Console にエラー

**原因**: Graphics APIがCompute Shaderに対応していない  
**対処**: STEP 2 の手順でDX11/Metal/Vulkanを確認する

### 群れが表示されない（エラーなし）

**原因1**: Compute ShaderのアサインされていないMaterial  
**対処**: BoidsManager の **Compute Shader** と BoidsRenderer の **Boid Material** が設定されているか確認

**原因2**: カメラが群れの範囲外  
**対処**: シミュレーション原点 `(0, 0, 0)` にカメラを向け、`-60` 程度引いて配置する

### マウスインタラクションが効かない

**原因**: Main Camera タグが設定されていない  
**対処**: カメラの Tag が `MainCamera` になっているかHierarchyで確認する

### フレームレートが極端に低い

**原因**: Num Boids が多すぎる（低スペックPCで 8192+ など）  
**対処**: Num Boids を `4096` 以下に下げる  
**備考**: RTX 4050（開発機）では 8192 でも問題ない

---

## パラメータチューニングのヒント

| 狙い | 調整方法 |
|------|---------|
| 魚の群れらしくしたい | Alignment↑ Cohesion↑ Separation↓ |
| 鳥の群れ（ムクドリ型）にしたい | Separation↑ Alignment↑ Cohesion↓ |
| 群れが密集しすぎる | Separation Weight を 2.5〜3.0 に上げる |
| 群れがバラバラすぎる | Cohesion Weight を 1.5〜2.0 に上げる |
| 個体が小さすぎる/大きすぎる | マテリアルの **個体スケール** を調整 |
| スポーン範囲が狭い | Spawn Radius と Bounds Radius を同時に大きくする |

---

## 次のステップ（発展）

- **個体数の大規模化**: 1万超を狙う場合は `BoidsCompute.compute` の総当たり O(N²) を空間ハッシュ（グリッド分割）で O(N) に最適化する
- **カスタムメッシュ**: `BoidsRenderer → Boid Mesh` に魚や鳥の低ポリモデルをセットして見た目を変える
- **外部UIからパラメータ変更**: `BoidsManager` のフィールドは `public` または `[SerializeField]` なので、Slider UIからリアルタイムに操作可能
