using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// バッティングの当たり判定と打撃計算を行う
/// </summary>
public class BattingCalculator : MonoBehaviour
{
    // 芯ズレ時の最小効率（外したらほぼ飛ばない）
    private const float MIN_IMPACT_EFFICIENCY = 0.03f;

    // 芯ズレ時の最大効率
    private const float MAX_IMPACT_EFFICIENCY = 1.0f;

    // 打球初速全体倍率（ゲーム性調整用）
    private const float GLOBAL_EXIT_VELOCITY_SCALE = 0.85f;

    // sweetSpot〜maxImpact を正規化する際の下限・上限
    private const float MISS_MIN = 0.0f;
    private const float MISS_MAX = 1.0f;

    // --- Unit conversions ---
    private const float KMH_TO_MS = 1f / 3.6f;

    // --- Timing calculation ---
    private const float TIMING_DISTANCE_WINDOW_M = 2f;     // DISTANCE_WINDOW
    private const float TIMING_PULL_OPPO_THRESHOLD = 0.1f; // pull/oppo判定の閾値

    // --- Launch angle calc ---
    private const float VERTICAL_OFFSET_NORMALIZE_SCALE = 10f; // verticalOffset * 10f

    // --- Pitch velocity sampling ---
    private const float PITCH_TRAJECTORY_DT = 0.01f; // (p2 - p1) / 0.01f の dt

    // --- Exit velocity calculation ---
    private const float BALL_MASS_KG = 0.145f;                 // ローカル const BALL_MASS
    private const float EFFECTIVE_BAT_MASS_FACTOR = 0.7f;       // batMass * 0.7f

    // ======== 追加：ExitVelocity 調整（飛びすぎ防止） ========
    private const float COR_MIN = 0.15f;
    private const float COR_MAX = 0.60f;

    // 現実寄りの上限目安：60〜80m/s（ゲームなので好みで）
    private const float EXIT_VELOCITY_MIN_MS = 0f;
    private const float EXIT_VELOCITY_MAX_MS = 80f;

    // このプロジェクトの打球前方向は -Z（CalculateBattedBallDirection が z=-cos...）
    private static readonly Vector3 BAT_FORWARD_WORLD = Vector3.back;

    // --- Spin rate calculation ---
    private const float SPIN_VELOCITY_BASE_MS = 40f;           // exitVelocity / 40f
    private const float SPIN_ANGLE_BASE_DEG = 30f;             // launchAngle / 30f
    private const float SPIN_ANGLE_SCALE = 0.3f;               // * 0.3f
    private const float SPIN_MIN_RPM = 500f;
    private const float SPIN_MAX_RPM = 4000f;

    // ======== 変更：Lift coefficient（スピン比ベース + clamp） ========
    private const float BALL_RADIUS_M = 0.0366f;
    private const float RPM_TO_RAD_PER_SEC = 2f * Mathf.PI / 60f;
    private const float MIN_SPEED_FOR_SPIN_RATIO = 5f; // 低速で暴れないように

    // Cl = a*S/(b+S) のパラメータ（ゲーム用ツマミ）
    private const float CL_A = 1.2f;
    private const float CL_B = 0.2f;
    private const float CL_MIN = 0.0f;
    private const float CL_MAX = 0.35f; // ここを下げると飛ばなくなる

    // --- Hit type / direction labeling ---
    private const float LABEL_TIMING_TOO_EARLY = -0.7f;
    private const float LABEL_TIMING_EARLY = -0.3f;
    private const float LABEL_TIMING_JUST = 0.3f;
    private const float LABEL_TIMING_LATE = 0.7f;

    private const float DIRECTION_EXTREME_FOUL_DEG = 60f;
    private const float DIRECTION_FOUL_DEG = 45f;
    private const float DIRECTION_CORNER_OUTFIELD_DEG = 30f;
    private const float DIRECTION_GAP_DEG = 15f;
    private const float DIRECTION_CENTER_TIGHT_DEG = 5f;

    private const float BALLTYPE_GROUND_STRONG_DEG = 0f;
    private const float BALLTYPE_GROUND_DEG = 10f;
    private const float BALLTYPE_LOW_LINER_DEG = 15f;
    private const float BALLTYPE_LINER_DEG = 25f;
    private const float BALLTYPE_LINER_FLY_DEG = 35f;
    private const float BALLTYPE_FLY_DEG = 45f;

    // --- DetermineBattedBallType thresholds ---
    private const float HOMERUN_DISTANCE_M = 120f;
    private const float HIT_DISTANCE_M = 30f;

    // --- Miss result defaults ---
    private static readonly Vector3 MISS_LANDING_POS = Vector3.zero;

    // --- Simulation config defaults for batted ball ---
    private const float BATTED_SIM_DELTA_TIME = 0.01f;
    private const float BATTED_SIM_MAX_TIME = 10f;

    [Header("参照")]
    [SerializeField] private BattingCursor _cursor;
    [SerializeField] private StrikeZone _strikeZone;
    [SerializeField] private BattingData _parameters;
    [SerializeField] private BounceSettings _bounceSettings;

    [Header("イベント")]
    [SerializeField] private BattingHitEvent _hitEvent;
    [SerializeField] private BattingBallTrajectoryEvent _trajectoryEvent;
    [SerializeField] private BattingResultEvent _battedBallResultEvent;

    [Header("デバッグ")]
    [SerializeField] private bool _enableDebugLogs = true;

    private void Start()
    {
        if (_hitEvent != null)
        {
            _hitEvent.RegisterListener(OnHitAttempt);
        }
        else
        {
            Debug.LogError("[BattingCalculator] BattingHitEventが設定されていません");
        }

        // デバッグ：イベント設定確認
        Debug.Log($"[BattingCalculator] Start - TrajectoryEvent={(_trajectoryEvent != null ? "OK" : "NULL")}, ResultEvent={(_battedBallResultEvent != null ? "OK" : "NULL")}");
    }

    private void OnDestroy()
    {
        if (_hitEvent != null)
        {
            _hitEvent.UnregisterListener(OnHitAttempt);
        }
    }

    /// <summary>
    /// ヒット試行時の処理（BattingHitEventから呼ばれる）
    /// </summary>
    private void OnHitAttempt(PitchBallMove ball)
    {
        if (ball == null)
        {
            Debug.LogError("[BattingCalculator] Ballがnullです");
            return;
        }

        if (_enableDebugLogs)
        {
            Debug.Log("========== 打撃計算開始 ==========");
        }

        // スイング位置取得
        Vector3 swingPosition = _cursor.CurrentPos;

        // ボールのストライクゾーン通過位置を取得
        Vector3 ballPosition = GetBallPositionAtStrikeZone(ball);

        // 当たり判定
        bool isHit = CheckHit(swingPosition, ballPosition);

        if (isHit)
        {
            // 投球速度を取得
            Vector3 pitchVelocity = GetPitchVelocity(ball, ballPosition);

            // 打球軌道を計算
            CalculateBattedBallTrajectory(
                pitchVelocity,
                swingPosition,
                ballPosition,
                ball
            );
        }
        else
        {
            // ミス処理
            OnMiss(swingPosition, ballPosition);
        }

        if (_enableDebugLogs)
        {
            Debug.Log("========== 打撃計算完了 ==========");
        }
    }

    /// <summary>
    /// ボールのストライクゾーン通過位置を取得
    /// </summary>
    private Vector3 GetBallPositionAtStrikeZone(PitchBallMove ball)
    {
        List<Vector3> trajectory = ball.Trajectory;

        if (trajectory == null || trajectory.Count == 0)
        {
            Debug.LogError("[BattingCalculator] ボールの軌道データがありません");
            return Vector3.zero;
        }

        float strikeZoneZ = _strikeZone.Center.z;
        Vector3 ballPosition = BallPhysicsCalculator.FindPointAtZ(trajectory, strikeZoneZ);

        if (_enableDebugLogs)
        {
            Debug.Log($"[BattingCalculator] ボール通過位置: {ballPosition}");
        }

        return ballPosition;
    }

    /// <summary>
    /// 当たり判定
    /// </summary>
    private bool CheckHit(Vector3 swingPosition, Vector3 ballPosition)
    {
        float distance = Vector3.Distance(swingPosition, ballPosition);
        bool isHit = distance <= _parameters.maxImpactDistance;

        if (_enableDebugLogs)
        {
            Debug.Log($"[BattingCalculator] インパクト距離: {distance * 100f:F2}cm");
            Debug.Log($"[BattingCalculator] 判定: {(isHit ? "ヒット" : "ミス")}");
        }

        return isHit;
    }

    /// <summary>
    /// 投球速度を「バット前方向（打球前方向）」へ射影し、
    /// "バットへ向かってくる成分" を正の速度として返す（m/s, >=0）
    /// </summary>
    private static float GetIncomingPitchSpeedIntoBat(Vector3 pitchVelocity)
    {
        // pitchが BAT_FORWARD と逆向きに飛んでくるとき dot は負になる想定
        // → それを正の「食い込む速度」に変換
        return Mathf.Max(0f, -Vector3.Dot(pitchVelocity, BAT_FORWARD_WORLD));
    }

    /// <summary>
    /// 打球パラメータを計算（内部用）
    /// </summary>
    private BattingBallResult CalculateBattedBallParameters(
        Vector3 pitchVelocity,
        Vector3 swingPosition,
        Vector3 ballPosition,
        PitchBallMove ball)
    {
        // インパクト距離（芯からのズレ）
        float impactDistance = Vector3.Distance(swingPosition, ballPosition);

        // インパクト効率を計算
        float impactEfficiency = CalculateImpactEfficiency(impactDistance);
        bool isSweetSpot = impactDistance <= _parameters.sweetSpotRadius;

        // 打球速度を計算（投球速度は「食い込む成分」を使う：符号・向き込み）
        float incomingPitchSpeed = GetIncomingPitchSpeedIntoBat(pitchVelocity);

        float exitVelocity = CalculateExitVelocity(
            incomingPitchSpeed,
            _parameters.batSpeedKmh * KMH_TO_MS,
            impactEfficiency
        );

        // タイミングから水平角度を計算
        float timing = CalculateSwingTimingSimple(ball);
        float horizontalAngle = CalculateHorizontalAngleFromTiming(timing);

        // カーソルとボールの位置から打ち上げ角度を計算（非線形）
        float launchAngle = CalculateLaunchAngleFromPositionNonLinear(swingPosition, ballPosition);

        // ファール判定
        bool isFoul = IsFoulBall(horizontalAngle);

        // 打球方向ベクトル
        Vector3 direction = CalculateBattedBallDirection(launchAngle, horizontalAngle);

        // 打球の初速ベクトル
        Vector3 initialVelocity = direction * exitVelocity;

        // スピン（バックスピン）
        Vector3 spinAxis = CalculateSpinAxis(direction);
        float spinRate = CalculateSpinRate(exitVelocity, launchAngle, impactEfficiency);

        if (_enableDebugLogs)
        {
            Debug.Log($"[位置関係] カーソルY: {swingPosition.y:F3}, ボールY: {ballPosition.y:F3}, 差: {(swingPosition.y - ballPosition.y) * 100f:F1}cm");
            Debug.Log($"[打球詳細] timing: {timing:F2}, 水平: {horizontalAngle:F1}度, 仰角: {launchAngle:F1}度, ファール: {isFoul}");
            Debug.Log($"[投球速度] raw={pitchVelocity}, incoming={incomingPitchSpeed:F2} m/s");
        }

        return new BattingBallResult
        {
            InitialVelocity = initialVelocity,
            ExitVelocity = exitVelocity,
            LaunchAngle = launchAngle,
            HorizontalAngle = horizontalAngle,
            SpinAxis = spinAxis,
            SpinRate = spinRate,
            ImpactDistance = impactDistance,
            ImpactEfficiency = impactEfficiency,
            IsSweetSpot = isSweetSpot,
            Timing = timing,
            IsFoul = isFoul
        };
    }

    /// <summary>
    /// タイミングから水平角度を計算（完全決定論的・右打者用）
    /// </summary>
    private float CalculateHorizontalAngleFromTiming(float timing)
    {
        if (Mathf.Abs(timing) > _parameters.foulThreshold)
        {
            float excessTiming = (Mathf.Abs(timing) - _parameters.foulThreshold) / (1f - _parameters.foulThreshold);
            float foulAngle = Mathf.Lerp(_parameters.maxFairAngle, _parameters.maxFoulAngle, excessTiming);
            return Mathf.Sign(timing) * foulAngle;
        }

        float normalizedTiming = timing / _parameters.foulThreshold;
        float horizontalAngle = normalizedTiming * _parameters.maxFairAngle;

        return horizontalAngle;
    }

    /// <summary>
    /// 非線形変換で打ち上げ角度を計算
    /// </summary>
    private float CalculateLaunchAngleFromPositionNonLinear(Vector3 cursorPosition, Vector3 ballPosition)
    {
        float verticalOffset = cursorPosition.y - ballPosition.y;

        float normalizedOffset = verticalOffset * VERTICAL_OFFSET_NORMALIZE_SCALE;
        float angleOffset = Mathf.Sign(normalizedOffset) *
                           Mathf.Pow(Mathf.Abs(normalizedOffset), _parameters.launchAnglePower) *
                           _parameters.launchAngleScale;

        float launchAngle = _parameters.idealLaunchAngle - angleOffset;

        launchAngle = Mathf.Clamp(launchAngle, _parameters.minLaunchAngle, _parameters.maxLaunchAngle);

        if (_enableDebugLogs)
        {
            Debug.Log($"[打ち上げ角度非線形] 縦差: {verticalOffset * 100f:F1}cm");
            Debug.Log($"[打ち上げ角度非線形] 正規化: {normalizedOffset:F2}, 角度オフセット: {angleOffset:F1}度");
            Debug.Log($"[打ち上げ角度非線形] 最終角度: {launchAngle:F1}度");
        }

        return launchAngle;
    }

    /// <summary>
    /// ボールの位置からタイミングを計算（簡易版）
    /// </summary>
    private float CalculateSwingTimingSimple(PitchBallMove ball)
    {
        Vector3 ballPosition = ball.transform.position;
        float strikeZoneZ = _strikeZone.Center.z;

        float distanceToZone = ballPosition.z - strikeZoneZ;

        float normalizedTiming = Mathf.Clamp(distanceToZone / TIMING_DISTANCE_WINDOW_M, -1f, 1f);

        if (_enableDebugLogs)
        {
            Debug.Log($"[タイミング簡易] ボール位置Z: {ballPosition.z:F2}, ゾーンZ: {strikeZoneZ:F2}");
            Debug.Log($"[タイミング簡易] 距離: {distanceToZone:F2}m, タイミング: {normalizedTiming:F2}");
        }

        return normalizedTiming;
    }

    /// <summary>
    /// ファール判定
    /// </summary>
    private bool IsFoulBall(float horizontalAngle)
    {
        return Mathf.Abs(horizontalAngle) > _parameters.maxFairAngle;
    }

    /// <summary>
    /// 投球速度を取得
    /// </summary>
    private Vector3 GetPitchVelocity(PitchBallMove ball, Vector3 ballPosition)
    {
        List<Vector3> trajectory = ball.Trajectory;
        float strikeZoneZ = _strikeZone.Center.z;

        for (int i = 0; i < trajectory.Count - 1; i++)
        {
            Vector3 p1 = trajectory[i];
            Vector3 p2 = trajectory[i + 1];

            if (p1.z <= strikeZoneZ && p2.z >= strikeZoneZ)
            {
                Vector3 velocity = (p2 - p1) / PITCH_TRAJECTORY_DT;
                return velocity;
            }
        }

        if (trajectory.Count >= 2)
        {
            // ✅ 修正：区間数は (Count - 1)
            float totalTime = (trajectory.Count - 1) * PITCH_TRAJECTORY_DT;
            Vector3 avgVelocity = (trajectory[trajectory.Count - 1] - trajectory[0]) / totalTime;
            return avgVelocity;
        }

        Debug.LogWarning("[BattingCalculator] 投球速度の取得に失敗");
        return Vector3.forward * 30f; // デフォルト（ここは必要なら別定数化してもOK）
    }

    /// <summary>
    /// インパクト効率を計算（芯からの距離に応じて減衰）
    /// </summary>
    private float CalculateImpactEfficiency(float impactDistance)
    {
        // 完全に芯
        if (impactDistance <= _parameters.sweetSpotRadius)
            return MAX_IMPACT_EFFICIENCY;

        // 当たり判定ギリギリ
        if (impactDistance >= _parameters.maxImpactDistance)
            return MIN_IMPACT_EFFICIENCY;

        // 芯ズレ量を 0〜1 に正規化
        float miss = Mathf.InverseLerp(
            _parameters.sweetSpotRadius,
            _parameters.maxImpactDistance,
            impactDistance
        );
        miss = Mathf.Clamp(miss, MISS_MIN, MISS_MAX);

        // launchAnglePower を「芯ズレ減衰の厳しさ」として使用
        float efficiency =
            MAX_IMPACT_EFFICIENCY
            - Mathf.Pow(miss, _parameters.launchAnglePower);

        // 全体を飛びづらくするスケール
        efficiency *= GLOBAL_EXIT_VELOCITY_SCALE;

        // 最終クランプ
        efficiency = Mathf.Clamp(
            efficiency,
            MIN_IMPACT_EFFICIENCY,
            MAX_IMPACT_EFFICIENCY
        );

        return efficiency;
    }

    /// <summary>
    /// 打球速度を計算（反発係数と運動量保存則）
    /// pitchSpeed は「バットへ向かってくる成分」のスカラー（m/s, >=0）
    /// </summary>
    private float CalculateExitVelocity(float pitchSpeed, float batSpeed, float efficiency)
    {
        float effectiveBatMass = _parameters.batMass * EFFECTIVE_BAT_MASS_FACTOR;

        // ✅ 反発係数を clamp（調整が安定する）
        float e = Mathf.Clamp(_parameters.coefficientOfRestitution, COR_MIN, COR_MAX);

        float numerator =
            (BALL_MASS_KG - e * effectiveBatMass) * pitchSpeed +
            effectiveBatMass * (1f + e) * batSpeed;

        float denominator = BALL_MASS_KG + effectiveBatMass;

        float baseVelocity = numerator / denominator;
        float finalVelocity = baseVelocity * efficiency;

        // ✅ 最後に上限 clamp（飛びすぎ防止のブレーキ）
        finalVelocity = Mathf.Clamp(finalVelocity, EXIT_VELOCITY_MIN_MS, EXIT_VELOCITY_MAX_MS);

        return finalVelocity;
    }

    /// <summary>
    /// 打球方向ベクトルを計算
    /// </summary>
    private Vector3 CalculateBattedBallDirection(float launchAngle, float horizontalAngle)
    {
        float launchRad = launchAngle * Mathf.Deg2Rad;
        float horizontalRad = horizontalAngle * Mathf.Deg2Rad;

        float x = Mathf.Sin(horizontalRad) * Mathf.Cos(launchRad);
        float y = Mathf.Sin(launchRad);
        float z = -Mathf.Cos(horizontalRad) * Mathf.Cos(launchRad);

        return new Vector3(x, y, z).normalized;
    }

    /// <summary>
    /// スピン軸を計算（バックスピン）
    /// </summary>
    private Vector3 CalculateSpinAxis(Vector3 direction)
    {
        Vector3 horizontalDirection = new Vector3(direction.x, 0, direction.z).normalized;

        // バックスピンで揚力(+Y)が出る向き
        Vector3 spinAxis = Vector3.Cross(horizontalDirection, Vector3.up).normalized;

        if (spinAxis.sqrMagnitude < 0.01f)
        {
            spinAxis = Vector3.right;
        }

        return spinAxis;
    }

    /// <summary>
    /// スピン量を計算
    /// </summary>
    private float CalculateSpinRate(float exitVelocity, float launchAngle, float efficiency)
    {
        float velocityFactor = exitVelocity / SPIN_VELOCITY_BASE_MS;
        float angleFactor = 1f + (launchAngle / SPIN_ANGLE_BASE_DEG) * SPIN_ANGLE_SCALE;
        float spinRate = _parameters.baseBackspinRPM * velocityFactor * angleFactor * efficiency;

        return Mathf.Clamp(spinRate, SPIN_MIN_RPM, SPIN_MAX_RPM);
    }

    /// <summary>
    /// バックスピンによる揚力係数（スピン比ベース）
    /// S = (ω * r) / v
    /// Cl = a*S/(b+S) を clamp
    /// </summary>
    private float CalculateLiftCoefficient(float spinRateRpm, float speedMs)
    {
        float v = Mathf.Max(MIN_SPEED_FOR_SPIN_RATIO, speedMs);

        float omega = spinRateRpm * RPM_TO_RAD_PER_SEC; // rad/s
        float spinRatio = (omega * BALL_RADIUS_M) / v;  // 無次元 S

        float cl = (CL_A * spinRatio) / (CL_B + spinRatio);
        cl = Mathf.Clamp(cl, CL_MIN, CL_MAX);

        return cl;
    }

    /// <summary>
    /// ミス時の処理
    /// </summary>
    private void OnMiss(Vector3 swingPosition, Vector3 ballPosition)
    {
        float distance = Vector3.Distance(swingPosition, ballPosition);

        if (_enableDebugLogs)
        {
            Debug.Log($"[BattingCalculator] ❌ 空振り！誤差: {distance * 100f:F2}cm");
        }

        // 空振り結果
        BattingBallResult missResult = new BattingBallResult
        {
            BallType = BattingBallType.Miss,
            IsHit = false,
            Distance = 0f,
            LandingPosition = MISS_LANDING_POS
        };

        // 1. 軌道イベントを先に発火（空）
        if (_trajectoryEvent != null)
        {
            _trajectoryEvent.RaiseEvent(new List<Vector3>());
            Debug.Log("[BattingCalculator] ✓ 空振り：軌道イベント発火（空）");
        }

        // 2. 結果イベントを後に発火
        if (_battedBallResultEvent != null)
        {
            _battedBallResultEvent.RaiseEvent(missResult);
            Debug.Log("[BattingCalculator] ✓ 空振り：結果イベント発火");
        }
    }

    /// <summary>
    /// 打球結果をログ出力
    /// </summary>
    private void LogBattedBallResult(BattingBallResult result)
    {
        Debug.Log($"[BattingCalculator] 投球速度から打球計算:");
        Debug.Log($"  - 打球速度: {result.ExitVelocity * 3.6f:F1} km/h");
        Debug.Log($"  - 打球角度: 仰角{result.LaunchAngle:F1}度, 水平{result.HorizontalAngle:F1}度");
        Debug.Log($"  - タイミング: {result.Timing:F3} ({GetTimingLabel(result.Timing)})");

        string pullOrOppo = result.Timing < -TIMING_PULL_OPPO_THRESHOLD ? "引っ張り" :
                           result.Timing > TIMING_PULL_OPPO_THRESHOLD ? "流し打ち" : "センター";
        Debug.Log($"  - 打撃タイプ: {pullOrOppo}");

        Debug.Log($"  - 打球方向: {GetDirectionLabel(result.HorizontalAngle)}");
        Debug.Log($"  - 打球タイプ: {GetBattedBallType(result.LaunchAngle)}");
        Debug.Log($"  - ファール: {result.IsFoul}");
        Debug.Log($"  - 打球方向ベクトル: {result.InitialVelocity.normalized}");
        Debug.Log($"  - スピン軸: {result.SpinAxis}");
        Debug.Log($"  - スピン: {result.SpinRate:F0} RPM");
        Debug.Log($"  - インパクト距離: {result.ImpactDistance * 100f:F2} cm");
        Debug.Log($"  - 芯: {result.IsSweetSpot}, 効率: {result.ImpactEfficiency:P0}");
    }

    private string GetTimingLabel(float timing)
    {
        if (timing < LABEL_TIMING_TOO_EARLY) return "早すぎ（ファール）";
        if (timing < LABEL_TIMING_EARLY) return "早め（引っ張り）";
        if (timing < LABEL_TIMING_JUST) return "ジャスト";
        if (timing < LABEL_TIMING_LATE) return "遅め（流し打ち）";
        return "遅すぎ（ファール）";
    }

    private string GetDirectionLabel(float horizontalAngle)
    {
        if (horizontalAngle < -DIRECTION_EXTREME_FOUL_DEG) return "左ファール（極端）";
        if (horizontalAngle < -DIRECTION_FOUL_DEG) return "左ファール";
        if (horizontalAngle < -DIRECTION_CORNER_OUTFIELD_DEG) return "レフト";
        if (horizontalAngle < -DIRECTION_GAP_DEG) return "レフト～レフトセンター";
        if (horizontalAngle < -DIRECTION_CENTER_TIGHT_DEG) return "レフトセンター";
        if (horizontalAngle < DIRECTION_CENTER_TIGHT_DEG) return "センター";
        if (horizontalAngle < DIRECTION_GAP_DEG) return "ライトセンター";
        if (horizontalAngle < DIRECTION_CORNER_OUTFIELD_DEG) return "ライトセンター～ライト";
        if (horizontalAngle < DIRECTION_FOUL_DEG) return "ライト";
        if (horizontalAngle < DIRECTION_EXTREME_FOUL_DEG) return "右ファール";
        return "右ファール（極端）";
    }

    private string GetBattedBallType(float launchAngle)
    {
        if (launchAngle < BALLTYPE_GROUND_STRONG_DEG) return "強烈なゴロ";
        if (launchAngle < BALLTYPE_GROUND_DEG) return "ゴロ";
        if (launchAngle < BALLTYPE_LOW_LINER_DEG) return "低いライナー";
        if (launchAngle < BALLTYPE_LINER_DEG) return "ライナー";
        if (launchAngle < BALLTYPE_LINER_FLY_DEG) return "高めのライナー/フライ";
        if (launchAngle < BALLTYPE_FLY_DEG) return "フライ";
        return "ポップフライ";
    }

    /// <summary>
    /// 打球タイプを判定
    /// </summary>
    private BattingBallType DetermineBattedBallType(
        List<Vector3> trajectory,
        float launchAngle,
        bool isFoul,
        Vector3 startPosition)
    {
        // 空振り判定（軌道が空の場合）
        if (trajectory == null || trajectory.Count == 0)
        {
            return BattingBallType.Miss;
        }

        // ファール判定
        if (isFoul)
        {
            return BattingBallType.Foul;
        }

        // 飛距離計算
        Vector3 endPosition = trajectory[trajectory.Count - 1];
        float distance = Vector3.Distance(
            new Vector3(startPosition.x, 0, startPosition.z),
            new Vector3(endPosition.x, 0, endPosition.z)
        );

        // ホームラン判定（120m以上）
        if (distance >= HOMERUN_DISTANCE_M)
        {
            return BattingBallType.HomeRun;
        }

        // ゴロ判定（打球角度10度未満）
        if (launchAngle < BALLTYPE_GROUND_DEG)
        {
            return BattingBallType.GroundBall;
        }

        // ヒット判定（フェアゾーンで一定距離以上）
        if (distance >= HIT_DISTANCE_M)
        {
            return BattingBallType.Hit;
        }

        // それ以外はゴロ扱い
        return BattingBallType.GroundBall;
    }

    /// <summary>
    /// 打球軌道を計算（メインメソッド）- 結果判定追加版
    /// </summary>
    private void CalculateBattedBallTrajectory(
        Vector3 pitchVelocity,
        Vector3 swingPosition,
        Vector3 ballPosition,
        PitchBallMove ball)
    {
        // 打球パラメータ計算
        BattingBallResult result = CalculateBattedBallParameters(
            pitchVelocity,
            swingPosition,
            ballPosition,
            ball
        );

        if (_enableDebugLogs)
        {
            LogBattedBallResult(result);
        }

        // BallPhysicsCalculatorで軌道計算
        float liftCoefficient = CalculateLiftCoefficient(result.SpinRate, result.ExitVelocity);

        var config = new BallPhysicsCalculator.SimulationConfig
        {
            DeltaTime = BATTED_SIM_DELTA_TIME,
            MaxSimulationTime = BATTED_SIM_MAX_TIME,
            StopAtZ = null,
            BounceSettings = _bounceSettings
        };

        List<Vector3> trajectory = BallPhysicsCalculator.SimulateTrajectory(
            ballPosition,
            result.InitialVelocity,
            result.SpinAxis,
            result.SpinRate,
            liftCoefficient,
            config
        );

        // 落下地点
        Vector3 landingPosition = trajectory.Count > 0 ? trajectory[trajectory.Count - 1] : Vector3.zero;

        // 打球タイプ
        BattingBallType ballType = DetermineBattedBallType(
            trajectory,
            result.LaunchAngle,
            result.IsFoul,
            ballPosition
        );

        // 飛距離
        float distance = 0f;
        if (trajectory.Count > 0)
        {
            distance = Vector3.Distance(
                new Vector3(ballPosition.x, 0, ballPosition.z),
                new Vector3(landingPosition.x, 0, landingPosition.z)
            );
        }

        // 結果更新
        result.BallType = ballType;
        result.Distance = distance;
        result.IsHit = (ballType == BattingBallType.Hit || ballType == BattingBallType.HomeRun);
        result.LandingPosition = landingPosition;

        if (_enableDebugLogs)
        {
            Debug.Log($"[打球結果] タイプ: {ballType}, 飛距離: {distance:F1}m, 落下地点: {landingPosition}");
        }

        // イベント発火の順序
        Debug.Log($"[BattingCalculator] イベント発火開始 - 軌道点数={trajectory.Count}, 結果={ballType}");

        // 1. 軌道イベントを先に発火
        if (_trajectoryEvent != null)
        {
            _trajectoryEvent.RaiseEvent(trajectory);
            Debug.Log($"[BattingCalculator] ✓ 軌道イベント発火完了: {trajectory.Count}点");
        }
        else
        {
            Debug.LogError("[BattingCalculator] BattingBallTrajectoryEventが設定されていません");
        }

        // 2. 結果イベントを後に発火
        if (_battedBallResultEvent != null)
        {
            _battedBallResultEvent.RaiseEvent(result);
            Debug.Log($"[BattingCalculator] ✓ 結果イベント発火完了: {ballType}, 落下地点: {landingPosition}");
        }
        else
        {
            Debug.LogError("[BattingCalculator] BattingResultEventが設定されていません");
        }
    }

    private void OnDrawGizmos()
    {
        if (_cursor == null || _strikeZone == null || _parameters == null) return;

        Gizmos.color = Color.green;
        Vector3 cursorPos = Application.isPlaying ? _cursor.CurrentPos : _strikeZone.Center;
        Gizmos.DrawWireSphere(cursorPos, _parameters.maxImpactDistance);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(cursorPos, _parameters.sweetSpotRadius);
    }
}
