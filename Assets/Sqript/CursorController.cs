using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class CursorController : MonoBehaviour
{
    [SerializeField] private Image _cursorImage;
    [SerializeField] private LayerMask _meetareaLayer;
    [SerializeField] private Transform _cursorPosition;
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
            IsCursorInZone = true;
        }
        else
        {
            IsCursorInZone = false;
        }

        if (IsCursorInZone)
        {
            Cursor.visible = false;
        }
        else
        {
            Cursor.visible = true;
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.DrawWireCube(_cursorPosition.position, new Vector3(1,1,1));
    }
}
