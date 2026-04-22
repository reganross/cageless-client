public static class PositionCorrection
{
    public static Vector3 Apply(
        Vector3 current,
        Vector3 target,
        float deltaTime,
        float smallThreshold = 0.05f,
        float largeThreshold = 1.0f,
        float smoothSpeed = 10f,
        float fastSpeed = 25f)
    {
        var error = target - current;
        var distance = error.Length();

        if (distance > largeThreshold)
            return target;

        float speed = distance < smallThreshold ? smoothSpeed : fastSpeed;

        return current + error * speed * deltaTime;
    }
}