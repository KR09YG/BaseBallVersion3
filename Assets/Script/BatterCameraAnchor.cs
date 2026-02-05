using UnityEngine;

public class BatterCameraAnchor : MonoBehaviour
{
    [SerializeField] private Transform _batterRoot;
    [SerializeField] private float _height = 1.2f;
    [SerializeField] private bool _lockY = true;     // trueÇ»ÇÁè„â∫í«è]ÇµÇ»Ç¢
    [SerializeField] private bool _lockRotation = true; // trueÇ»ÇÁâÒì]ÇµÇ»Ç¢
    [SerializeField] private float _posSmoothTime = 0.08f;

    private Vector3 _posVel;

    public void SetBatter(Transform batterRoot)
    {
        _batterRoot = batterRoot;
        Snap();
    }

    private void LateUpdate()
    {
        if (_batterRoot == null) return;

        Vector3 target = _batterRoot.position;
        target.y = _lockY ? _height : (target.y + _height);

        transform.position = Vector3.SmoothDamp(transform.position, target, ref _posVel, _posSmoothTime);

        if (_lockRotation)
            transform.rotation = Quaternion.identity;
    }

    private void Snap()
    {
        if (_batterRoot == null) return;
        Vector3 p = _batterRoot.position;
        p.y = _lockY ? _height : (p.y + _height);
        transform.position = p;
        if (_lockRotation) transform.rotation = Quaternion.identity;
    }
}
