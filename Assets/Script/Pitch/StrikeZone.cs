using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public sealed class StrikeZone : MonoBehaviour
{
    [Header("Zone Size (meters)")]
    [SerializeField] private float _width = 0.432f;
    [SerializeField] private float _height = 0.6f;
    [SerializeField] private float _depth = 0.1f;

    [Header("Collider")]
    [SerializeField] private bool _isTrigger = true;

    private BoxCollider _collider;

    public Bounds Bounds => _collider != null ? _collider.bounds : new Bounds(transform.position, Vector3.zero);
    public Vector3 Center => transform.position;
    public float CenterZ => transform.position.z;
    public Vector3 Size => new Vector3(_width, _height, _depth);

    private void Awake()
    {
        SetupCollider();
    }

    private void SetupCollider()
    {
        _collider = GetComponent<BoxCollider>();
        _collider.isTrigger = _isTrigger;
        ApplySizeToCollider();
    }

    private void ApplySizeToCollider()
    {
        if (_collider == null) return;
        _collider.size = Size;
        _collider.center = Vector3.zero;
    }

    public bool IsInZone(Vector3 worldPosition)
    {
        return Bounds.Contains(worldPosition);
    }

    public Vector2 GetRelativePosition(Vector3 worldPosition)
    {
        Vector3 localPos = transform.InverseTransformPoint(worldPosition);
        float relativeX = localPos.x / _width;
        float relativeY = localPos.y / _height;
        return new Vector2(relativeX, relativeY);
    }
}
