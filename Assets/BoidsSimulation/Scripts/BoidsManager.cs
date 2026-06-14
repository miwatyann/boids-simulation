using UnityEngine;

namespace BoidsSimulation
{
    public class BoidsManager : MonoBehaviour
    {
        // ComputeShader側の構造体とメモリレイアウトを一致させる（float3×2 = 24バイト）
        public struct Boid
        {
            public Vector3 position;
            public Vector3 velocity;

            // ComputeBufferのstride計算用（float 6個分）
            public static int Stride => sizeof(float) * 6;
        }

        [Header("ComputeShader")]
        [SerializeField] private ComputeShader computeShader;

        [Header("個体数")]
        [Tooltip("生成する群れの個体数。数千〜1万程度を想定")]
        [SerializeField] private int numBoids = 4096;

        [Header("初期配置")]
        [Tooltip("初期スポーンする球の半径")]
        [SerializeField] private float spawnRadius = 20f;
        [Tooltip("初期速度の大きさ")]
        [SerializeField] private float initialSpeed = 4f;

        [Header("ルールの重み")]
        [SerializeField] private float separationWeight = 2.0f;
        [SerializeField] private float alignmentWeight = 1.0f;
        [SerializeField] private float cohesionWeight = 1.0f;

        [Header("近傍半径")]
        [SerializeField] private float neighborRadius = 4.0f;
        [SerializeField] private float separationRadius = 1.5f;

        [Header("速度・操舵の制限")]
        [SerializeField] private float minSpeed = 2.0f;
        [SerializeField] private float maxSpeed = 8.0f;
        [SerializeField] private float maxSteerForce = 6.0f;

        [Header("行動範囲（原点中心の球）")]
        [SerializeField] private float boundsRadius = 25f;
        [SerializeField] private float boundsWeight = 4.0f;

        [Header("マウスインタラクション（要件3）")]
        [Tooltip("マウス追従のON/OFF")]
        [SerializeField] private bool interactionEnabled = true;
        [Tooltip("捕食者モード：群れが逃げる力（既定・左クリックで強化）")]
        [SerializeField] private float predatorWeight = 6.0f;
        [Tooltip("左クリック時に捕食者の力を何倍にするか")]
        [SerializeField] private float predatorClickMultiplier = 1.8f;
        [Tooltip("エサモード：群れが集まる力（右クリック）")]
        [SerializeField] private float foodWeight = 4.0f;
        [Tooltip("影響が及ぶ半径")]
        [SerializeField] private float targetRadius = 10.0f;
        [Tooltip("捕食者⇔エサ切替時の重みの追従速度")]
        [SerializeField] private float weightLerpSpeed = 8.0f;
        [Tooltip("任意：カーソル位置に追従させる見た目用オブジェクト（球など）")]
        [SerializeField] private Transform cursorVisual;

        // GPUバッファ（ダブルバッファでスワップする）
        private ComputeBuffer _bufferRead;
        private ComputeBuffer _bufferWrite;

        private int _kernel;
        private int _threadGroups;
        private Vector3 _targetPosition;
        private float _currentTargetWeight; // 滑らかに補間する現在の重み（負=捕食者/正=エサ）

        // カーネルに渡すプロパティID（毎フレームの文字列ハッシュを避けてキャッシュ）
        private static readonly int IdBoidsRead = Shader.PropertyToID("boidsRead");
        private static readonly int IdBoidsWrite = Shader.PropertyToID("boidsWrite");
        private static readonly int IdNumBoids = Shader.PropertyToID("numBoids");
        private static readonly int IdDeltaTime = Shader.PropertyToID("deltaTime");
        private static readonly int IdSeparationWeight = Shader.PropertyToID("separationWeight");
        private static readonly int IdAlignmentWeight = Shader.PropertyToID("alignmentWeight");
        private static readonly int IdCohesionWeight = Shader.PropertyToID("cohesionWeight");
        private static readonly int IdNeighborRadius = Shader.PropertyToID("neighborRadius");
        private static readonly int IdSeparationRadius = Shader.PropertyToID("separationRadius");
        private static readonly int IdMinSpeed = Shader.PropertyToID("minSpeed");
        private static readonly int IdMaxSpeed = Shader.PropertyToID("maxSpeed");
        private static readonly int IdMaxSteerForce = Shader.PropertyToID("maxSteerForce");
        private static readonly int IdBoundsRadius = Shader.PropertyToID("boundsRadius");
        private static readonly int IdBoundsWeight = Shader.PropertyToID("boundsWeight");
        private static readonly int IdTargetPosition = Shader.PropertyToID("targetPosition");
        private static readonly int IdTargetWeight = Shader.PropertyToID("targetWeight");
        private static readonly int IdTargetRadius = Shader.PropertyToID("targetRadius");
        private static readonly int IdTargetEnabled = Shader.PropertyToID("targetEnabled");

        public ComputeBuffer BoidsBuffer => _bufferRead;
        public int NumBoids => numBoids;

        private void Start()
        {
            InitializeBuffers();
        }

        private void InitializeBuffers()
        {
            _kernel = computeShader.FindKernel("CSMain");

            // numthreads(256,1,1) に合わせてグループ数を切り上げ
            _threadGroups = Mathf.CeilToInt(numBoids / 256f);

            var boids = new Boid[numBoids];
            for (int i = 0; i < numBoids; i++)
            {
                boids[i] = new Boid
                {
                    position = Random.insideUnitSphere * spawnRadius,
                    velocity = Random.onUnitSphere * initialSpeed,
                };
            }

            _bufferRead = new ComputeBuffer(numBoids, Boid.Stride);
            _bufferWrite = new ComputeBuffer(numBoids, Boid.Stride);
            _bufferRead.SetData(boids);

            // フレーム間で変化しない定数をセット
            computeShader.SetInt(IdNumBoids, numBoids);
        }

        private void Update()
        {
            if (interactionEnabled)
            {
                UpdateTargetPosition();
                UpdateInteractionWeight();

                if (cursorVisual != null)
                {
                    cursorVisual.position = _targetPosition;
                }
            }

            DispatchSimulation();
        }

        // カメラ視線に垂直な平面にカーソルを投影してワールド座標を取得
        private void UpdateTargetPosition()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            var plane = new Plane(-cam.transform.forward, transform.position);
            if (plane.Raycast(ray, out float enter))
            {
                _targetPosition = ray.GetPoint(enter);
            }
        }

        private void UpdateInteractionWeight()
        {
            float target;
            if (Input.GetMouseButton(1))
            {
                // 右クリック：エサ（引き寄せ）
                target = foodWeight;
            }
            else if (Input.GetMouseButton(0))
            {
                // 左クリック：捕食者を強化（より勢いよく散る）
                target = -predatorWeight * predatorClickMultiplier;
            }
            else
            {
                // 既定：弱い捕食者（カーソル周りに穴ができる）
                target = -predatorWeight;
            }

            _currentTargetWeight = Mathf.Lerp(
                _currentTargetWeight, target, Time.deltaTime * weightLerpSpeed);
        }

        private void DispatchSimulation()
        {
            computeShader.SetBuffer(_kernel, IdBoidsRead, _bufferRead);
            computeShader.SetBuffer(_kernel, IdBoidsWrite, _bufferWrite);

            computeShader.SetFloat(IdDeltaTime, Time.deltaTime);

            computeShader.SetFloat(IdSeparationWeight, separationWeight);
            computeShader.SetFloat(IdAlignmentWeight, alignmentWeight);
            computeShader.SetFloat(IdCohesionWeight, cohesionWeight);

            computeShader.SetFloat(IdNeighborRadius, neighborRadius);
            computeShader.SetFloat(IdSeparationRadius, separationRadius);

            computeShader.SetFloat(IdMinSpeed, minSpeed);
            computeShader.SetFloat(IdMaxSpeed, maxSpeed);
            computeShader.SetFloat(IdMaxSteerForce, maxSteerForce);

            computeShader.SetFloat(IdBoundsRadius, boundsRadius);
            computeShader.SetFloat(IdBoundsWeight, boundsWeight);

            computeShader.SetVector(IdTargetPosition, _targetPosition);
            computeShader.SetFloat(IdTargetWeight, _currentTargetWeight);
            computeShader.SetFloat(IdTargetRadius, targetRadius);
            computeShader.SetInt(IdTargetEnabled, interactionEnabled ? 1 : 0);

            computeShader.Dispatch(_kernel, _threadGroups, 1, 1);

            // 次フレームは書き込んだ側を読む
            (_bufferRead, _bufferWrite) = (_bufferWrite, _bufferRead);
        }

        private void OnDestroy()
        {
            // ComputeBufferはGCで解放されないので明示的に破棄する
            _bufferRead?.Release();
            _bufferWrite?.Release();
            _bufferRead = null;
            _bufferWrite = null;
        }
    }
}
