using UnityEngine;
using UnityEngine.UI;

public class LogoSelector : MonoBehaviour
{
    [SerializeField] private Image _logoImage;
    [SerializeField] private Sprite[] _teamSprites;
    [SerializeField] private Animator _attentionTextAnim;
    [SerializeField] private SceneChanger _changer;
    [SerializeField] private DirectionManager _directionManager;
    private Sprite _selectedSprite = null;
    private Sprite _opponentSprite = null;
    private int _selectedIndex;

    public void SetLogo(int id)
    {
        if (id < 0 || id >= _teamSprites.Length)
        {
            Debug.LogError("ê›íËÇ≥ÇÍÇΩIDÇ…ä‘à·Ç¢Ç™Ç†ÇËÇ‹Ç∑");
            return;
        }

        _selectedSprite = _teamSprites[id];
        _selectedIndex = id;
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

        RandomSelectedOpponentLogo();
        SingletonDataManager.Instance.SetSelectedLogoIndex(_selectedSprite,_opponentSprite);
        _directionManager.StartGameDirection();
    }

    public void RandomSelectedOpponentLogo()
    {
        int index = Random.Range(0, _teamSprites.Length);
        while (_selectedIndex == index)
        {
            index = Random.Range(0,_teamSprites.Length);
        }
        
        _opponentSprite = _teamSprites[index];
    }
}
