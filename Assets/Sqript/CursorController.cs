using UnityEngine;
using UnityEngine.UI;

public class CursorController : MonoBehaviour
{
    [SerializeField] private Image _cursorImage;
    [SerializeField] private LayerMask _meetareaLayer;
    [SerializeField] private Transform _cursorPosition;

   
    void Update()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hitInfo, 10, _meetareaLayer))
        {
            _cursorImage.rectTransform.position = Input.mousePosition;
            _cursorPosition.position = hitInfo.point;
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.DrawWireCube(_cursorPosition.position, new Vector3(1,1,1));
    }
}
