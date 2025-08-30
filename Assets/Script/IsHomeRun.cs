using System;
using UnityEngine;

public class IsHomeRun : MonoBehaviour
{
    [SerializeField] Animator _homerunTextAnim;
    [SerializeField] Transform _homerunLine;
    public Action OnHomeRun;

    private void Start()
    {
        ServiceLocator.Register(this);
        OnHomeRun += () => _homerunTextAnim.Play("HomeRunTextAnim");
    }

    public bool HomeRunCheck(Vector3 landingPos)
    {
        return _homerunLine.position.magnitude < landingPos.magnitude;
    }
}
