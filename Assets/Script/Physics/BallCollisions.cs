using UnityEngine;

internal static class BallCollisions
{
    /// <summary>
    /// 地面バウンド処理
    /// </summary>
    public static bool HandleGroundBounce(ref Vector3 velocity, BounceSettings settings, int bounceCount)
    {
        velocity.y = -velocity.y * settings.groundRestitution;

        float horizontalSpeed = new Vector2(velocity.x, velocity.z).magnitude;
        float frictionLoss = horizontalSpeed * settings.groundFriction;

        if (horizontalSpeed > 0.01f)
        {
            Vector3 horizontalDir = new Vector3(velocity.x, 0, velocity.z).normalized;
            float newHorizontalSpeed = Mathf.Max(0, horizontalSpeed - frictionLoss);
            velocity.x = horizontalDir.x * newHorizontalSpeed;
            velocity.z = horizontalDir.z * newHorizontalSpeed;
        }

        bool shouldRoll = velocity.y < 2f && bounceCount >= 2;
        return shouldRoll;
    }

    /// <summary>
    /// 壁バウンド処理（fieldBounds用）
    /// </summary>
    public static void HandleWallBounce(ref Vector3 position, ref Vector3 velocity, BounceSettings settings)
    {
        Bounds bounds = settings.fieldBounds;

        if (position.x < bounds.min.x)
        {
            position.x = bounds.min.x;
            velocity.x = -velocity.x * settings.wallRestitution;
        }
        else if (position.x > bounds.max.x)
        {
            position.x = bounds.max.x;
            velocity.x = -velocity.x * settings.wallRestitution;
        }

        if (position.z < bounds.min.z)
        {
            position.z = bounds.min.z;
            velocity.z = -velocity.z * settings.wallRestitution;
        }
        else if (position.z > bounds.max.z)
        {
            position.z = bounds.max.z;
            velocity.z = -velocity.z * settings.wallRestitution;
        }
    }

    /// <summary>
    /// 転がり処理
    /// </summary>
    public static Vector3 SimulateRolling(Vector3 position, ref Vector3 velocity,
                                         BounceSettings settings, float deltaTime)
    {
        velocity.y = 0f;
        velocity.x *= settings.rollingDeceleration;
        velocity.z *= settings.rollingDeceleration;

        Vector3 nextPosition = position + velocity * deltaTime;
        nextPosition.y = settings.groundLevel;
        return nextPosition;
    }

    /// <summary>
    /// フィールド境界外判定
    /// </summary>
    public static bool IsOutOfBounds(Vector3 position, Bounds bounds)
    {
        return position.x < bounds.min.x || position.x > bounds.max.x ||
               position.z < bounds.min.z || position.z > bounds.max.z;
    }

    // =========================================================
    // ✅ Net(Layer: Net) 反射（MeshColliderにRaycastし、Bounds面で反射）
    // =========================================================

    internal static bool TryReflectOnNetBoundsPlane(
        Vector3 position,
        ref Vector3 nextPosition,
        ref Vector3 velocity,
        float deltaTime,
        float restitution)
    {
        int netLayer = LayerMask.NameToLayer("Net");
        if (netLayer < 0) return false;

        int netMask = 1 << netLayer;

        Vector3 segment = nextPosition - position;
        float dist = segment.magnitude;
        if (dist < 0.0001f) return false;

        Vector3 dir = segment / dist;

        if (!Physics.Raycast(position, dir, out RaycastHit hit, dist, netMask, QueryTriggerInteraction.Ignore))
            return false;

        float tHit = hit.distance / dist; // 0..1
        Vector3 hitPos = Vector3.Lerp(position, nextPosition, tHit);

        Bounds b = hit.collider.bounds;
        Vector3 normal = GetBoundsPlaneNormalXZ(b, hitPos, dir);

        Vector3 reflected = Vector3.Reflect(velocity, normal) * restitution;

        // 貼り付き防止の押し戻し
        hitPos += normal * BallPhysicsConstants.NET_EPSILON;

        float remaining = 1f - tHit;
        nextPosition = hitPos + reflected * (deltaTime * remaining);

        velocity = reflected;
        return true;
    }

    private static Vector3 GetBoundsPlaneNormalXZ(Bounds b, Vector3 hitPos, Vector3 dir)
    {
        float dxMin = Mathf.Abs(hitPos.x - b.min.x);
        float dxMax = Mathf.Abs(hitPos.x - b.max.x);
        float dzMin = Mathf.Abs(hitPos.z - b.min.z);
        float dzMax = Mathf.Abs(hitPos.z - b.max.z);

        float min = dxMin;
        Vector3 normal = Vector3.right; // x=min -> +X（内側）

        if (dxMax < min) { min = dxMax; normal = Vector3.left; }
        if (dzMin < min) { min = dzMin; normal = Vector3.forward; }
        if (dzMax < min) { min = dzMax; normal = Vector3.back; }

        if (Vector3.Dot(normal, dir) > 0f)
            normal = -normal;

        return normal.normalized;
    }
}
