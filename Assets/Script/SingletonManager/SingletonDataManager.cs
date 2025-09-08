using System.ComponentModel;
using UnityEngine;

public class SingletonDataManager : MonoBehaviour
{
    public static SingletonDataManager Instance { get; private set; }

    public Sprite SelectedPlayerLogoSprite { get; private set; }
    public Sprite OpponentLogoSprite { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void SetSelectedLogoIndex(Sprite player, Sprite opponent)
    {
        SelectedPlayerLogoSprite = player;
        OpponentLogoSprite = opponent;
    }
}
