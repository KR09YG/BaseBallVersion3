using UnityEngine;
using UnityEngine.UI;

public class TeamLogoChanger : MonoBehaviour
{
    [SerializeField] private Image _playerTeamLogo;
    [SerializeField] private Image _opponentTeamLogo;

    private void Start()
    {
        _playerTeamLogo.sprite = SingletonDataManager.Instance.SelectedPlayerLogoSprite;
        _opponentTeamLogo.sprite = SingletonDataManager.Instance.OpponentLogoSprite;
    }
}
