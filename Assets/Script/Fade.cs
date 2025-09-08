using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;

public class Fade : MonoBehaviour
{
    [SerializeField] Image _fadeImage;

    /// <summary>
    /// �t�F�[�h������
    /// </summary>
    /// <param name="fadeId">0 = �t�F�[�h�A�E�g�A1 = �t�F�[�h�C��</param>
    public void FadeImage(int fadeId,float fadeDuration)
    {
        _fadeImage.DOFade(fadeId, fadeDuration);
    }
}
