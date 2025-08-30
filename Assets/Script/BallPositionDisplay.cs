using UnityEngine;
using UnityEngine.UI;

public class BallPositionDisplay : MonoBehaviour
{
    [SerializeField] Transform _ballTransform;
    [SerializeField] Image _buttonImage;
    [SerializeField] Slider _slider;
    [SerializeField] BallControl _ballControl;
    Vector3 _displayPos;
    private bool _isSupport;

    private void Start()
    {
        transform.position = new Vector3(0,-10,0);
    }
    private void Update()
    {
        if (_isSupport)
        {
            BallPosDisplay();
            _slider.value = _ballControl.BallPitchProgress;
        }
    }

    public void SopportButton()
    {
        _isSupport = !_isSupport;
        if (!_isSupport)
        {
            _buttonImage.color = Color.white;
            //SupportÇ™ÉIÉtÇÃéûÇÕå©Ç¶Ç»Ç¢à íuÇ…éùÇ¡ÇƒÇ¢Ç≠            
            transform.position = new Vector3(0, -10, 0);
        }
        else
        {
            _buttonImage.color = Color.red;           
        }
    }

    private void BallPosDisplay()
    {
        _displayPos = _ballTransform.position;
        _displayPos.z = 0;
        transform.position = _displayPos;
    }
}
