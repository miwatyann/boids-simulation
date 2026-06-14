using UnityEngine;

namespace BoidsSimulation
{
    [RequireComponent(typeof(BoidsManager))]
    public class BoidsRenderer : MonoBehaviour
    {
        [Header("描画リソース")]
        [Tooltip("1個体として描くメッシュ（低ポリ推奨。未指定なら内蔵Coneを生成）")]
        [SerializeField] private Mesh boidMesh;
        [Tooltip("BoidRenderURP マテリアル")]
        [SerializeField] private Material boidMaterial;

        [Header("描画範囲")]
        [Tooltip("カリング用の境界ボックスの大きさ。シミュレーション範囲より大きく")]
        [SerializeField] private float drawBoundsSize = 100f;

        private BoidsManager _manager;

        // DrawMeshInstancedIndirect に渡す引数バッファ
        // [0]=indexCountPerInstance [1]=instanceCount [2]=startIndex [3]=baseVertex [4]=startInstance
        private ComputeBuffer _argsBuffer;
        private readonly uint[] _args = new uint[5] { 0, 0, 0, 0, 0 };

        // マテリアルが参照するバッファのプロパティID
        private static readonly int IdBoidsBuffer = Shader.PropertyToID("boidsBuffer");

        // 内蔵フォールバックメッシュ（ProceduralなConeを動的生成）した場合の破棄管理
        private Mesh _generatedMesh;

        private void Start()
        {
            _manager = GetComponent<BoidsManager>();

            if (boidMesh == null)
            {
                _generatedMesh = CreateConeMesh();
                boidMesh = _generatedMesh;
            }

            InitArgsBuffer();
        }

        private void InitArgsBuffer()
        {
            _argsBuffer = new ComputeBuffer(1, _args.Length * sizeof(uint),
                ComputeBufferType.IndirectArguments);

            _args[0] = boidMesh.GetIndexCount(0);   // 1インスタンスあたりのインデックス数
            _args[1] = (uint)_manager.NumBoids;     // インスタンス数 = 個体数
            _args[2] = boidMesh.GetIndexStart(0);
            _args[3] = boidMesh.GetBaseVertex(0);
            _args[4] = 0;
            _argsBuffer.SetData(_args);
        }

        private void Update()
        {
            // BoidsManagerは毎フレーム読み書きバッファをスワップするため、
            // 現在の読み取りバッファを毎フレームマテリアルに渡し直す。
            ComputeBuffer current = _manager.BoidsBuffer;
            if (current == null) return;

            boidMaterial.SetBuffer(IdBoidsBuffer, current);

            var bounds = new Bounds(Vector3.zero, Vector3.one * drawBoundsSize);

            Graphics.DrawMeshInstancedIndirect(
                boidMesh, 0, boidMaterial, bounds, _argsBuffer);
        }

        // 先端が+Z（forward）を向く低ポリConeを動的生成
        private static Mesh CreateConeMesh()
        {
            const int segments = 6;
            const float radius = 0.4f;
            const float length = 1.2f;

            var vertices = new Vector3[segments + 2];
            var normals = new Vector3[segments + 2];

            // 先端（+Z方向）
            int tip = 0;
            vertices[tip] = new Vector3(0, 0, length * 0.5f);
            normals[tip] = Vector3.forward;

            // 底面中心（-Z方向）
            int baseCenter = 1;
            vertices[baseCenter] = new Vector3(0, 0, -length * 0.5f);
            normals[baseCenter] = Vector3.back;

            // 底面の円周
            for (int i = 0; i < segments; i++)
            {
                float a = (float)i / segments * Mathf.PI * 2f;
                Vector3 p = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, -length * 0.5f);
                vertices[2 + i] = p;
                normals[2 + i] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0.3f).normalized;
            }

            var triangles = new System.Collections.Generic.List<int>();
            for (int i = 0; i < segments; i++)
            {
                int curr = 2 + i;
                int next = 2 + (i + 1) % segments;
                // 側面（先端へ）
                triangles.Add(tip);
                triangles.Add(next);
                triangles.Add(curr);
                // 底面
                triangles.Add(baseCenter);
                triangles.Add(curr);
                triangles.Add(next);
            }

            var mesh = new Mesh { name = "BoidCone" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private void OnDestroy()
        {
            _argsBuffer?.Release();
            _argsBuffer = null;

            if (_generatedMesh != null)
            {
                Destroy(_generatedMesh);
            }
        }
    }
}
