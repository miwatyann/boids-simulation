# Boids 群れシミュレーション（GPU / Compute Shader）

GPU上で数千〜1万の個体を動かす Boids シミュレーション。Unity URP + Compute Shader で実装。

teamLab インタラクティブチーム志望のポートフォリオとして制作。「大量の個体がルールだけで群れを作る」という創発的な挙動を、できるだけ多くの個体でリアルタイム動作させることを目標にした。

## 技術的なポイント

**CPU-GPU間のデータ転送をゼロにした**

毎フレーム全個体の座標をCPUに戻して描画するのが素直な実装だが、個体数が増えると転送コストがボトルネックになる。このプロジェクトではGPU上で計算・描画を完結させ、CPUは Dispatch と引数バッファの更新だけに絞った。

- 計算: `BoidsCompute.compute` が全個体の Boids 3原則（分離・整列・結合）をGPUで並列計算
- 描画: `Graphics.DrawMeshInstancedIndirect` でGPUバッファを直接参照して大量描画

**ダブルバッファで読み書き競合を回避**

全個体が「他の全個体を参照しながら自分を更新する」ため、同じバッファに読み書きすると競合する。`boidsRead`（今フレームの状態）と `boidsWrite`（次フレームの書き込み先）を分けて毎フレームスワップすることで解決した。これを理解するのに一番時間がかかった。

**近傍探索は O(N²) の総当たり**

各個体が全個体との距離を計算するシンプルな実装。GPUの並列性のおかげで 8192個体でも安定して動く。さらに大規模化するなら空間ハッシュによる O(N) 最適化が次のステップ。

## 詰まったところ

- `StructuredBuffer` を頂点シェーダーで読む場合、`Core.hlsl` だけでは `Light` 型が未定義になる。`Lighting.hlsl` の追加インクルードが必要で、これに気づくまでシェーダーエラーで詰まった
- ダブルバッファのスワップタイミング（Dispatch後・描画前）を間違えると個体がすべて原点に集まる

## マウス操作

| 操作 | 効果 |
|------|------|
| カーソル移動 | 弱い捕食者。群れがカーソルを避けて穴ができる |
| 左クリック（ホールド） | 捕食者を強化。群れが勢いよく散る |
| 右クリック（ホールド） | エサ。群れがカーソルへ集まる |

`targetWeight` の符号で引き寄せ/逃避を切り替えている。重みは Lerp で補間しているので切替がなめらか。

## ファイル構成

```
Assets/BoidsSimulation/
├── Shaders/
│   ├── BoidsCompute.compute    ... Boids 3原則の計算カーネル
│   └── BoidRenderURP.shader    ... GPU バッファ直接参照の描画シェーダー
├── Scripts/
│   ├── BoidsManager.cs         ... バッファ管理・Dispatch 制御
│   └── BoidsRenderer.cs        ... DrawMeshInstancedIndirect での描画
└── README.md
```

## セットアップ

1. このフォルダを Unity プロジェクトの `Assets/` 直下に置く
2. `BoidRenderURP` シェーダーからマテリアルを作成（Shader: `Boids/BoidRenderURP`）
3. 空の GameObject に `BoidsManager` と `BoidsRenderer` をアタッチ
4. `BoidsManager` の Compute Shader に `BoidsCompute` をアサイン
5. `BoidsRenderer` の Boid Material に手順 2 のマテリアルをアサイン
6. 再生

> SM4.5以上（DX11 / Metal / Vulkan）が必要。

## 主なパラメータ

| パラメータ | 役割 |
|-----------|------|
| separationWeight / alignmentWeight / cohesionWeight | Boids 3原則の強さ |
| neighborRadius / separationRadius | 近傍・分離の参照半径 |
| minSpeed / maxSpeed / maxSteerForce | 速度・操舵の制限 |
| boundsRadius / boundsWeight | 行動範囲と押し戻す力 |
| predatorWeight / foodWeight / targetRadius | マウスインタラクションの強さ・範囲 |
