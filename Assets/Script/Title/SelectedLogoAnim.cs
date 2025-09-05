using UnityEngine;
using UnityEngine.UI;

public class SelectedLogoAnim : MonoBehaviour
{
    [SerializeField] private Animator _animator;
    [SerializeField] private Image _panel;
    private bool _isSelected = false;
    public void ClickSelectedLogoImage()
    {
        _isSelected = !_isSelected;

        if (_isSelected)
        {
            _animator.Play("SelectedLogoImageSizeUp");
            _panel.raycastTarget = true;
        }
        else
        {
            _animator.SetTrigger("Return");
            _panel.raycastTarget = false;
        }
    }
}
