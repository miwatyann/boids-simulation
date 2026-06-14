# Boids 生態系シミュレーション（GPU / Compute Shader）

teamLab インタラクティブチーム志望ポートフォリオ Project 3 のコア部分。
数千〜1万の個体を **Compute Shader** でGPU計算する Boids 群れシミュレーション。

## ファイル構成

```
Assets/BoidsSimulation/
├── Shaders/
│   ├── BoidsCompute.compute    ... GPUでのBoids3原則の計算カーネル
│   └── BoidRenderURP.shader    ... バッファを直接読む大量描画シェーダー
├── Scripts/
│   ├── BoidsManager.cs         ... バッファ管理・Dispatch制御（計算）
│   └── BoidsRenderer.cs        ... DrawMeshInstancedIndirectでの描画
└── README.md
```

## セットアップ手順（Unity / URP）

1. このフォルダごと Unity プロジェクトの `Assets/` 配下に置く。
2. `BoidRenderURP` シェーダーから **マテリアルを作成**（Create > Material → Shader を `Boids/BoidRenderURP` に）。
3. 空の GameObject を作成し `BoidsManager` と `BoidsRenderer` の両方をアタッチ。
4. `BoidsManager` の **Compute Shader** に `BoidsCompute` を割り当てる。
5. `BoidsRenderer` の **Boid Material** に手順2のマテリアルを割り当てる
   （Boid Mesh は空でOK。自動で低ポリConeを生成する）。
6. 再生すると GPU計算＋大量描画が動く。

> 注意：DrawMeshInstancedIndirect と StructuredBuffer読み込みには
> SM4.5以上（DX11/Metal/Vulkan）が必要。プロジェクトのGraphics APIを確認すること。

## 実装メモ

- **ダブルバッファ**：`boidsRead`（今フレーム）/`boidsWrite`（次フレーム）を
  毎フレームスワップし「読みながら書く」競合を回避。
- **近傍探索は総当たり O(N²)**：数千〜1万まではこれで十分。
  さらに大規模化するなら空間ハッシュ（グリッド分割）で近傍を絞るのが次の最適化。
- **マウスインタラクション（要件3）の土台**は実装済み。
  `targetWeight` の符号で「捕食者（逃げる／負）」「エサ（近づく／正）」を切替。

## マウス操作（要件3）

`Interaction Enabled`（既定ON）で有効。カーソルはシミュレーション中心を通る
カメラ垂直平面に投影される。

| 操作 | 効果 |
|------|------|
| カーソル移動（既定） | 弱い捕食者。群れが避けてカーソル周りに穴ができる |
| 左クリック（ホールド） | 捕食者を強化。群れが勢いよく散る |
| 右クリック（ホールド） | エサ。群れがカーソルへ集まる |

- 重みは `weightLerpSpeed` で滑らかに補間され、切替が急にならない。
- `Cursor Visual` に球などを割り当てると、その見た目がカーソル位置に追従する。

## 次のステップ（仕上げ）

- 背景・ポストプロセス（Bloom等）、個体数や半径のチューニング。
- さらに大規模化するなら空間ハッシュで近傍探索を O(N²)→O(N) に最適化。

## 主なパラメータ（Inspectorで調整）

| 項目 | 役割 |
|------|------|
| separationWeight / alignmentWeight / cohesionWeight | 3原則の強さ |
| neighborRadius / separationRadius | 近傍・分離の参照半径 |
| minSpeed / maxSpeed / maxSteerForce | 速度・操舵の制限 |
| boundsRadius / boundsWeight | 行動範囲の球と押し戻す力 |
| targetWeight / targetRadius | マウス影響の強さ・範囲 |
