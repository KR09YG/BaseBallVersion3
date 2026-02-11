using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class FielderAnimationData
{
    public FielderState State;
    public AnimationClip Clip;
}

public class FielderAnimationController : MonoBehaviour
{
    [SerializeField] private Animator _animator;
    [SerializeField] private FielderAnimations _animationData;
    private Dictionary<FielderState, AnimationClip> _animationClips = new Dictionary<FielderState, AnimationClip>();
    private FielderState _currentState;

    private void Awake()
    {
        SetAnimDictionary();
    }

    private void SetAnimDictionary()
    {
        if (_animationClips == null || _animationData == null)
        {
            Debug.LogError("Animation Clips not assigned!");
            return;
        }

        foreach (var data in _animationData.FielderAnimationDatas)
        {
            if (!_animationClips.ContainsKey(data.State))
            {
                _animationClips.Add(data.State, data.Clip);
            }
            else
            {
                Debug.LogWarning($"Duplicate animation data for state: {data.State}");
            }
        }
    }

    /// <summary>
    /// ステートに応じたアニメーションを再生する
    /// </summary>
    /// <param name="state"></param>
    public void PlayAnimation(FielderState state)
    {
        _currentState = state;

        if (_animationClips.TryGetValue(state, out var clip))
        {
            if (_currentState != FielderState.MovingTo)
            {
                _animator.applyRootMotion = false;
            }
            else
            {
                _animator.applyRootMotion = true;
            }

            _animator.Play(clip.name, 0, 0f);
        }
        else
        {
            Debug.LogError($"▼▼▼ NO CLIP FOUND for state: {state} ▼▼▼", this);
        }
    }
}
