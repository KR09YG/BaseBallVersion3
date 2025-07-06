using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class CursorController : MonoBehaviour
{
    [SerializeField] private Image _cursorImage;
    [SerializeField] private LayerMask _meetareaLayer;
    [SerializeField] private Transform _cursorPosition;
    public Transform CursorPosition => _cursorPosition;
    private bool _isCursor = false;
    public bool IsCursorInZone { get; private set; }

    private void Start()
    {
        Cursor.visible = _isCursor;
    }
    void Update()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hitInfo, 10, _meetareaLayer))
        {
            _cursorImage.rectTransform.position = Input.mousePosition;
            _cursorPosition.position = hitInfo.point;
            Cursor.visible = false;
            IsCursorInZone = true;
        }
        else
        {
            IsCursorInZone = false;
            Cursor.visible = true;
        }
    }
}
