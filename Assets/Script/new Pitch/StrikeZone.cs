using UnityEngine;

public class StrikeZone : MonoBehaviour
{
    [Header("ゾーンサイズ")]
    [SerializeField] private float _width = 0.432f;
    [SerializeField] private float _height = 0.6f;
    [SerializeField] private float _depth = 0.1f;

    [Header("表示設定")]
    [SerializeField] private Color _zoneColor = new Color(1f, 1f, 1f, 0.3f);
    [SerializeField] private bool _showGizmo = true;
    [SerializeField] private bool _showInGame = true;  // ゲーム画面で表示
    [SerializeField] private float _lineWidth = 0.01f;  // 線の太さ
    [SerializeField] private Material _lineMaterial;  // 線のマテリアル

    private BoxCollider _collider;
    private LineRenderer _lineRenderer;

    /// <summary>
    /// ゾーンの範囲
    /// </summary>
    public Bounds ZoneBounds => _collider != null ? _collider.bounds : new Bounds(transform.position, Vector3.zero);

    /// <summary>
    /// ゾーンの中心位置
    /// </summary>
    public Vector3 Center => transform.position;

    /// <summary>
    /// ゾーンのサイズ
    /// </summary>
    public Vector3 Size => new Vector3(_width, _height, _depth);

    private void Awake()
    {
        SetupCollider();
        SetupLineRenderer();
    }

    private void SetupCollider()
    {
        _collider = GetComponent<BoxCollider>();
        if (_collider == null)
        {
            _collider = gameObject.AddComponent<BoxCollider>();
        }

        _collider.isTrigger = true;
        _collider.size = new Vector3(_width, _height, _depth);
    }

    private void SetupLineRenderer()
    {
        if (!_showInGame) return;

        // LineRendererコンポーネントを追加
        _lineRenderer = GetComponent<LineRenderer>();
        if (_lineRenderer == null)
        {
            _lineRenderer = gameObject.AddComponent<LineRenderer>();
        }

        // LineRendererの設定
        _lineRenderer.startWidth = _lineWidth;
        _lineRenderer.endWidth = _lineWidth;
        _lineRenderer.loop = true;  // ループして閉じた形にする
        _lineRenderer.positionCount = 4;  // 4つの頂点（矩形）
        _lineRenderer.useWorldSpace = false;  // ローカル座標を使用

        // マテリアル設定
        if (_lineMaterial != null)
        {
            _lineRenderer.material = _lineMaterial;
        }
        else
        {
            // デフォルトマテリアル（Unlit/Color）
            _lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        }

        _lineRenderer.startColor = _zoneColor;
        _lineRenderer.endColor = _zoneColor;

        // 矩形の4つの頂点を設定
        UpdateLineRendererPositions();
    }

    private void UpdateLineRendererPositions()
    {
        if (_lineRenderer == null) return;

        float halfWidth = _width * 0.5f;
        float halfHeight = _height * 0.5f;

        // 矩形の4つの頂点（ローカル座標）
        Vector3[] positions = new Vector3[4]
        {
            new Vector3(-halfWidth, -halfHeight, 0),  // 左下
            new Vector3(halfWidth, -halfHeight, 0),   // 右下
            new Vector3(halfWidth, halfHeight, 0),    // 右上
            new Vector3(-halfWidth, halfHeight, 0)    // 左上
        };

        _lineRenderer.SetPositions(positions);
    }

    /// <summary>
    /// 指定座標がストライクゾーン内か判定
    /// </summary>
    public bool IsInZone(Vector3 position)
    {
        return ZoneBounds.Contains(position);
    }

    /// <summary>
    /// ゾーン内の相対位置を取得 (-0.5 〜 0.5)
    /// </summary>
    public Vector2 GetRelativePosition(Vector3 worldPosition)
    {
        Vector3 localPos = transform.InverseTransformPoint(worldPosition);
        float relativeX = localPos.x / _width;
        float relativeY = localPos.y / _height;
        return new Vector2(relativeX, relativeY);
    }

    /// <summary>
    /// ゲーム画面での表示ON/OFF
    /// </summary>
    public void SetVisible(bool visible)
    {
        if (_lineRenderer != null)
        {
            _lineRenderer.enabled = visible;
        }
    }

    private void OnDrawGizmos()
    {
        if (!_showGizmo) return;

        Gizmos.color = _zoneColor;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(Vector3.zero, new Vector3(_width, _height, _depth));

        Gizmos.color = Color.white;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(_width, _height, _depth));
    }

    private void OnValidate()
    {
        // Inspector値変更時にLineRendererを更新
        if (Application.isPlaying && _lineRenderer != null)
        {
            UpdateLineRendererPositions();
            _lineRenderer.startColor = _zoneColor;
            _lineRenderer.endColor = _zoneColor;
            _lineRenderer.startWidth = _lineWidth;
            _lineRenderer.endWidth = _lineWidth;
        }
    }
}