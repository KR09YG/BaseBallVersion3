using UnityEngine;
using UnityEngine.UI;

public class CursorController : MonoBehaviour
{
    [SerializeField] Image _cursorImage;
    [SerializeField] LayerMask _meetareaLayer;
    public Vector3 _cursorPosition {  get; private set; }

   
    void Update()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hitInfo, 10, _meetareaLayer))
        {
            _cursorImage.rectTransform.position = Input.mousePosition;
            _cursorPosition = hitInfo.point;
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.DrawWireCube(_cursorPosition, new Vector3(1,1,1));
    }
}
