using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// バッティングの当たり判定と打撃計算を行う
/// </summary>
public class BattingCalculator : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private BattingCursor _cursor;
    [SerializeField] private StrikeZone _strikeZone;
    [SerializeField] private BattingData _parameters;
    [SerializeField] private BounceSettings _bounceSettings;

    [Header("イベント")]
    [SerializeField] private BattingHitEvent _hitEvent;
    [SerializeField] private BattingBallTrajectoryEvent _trajectoryEvent;
    [SerializeField] private BattingResultEvent _battedBallResultEvent;

    [Header("軌道可視化")]
    [SerializeField] private bool _showTrajectory = true;
    [SerializeField] private LineRenderer _trajectoryLine;
    [SerializeField] private Color _hitTrajectoryColor = Color.green;
    [SerializeField] private Color _missTrajectoryColor = Color.red;

    [Header("デバッグ")]
    [SerializeField] private bool _enableDebugLogs = true;

    private void Start()
    {
        // LineRendererを自動生成（なければ）
        if (_trajectoryLine == null && _showTrajectory)
        {
            GameObject lineObj = new GameObject("BattedBallTrajectoryLine");
            lineObj.transform.SetParent(transform);
            _trajectoryLine = lineObj.AddComponent<LineRenderer>();

            _trajectoryLine.startWidth = 0.05f;
            _trajectoryLine.endWidth = 0.05f;
            _trajectoryLine.material = new Material(Shader.Find("Sprites/Default"));
            _trajectoryLine.startColor = _hitTrajectoryColor;
            _trajectoryLine.endColor = _hitTrajectoryColor;
            _trajectoryLine.useWorldSpace = true;
            _trajectoryLine.enabled = false;
        }

        if (_hitEvent != null)
        {
            _hitEvent.RegisterListener(OnHitAttempt);
        }
        else
        {
            Debug.LogError("[BattingCalculator] BattingHitEventが設定されていません");
        }

        // ✅ デバッグ：イベント設定確認
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
    private void OnHitAttempt(Ball ball)
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

            // 打球軌道を計算して返す
            List<Vector3> trajectory = CalculateBattedBallTrajectory(
                pitchVelocity,
                swingPosition,
                ballPosition,
                ball
            );

            // 軌道を描画
            if (_showTrajectory)
            {
                DrawTrajectory(trajectory, _hitTrajectoryColor);
            }

            // 結果を通知
            OnHitSuccess(trajectory);
        }
        else
        {
            // ミスの場合は軌道をクリア
            if (_showTrajectory)
            {
                ClearTrajectory();
            }

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
    private Vector3 GetBallPositionAtStrikeZone(Ball ball)
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
    /// 打球パラメータを計算（内部用）
    /// </summary>
    private BattingBallResult CalculateBattedBallParameters(
        Vector3 pitchVelocity,
        Vector3 swingPosition,
        Vector3 ballPosition,
        Ball ball)
    {
        // インパクト距離（芯からのズレ）
        float impactDistance = Vector3.Distance(swingPosition, ballPosition);

        // インパクト効率を計算
        float impactEfficiency = CalculateImpactEfficiency(impactDistance);
        bool isSweetSpot = impactDistance <= _parameters.sweetSpotRadius;

        // 打球速度を計算
        float exitVelocity = CalculateExitVelocity(
            pitchVelocity.magnitude,
            _parameters.batSpeedKmh / 3.6f,
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

        float normalizedOffset = verticalOffset * 10f;
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
    private float CalculateSwingTimingSimple(Ball ball)
    {
        Vector3 ballPosition = ball.transform.position;
        float strikeZoneZ = _strikeZone.Center.z;

        float distanceToZone = ballPosition.z - strikeZoneZ;

        const float DISTANCE_WINDOW = 2f;
        float normalizedTiming = Mathf.Clamp(distanceToZone / DISTANCE_WINDOW, -1f, 1f);

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
    private Vector3 GetPitchVelocity(Ball ball, Vector3 ballPosition)
    {
        List<Vector3> trajectory = ball.Trajectory;
        float strikeZoneZ = _strikeZone.Center.z;

        for (int i = 0; i < trajectory.Count - 1; i++)
        {
            Vector3 p1 = trajectory[i];
            Vector3 p2 = trajectory[i + 1];

            if (p1.z <= strikeZoneZ && p2.z >= strikeZoneZ)
            {
                Vector3 velocity = (p2 - p1) / 0.01f;
                return velocity;
            }
        }

        if (trajectory.Count >= 2)
        {
            Vector3 avgVelocity = (trajectory[trajectory.Count - 1] - trajectory[0]) /
                                 (trajectory.Count * 0.01f);
            return avgVelocity;
        }

        Debug.LogWarning("[BattingCalculator] 投球速度の取得に失敗");
        return Vector3.forward * 30f;
    }

    /// <summary>
    /// インパクト効率を計算（芯からの距離に応じて減衰）
    /// </summary>
    private float CalculateImpactEfficiency(float impactDistance)
    {
        if (impactDistance <= _parameters.sweetSpotRadius)
        {
            return 1.0f;
        }

        if (impactDistance >= _parameters.maxImpactDistance)
        {
            return 0.1f;
        }

        float normalizedDistance = (impactDistance - _parameters.sweetSpotRadius) /
                                  (_parameters.maxImpactDistance - _parameters.sweetSpotRadius);
        return Mathf.Clamp01(1.0f - normalizedDistance * normalizedDistance);
    }

    /// <summary>
    /// 打球速度を計算（反発係数と運動量保存則）
    /// </summary>
    private float CalculateExitVelocity(float pitchSpeed, float batSpeed, float efficiency)
    {
        const float BALL_MASS = 0.145f;
        float effectiveBatMass = _parameters.batMass * 0.7f;

        float numerator = (BALL_MASS - _parameters.coefficientOfRestitution * effectiveBatMass) * pitchSpeed
                        + effectiveBatMass * (1f + _parameters.coefficientOfRestitution) * batSpeed;
        float denominator = BALL_MASS + effectiveBatMass;

        float baseVelocity = numerator / denominator;
        float finalVelocity = baseVelocity * efficiency;

        return Mathf.Max(0f, finalVelocity);
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
        Vector3 spinAxis = Vector3.Cross(Vector3.up, horizontalDirection).normalized;

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
        float velocityFactor = exitVelocity / 40f;
        float angleFactor = 1f + (launchAngle / 30f) * 0.3f;
        float spinRate = _parameters.baseBackspinRPM * velocityFactor * angleFactor * efficiency;
        return Mathf.Clamp(spinRate, 500f, 4000f);
    }

    /// <summary>
    /// バックスピンによる揚力係数
    /// </summary>
    private float CalculateLiftCoefficient(float spinRate)
    {
        return 0.2f + (spinRate / 2500f) * 0.35f;
    }

    /// <summary>
    /// ヒット成功時の処理
    /// </summary>
    private void OnHitSuccess(List<Vector3> trajectory)
    {
        if (_enableDebugLogs)
        {
            Debug.Log($"[BattingCalculator] ✅ ヒット成功！");
        }

        // ✅ ここでは軌道イベントのみ（既に CalculateBattedBallTrajectory で発火済み）
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

        // ✅ 空振り結果
        BattingBallResult missResult = new BattingBallResult
        {
            BallType = BattingBallType.Miss,
            IsHit = false,
            Distance = 0f
        };

        // ✅✅✅ 重要：イベント発火の順序 ✅✅✅
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
    /// 軌道を描画
    /// </summary>
    private void DrawTrajectory(List<Vector3> trajectory, Color color)
    {
        if (_trajectoryLine == null || trajectory == null || trajectory.Count == 0)
            return;

        _trajectoryLine.enabled = true;
        _trajectoryLine.positionCount = trajectory.Count;
        _trajectoryLine.SetPositions(trajectory.ToArray());
        _trajectoryLine.startColor = color;
        _trajectoryLine.endColor = color;
    }

    /// <summary>
    /// 軌道をクリア
    /// </summary>
    private void ClearTrajectory()
    {
        if (_trajectoryLine != null)
        {
            _trajectoryLine.enabled = false;
            _trajectoryLine.positionCount = 0;
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

        string pullOrOppo = result.Timing < -0.1f ? "引っ張り" :
                           result.Timing > 0.1f ? "流し打ち" : "センター";
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
        if (timing < -0.7f) return "早すぎ（ファール）";
        if (timing < -0.3f) return "早め（引っ張り）";
        if (timing < 0.3f) return "ジャスト";
        if (timing < 0.7f) return "遅め（流し打ち）";
        return "遅すぎ（ファール）";
    }

    private string GetDirectionLabel(float horizontalAngle)
    {
        if (horizontalAngle < -60f) return "左ファール（極端）";
        if (horizontalAngle < -45f) return "左ファール";
        if (horizontalAngle < -30f) return "レフト";
        if (horizontalAngle < -15f) return "レフト～レフトセンター";
        if (horizontalAngle < -5f) return "レフトセンター";
        if (horizontalAngle < 5f) return "センター";
        if (horizontalAngle < 15f) return "ライトセンター";
        if (horizontalAngle < 30f) return "ライトセンター～ライト";
        if (horizontalAngle < 45f) return "ライト";
        if (horizontalAngle < 60f) return "右ファール";
        return "右ファール（極端）";
    }

    private string GetBattedBallType(float launchAngle)
    {
        if (launchAngle < 0f) return "強烈なゴロ";
        if (launchAngle < 10f) return "ゴロ";
        if (launchAngle < 15f) return "低いライナー";
        if (launchAngle < 25f) return "ライナー";
        if (launchAngle < 35f) return "高めのライナー/フライ";
        if (launchAngle < 45f) return "フライ";
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
        if (distance >= 120f)
        {
            return BattingBallType.HomeRun;
        }

        // ゴロ判定（打球角度10度未満）
        if (launchAngle < 10f)
        {
            return BattingBallType.GroundBall;
        }

        // ヒット判定（フェアゾーンで一定距離以上）
        if (distance >= 30f)  // 30m以上はヒット扱い
        {
            return BattingBallType.Hit;
        }

        // それ以外はゴロ扱い
        return BattingBallType.GroundBall;
    }

    /// <summary>
    /// 打球軌道を計算（メインメソッド）- 結果判定追加版
    /// </summary>
    private List<Vector3> CalculateBattedBallTrajectory(
        Vector3 pitchVelocity,
        Vector3 swingPosition,
        Vector3 ballPosition,
        Ball ball)
    {
        // === 内部計算用のBattedBallResult ===
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

        // === BallPhysicsCalculatorで軌道計算 ===
        float liftCoefficient = CalculateLiftCoefficient(result.SpinRate);

        var config = new BallPhysicsCalculator.SimulationConfig
        {
            DeltaTime = 0.01f,
            MaxSimulationTime = 10f,
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

        // ✅ 打球タイプを判定
        BattingBallType ballType = DetermineBattedBallType(
            trajectory,
            result.LaunchAngle,
            result.IsFoul,
            ballPosition
        );

        // ✅ 飛距離を計算
        float distance = 0f;
        if (trajectory.Count > 0)
        {
            Vector3 endPosition = trajectory[trajectory.Count - 1];
            distance = Vector3.Distance(
                new Vector3(ballPosition.x, 0, ballPosition.z),
                new Vector3(endPosition.x, 0, endPosition.z)
            );
        }

        // ✅ 結果を更新
        result.BallType = ballType;
        result.Distance = distance;
        result.IsHit = (ballType == BattingBallType.Hit || ballType == BattingBallType.HomeRun);

        if (_enableDebugLogs)
        {
            Debug.Log($"[打球結果] タイプ: {ballType}, 飛距離: {distance:F1}m");
        }

        // ✅✅✅ 重要：イベント発火の順序 ✅✅✅
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
            Debug.Log($"[BattingCalculator] ✓ 結果イベント発火完了: {ballType}");
        }
        else
        {
            Debug.LogError("[BattingCalculator] BattingResultEventが設定されていません");
        }

        return trajectory;
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