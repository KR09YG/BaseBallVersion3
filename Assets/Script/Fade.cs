using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;

public class Fade : MonoBehaviour
{
    [SerializeField] Image _fadeImage;

    /// <summary>
    /// フェードをする
    /// </summary>
    /// <param name="fadeId">0 = フェードアウト、1 = フェードイン</param>
    public void FadeImage(int fadeId,float fadeDuration)
    {
        _fadeImage.DOFade(fadeId, fadeDuration);
    }
}
