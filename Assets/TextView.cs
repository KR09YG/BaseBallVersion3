using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public enum TextViewType
{
    Infomation,
    Result
}

public class TextView : MonoBehaviour
{
    public static TextView Instance { get; private set; }

    [SerializeField] private TextViewEntry[] _textViewEntrys;
    private Dictionary<TextViewType, TextMeshProUGUI> _textViews;

    [Serializable]
    public struct TextViewEntry
    {
        public TextViewType Type;
        public TextMeshProUGUI Text;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _textViews = new Dictionary<TextViewType, TextMeshProUGUI>(_textViewEntrys.Length);
        SetDictionary();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void SetDictionary()
    {
        foreach (var entry in _textViewEntrys)
        {
            if (entry.Text == null)
            {
                Debug.LogWarning($"[TextView] Text is null for type: {entry.Type}", this);
                continue;
            }

            if (_textViews.ContainsKey(entry.Type))
            {
                Debug.LogWarning($"[TextView] Duplicate key: {entry.Type}", this);
                continue;
            }

            _textViews.Add(entry.Type, entry.Text);
            entry.Text.gameObject.SetActive(false); // èâä˙ÇÕâBÇ∑Ç»ÇÁ
        }
    }

    public void SetText(TextViewType type, string text)
    {
        if (!_textViews.TryGetValue(type, out var view) || view == null)
        {
            Debug.LogWarning($"[TextView] Type not registered: {type}", this);
            return;
        }

        view.gameObject.SetActive(true);
        view.text = text;
    }

    public void Hide(TextViewType type)
    {
        if (!_textViews.TryGetValue(type, out var view) || view == null) return;

        view.text = string.Empty;
        view.gameObject.SetActive(false);
    }
}
