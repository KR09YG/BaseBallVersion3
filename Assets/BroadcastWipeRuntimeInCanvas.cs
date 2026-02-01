using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;

/// <summary>
/// 放送用ワイプ（下→上のカスケード）を既存Canvas配下に生成して使い回す。
/// - Awakeで一回生成
/// - Playで「下から順番に入る → ホールド → 下から順番に出る」
/// - 終了時はRootを非アクティブにして、開始前/終了後に一切見えない
/// - 生成したTexture/SpriteはOnDestroyで破棄（リーク対策）
/// - 親Rectサイズ変更時はスライスを再構築（ウィンドウリサイズ対策）
/// </summary>
public sealed class BroadcastWipeRuntimeInCanvas : MonoBehaviour
{
    [Header("Place under this (Canvas child)")]
    [SerializeField] private RectTransform _parent;

    [Header("Slice Settings")]
    [SerializeField, Range(3, 16)] private int _sliceCount = 8;
    [SerializeField, Range(0f, 0.2f)] private float _stagger = 0.05f;

    [Header("Timing (sec)")]
    [SerializeField] private float _inTime = 0.22f;
    [SerializeField] private float _holdTime = 0.10f;
    [SerializeField] private float _outTime = 0.28f;

    [Header("Look")]
    [SerializeField, Range(0f, 1f)] private float _maxDark = 0.55f;
    [SerializeField, Range(0f, 1f)] private float _flashPeak = 0.30f;
    [SerializeField] private bool _useUnscaledTime = true;

    [Header("Colors")]
    [SerializeField] private Color _teamColor = new Color(0.10f, 0.55f, 1.00f, 1f);
    [SerializeField] private Color _backColor = new Color(0.05f, 0.08f, 0.12f, 1f);

#if UNITY_EDITOR
    [Header("Debug (Editor only)")]
    [SerializeField] private bool _enableSpaceKeyTest = false;
#endif

    // Root/UI refs
    RectTransform _root;
    CanvasGroup _group;
    Image _darken;
    Image _flash;

    readonly List<RectTransform> _backSlices = new();
    readonly List<RectTransform> _mainSlices = new();

    // Runtime resources (leak-safe)
    Texture2D _solidTexture;
    Sprite _solidSprite;

    // Init / runtime
    bool _isInitialized;
    CancellationTokenSource _cts;

    // Resize tracking
    Vector2 _lastRootSize;

    void Awake()
    {
        _cts = new CancellationTokenSource();

        if (_parent == null)
        {
            Debug.LogError($"[{name}] Parent RectTransform is not assigned.", this);
            enabled = false;
            return;
        }

        BuildOnce();
        SetIdleState();
        _isInitialized = true;
    }

    void OnDestroy()
    {
        CancelCurrent();
        _cts?.Dispose();

        // リーク対策：自前生成分を破棄
        if (_solidSprite != null) Destroy(_solidSprite);
        if (_solidTexture != null) Destroy(_solidTexture);
    }

#if UNITY_EDITOR
    void Update()
    {
        if (_enableSpaceKeyTest && Input.GetKeyDown(KeyCode.Space))
        {
            Play();
        }

        // リサイズ検知（Editorでウィンドウサイズ変えた時など）
        if (_isInitialized)
        {
            CheckAndRebuildIfResized();
        }
    }
#else
    void Update()
    {
        if (_isInitialized)
        {
            CheckAndRebuildIfResized();
        }
    }
#endif

    public void Play(Action onCovered = null) => PlayAsync(onCovered).Forget();

    public async UniTask PlayAsync(Action onCovered = null)
    {
        if (!_isInitialized)
        {
            Debug.LogWarning($"[{name}] Not initialized. Skipping play.", this);
            return;
        }

        if (_root == null)
        {
            Debug.LogError($"[{name}] Root is null. Cannot play.", this);
            return;
        }

        CancelCurrent();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        try
        {
            // 再生開始：完全に見える状態へ
            _root.gameObject.SetActive(true);
            _group.alpha = 1f;
            _group.blocksRaycasts = true;

            SetAlpha(_darken, 0f);
            SetAlpha(_flash, 0f);

            ResetSlicesToOffLeft(_backSlices);
            ResetSlicesToOffLeft(_mainSlices);

            // 同時実行：背景帯・メイン帯・暗転を速度差で動かして“放送っぽさ”を出す
            await UniTask.WhenAll(
                CascadeMoveX(_backSlices, _inTime, _stagger, EaseOutCubic, entering: true, ct),
                CascadeMoveX(_mainSlices, _inTime * 0.95f, _stagger, EaseOutCubic, entering: true, ct),
                DarkenTo(_maxDark, _inTime * 0.75f, ct)
            );

            // 覆い完了：軽い白フラ（カット感）
            await FlashPulse(0.08f, _flashPeak, ct);

            // ここで切替（シーンロード/カメラ切替/状態更新など）
            onCovered?.Invoke();

            await Delay(_holdTime, ct);

            // 抜け：下→上（要望どおり同順）
            await UniTask.WhenAll(
                CascadeMoveX(_backSlices, _outTime, _stagger, EaseInCubic, entering: false, ct),
                CascadeMoveX(_mainSlices, _outTime * 0.92f, _stagger, EaseInCubic, entering: false, ct),
                DarkenTo(0f, _outTime * 0.85f, ct)
            );

            SetIdleState();
        }
        catch (OperationCanceledException)
        {
            SetIdleState();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{name}] Unexpected error during wipe: {ex}", this);
            SetIdleState();
        }
    }

    // -------------------------
    // Build / Layout
    // -------------------------
    void BuildOnce()
    {
        if (_root != null) return;

        _solidSprite = CreateSolidSprite(out _solidTexture);

        var rootGO = new GameObject("WipeRoot_Stacked");
        rootGO.transform.SetParent(_parent, false);

        _root = rootGO.AddComponent<RectTransform>();
        StretchFull(_root);

        _group = rootGO.AddComponent<CanvasGroup>();

        _darken = CreateImage("Darken", _root, _solidSprite, Color.black);
        StretchFull(_darken.rectTransform);

        _flash = CreateImage("Flash", _root, _solidSprite, Color.white);
        StretchFull(_flash.rectTransform);
        _flash.transform.SetAsLastSibling();

        RebuildSlices();

        _lastRootSize = _root.rect.size;
    }

    void CheckAndRebuildIfResized()
    {
        if (_root == null) return;

        Vector2 size = _root.rect.size;
        // 小さな誤差で頻繁に作り直さないように閾値
        if (Mathf.Abs(size.x - _lastRootSize.x) < 0.5f && Mathf.Abs(size.y - _lastRootSize.y) < 0.5f)
            return;

        _lastRootSize = size;

        // 再生中に作り直すと見た目が破綻するので、アイドル時のみ
        if (_root.gameObject.activeSelf == false)
        {
            RebuildSlices();
        }
    }

    void RebuildSlices()
    {
        ClearSliceList(_backSlices);
        ClearSliceList(_mainSlices);

        float w = Mathf.Max(10f, _root.rect.width);
        float h = Mathf.Max(10f, _root.rect.height);

        // 継ぎ目対策：少し重ねる（1～2px）
        float sliceH = h / Mathf.Max(1, _sliceCount);
        float overlap = 2f;

        // ピクセル境界のチラつき軽減：位置/高さを整数に寄せる
        sliceH = Mathf.Round(sliceH);

        float backW = w * 1.55f;
        float mainW = w * 1.40f;

        for (int i = 0; i < _sliceCount; i++)
        {
            float y = i * sliceH;

            float thisH = sliceH + overlap;
            float thisY = Mathf.Max(0f, y - overlap * 0.5f);

            // 整数化（Overlayでの1px線対策）
            thisH = Mathf.Round(thisH);
            thisY = Mathf.Round(thisY);

            _backSlices.Add(CreateSlice($"BackSlice_{i}", _root, _solidSprite, _backColor, backW, thisH, thisY));
            _mainSlices.Add(CreateSlice($"MainSlice_{i}", _root, _solidSprite, _teamColor, mainW, thisH, thisY));
        }

        // 生成直後は必ずアイドル状態へ
        ResetSlicesToOffLeft(_backSlices);
        ResetSlicesToOffLeft(_mainSlices);
    }

    void SetIdleState()
    {
        if (_root == null) return;

        // 値を完全に戻す
        _group.alpha = 0f;
        _group.blocksRaycasts = false;

        SetAlpha(_darken, 0f);
        SetAlpha(_flash, 0f);

        ResetSlicesToOffLeft(_backSlices);
        ResetSlicesToOffLeft(_mainSlices);

        // “薄い線が見える”対策として、最後にRoot自体を無効化
        _root.gameObject.SetActive(false);
    }

    // -------------------------
    // Create helpers
    // -------------------------
    static Sprite CreateSolidSprite(out Texture2D tex)
    {
        tex = new Texture2D(1, 1, TextureFormat.RGBA32, false, true);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Point;

        return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f);
    }

    static Image CreateImage(string name, RectTransform parent, Sprite sprite, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.type = Image.Type.Simple;
        img.color = color;
        return img;
    }

    static RectTransform CreateSlice(string name, RectTransform parent, Sprite sprite, Color color, float width, float height, float y)
    {
        var img = CreateImage(name, parent, sprite, color);
        var rt = img.rectTransform;

        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0f, 0f);

        rt.sizeDelta = new Vector2(width, height);
        rt.anchoredPosition = new Vector2(-width, y);
        return rt;
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void ClearSliceList(List<RectTransform> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null) Destroy(list[i].gameObject);
        }
        list.Clear();
    }

    // -------------------------
    // Anim
    // -------------------------
    static void ResetSlicesToOffLeft(List<RectTransform> slices)
    {
        for (int i = 0; i < slices.Count; i++)
        {
            var rt = slices[i];
            if (rt == null) continue;
            SetX(rt, -rt.rect.width);
        }
    }

    async UniTask CascadeMoveX(List<RectTransform> slices, float duration, float stagger, Func<float, float> ease, bool entering, CancellationToken ct)
    {
        if (slices == null || slices.Count == 0) return;

        var tasks = new List<UniTask>(slices.Count);

        // 下→上（iが小さいほど下）
        for (int i = 0; i < slices.Count; i++)
        {
            int idx = i;
            tasks.Add(MoveSliceWithDelay(slices[idx], duration, stagger * idx, ease, entering, ct));
        }

        await UniTask.WhenAll(tasks);
    }

    async UniTask MoveSliceWithDelay(RectTransform rt, float duration, float delay, Func<float, float> ease, bool entering, CancellationToken ct)
    {
        if (rt == null) return;

        if (delay > 0f)
            await Delay(delay, ct);

        float from = entering ? -rt.rect.width : 0f;
        float to = entering ? 0f : OffRight(rt);

        await MoveX(rt, from, to, duration, ease, ct);
    }

    async UniTask MoveX(RectTransform rt, float from, float to, float dur, Func<float, float> ease, CancellationToken ct)
    {
        dur = Mathf.Max(0.0001f, dur);
        float t = 0f;

        SetX(rt, from);

        while (t < 1f)
        {
            ct.ThrowIfCancellationRequested();
            t += Delta() / dur;

            float e = ease(Mathf.Clamp01(t));
            SetX(rt, Mathf.LerpUnclamped(from, to, e));

            await UniTask.Yield(PlayerLoopTiming.Update);
        }

        SetX(rt, to);
    }

    async UniTask DarkenTo(float targetA, float dur, CancellationToken ct)
    {
        if (_darken == null) return;

        float from = _darken.color.a;
        dur = Mathf.Max(0.0001f, dur);
        float t = 0f;

        while (t < 1f)
        {
            ct.ThrowIfCancellationRequested();
            t += Delta() / dur;
            SetAlpha(_darken, Mathf.Lerp(from, targetA, Smooth01(t)));
            await UniTask.Yield(PlayerLoopTiming.Update);
        }

        SetAlpha(_darken, targetA);
    }

    async UniTask FlashPulse(float dur, float peak, CancellationToken ct)
    {
        if (_flash == null) return;

        dur = Mathf.Max(0.0001f, dur);
        float half = dur * 0.5f;

        await FadeAlpha(_flash, 0f, peak, half, ct);
        await FadeAlpha(_flash, peak, 0f, half, ct);
    }

    async UniTask FadeAlpha(Image img, float from, float to, float dur, CancellationToken ct)
    {
        dur = Mathf.Max(0.0001f, dur);
        float t = 0f;

        SetAlpha(img, from);

        while (t < 1f)
        {
            ct.ThrowIfCancellationRequested();
            t += Delta() / dur;
            SetAlpha(img, Mathf.Lerp(from, to, Smooth01(t)));
            await UniTask.Yield(PlayerLoopTiming.Update);
        }

        SetAlpha(img, to);
    }

    async UniTask Delay(float sec, CancellationToken ct)
    {
        if (sec <= 0f) return;

        var type = _useUnscaledTime ? DelayType.UnscaledDeltaTime : DelayType.DeltaTime;
        await UniTask.Delay((int)(sec * 1000f), type, PlayerLoopTiming.Update, ct);
    }

    void CancelCurrent()
    {
        if (_cts == null) return;

        if (!_cts.IsCancellationRequested)
            _cts.Cancel();

        _cts.Dispose();
        _cts = null;
    }

    float Delta() => _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

    float OffRight(RectTransform rt)
    {
        float w = Mathf.Max(10f, _root.rect.width);
        return w + rt.rect.width;
    }

    static void SetX(RectTransform rt, float x)
    {
        var p = rt.anchoredPosition;
        p.x = x;
        rt.anchoredPosition = p;
    }

    static void SetAlpha(Image img, float a)
    {
        var c = img.color;
        c.a = a;
        img.color = c;
    }

    static float Smooth01(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * (3f - 2f * x);
    }

    static float EaseOutCubic(float x)
    {
        float a = 1f - x;
        return 1f - a * a * a;
    }

    static float EaseInCubic(float x) => x * x * x;
}
