using UnityEngine;
using UnityEngine.UI;

public class LogoSelector : MonoBehaviour
{
    [SerializeField] private Image _logoImage;
    [SerializeField] private Sprite[] _teamSprites;
    [SerializeField] private Animator _attentionTextAnim;
    private Sprite _selectedSprite = null;

    public void SetLogo(int id)
    {
        if (id < 0 || id >= _teamSprites.Length)
        {
            Debug.LogError("設定されたIDに間違いがあります");
            return;
        }

        _selectedSprite = _teamSprites[id];
        ChangedSelectedLogoVisual();
    }

    public void ResetLogo()
    {
        _selectedSprite = null;
        _logoImage.sprite = null;
    }

    private void ChangedSelectedLogoVisual()
    {
        _logoImage.sprite = _selectedSprite;
    }

    public void SetTeamLogo()
    {
        if (!_selectedSprite)
        {
            _attentionTextAnim.Play("SelectAttention");
            return;
        }

        Debug.Log("チームロゴ設定完了　ゲームを開始します");
    }
}
