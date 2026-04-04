using UnityEngine;

public class LoopMoveXYZ : MonoBehaviour
{
    [SerializeField] private Vector3 moveRange = new Vector3(5f, 0f, 0f);
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private bool useLocalPosition;
    [SerializeField] private bool startFromCurrentPosition = true;

    private Vector3 startPosition;
    private Vector3 sanitizedRange;
    private float elapsedTime;
    private float phaseOffset;

    private void Awake()
    {
        CacheStartPosition();
        SanitizeRange();
        phaseOffset = Random.Range(0f, 1000f);
    }

    private void Start()
    {
        if (startFromCurrentPosition)
        {
            CacheStartPosition();
        }

        SanitizeRange();
    }

    private void Update()
    {
        elapsedTime += Time.deltaTime;
        float timeValue = (elapsedTime + phaseOffset) * SafeFloat(moveSpeed);
        float offsetX = GetLoopOffset(timeValue, sanitizedRange.x);
        float offsetY = GetLoopOffset(timeValue, sanitizedRange.y);
        float offsetZ = GetLoopOffset(timeValue, sanitizedRange.z);

        Vector3 nextPosition = startPosition + new Vector3(offsetX, offsetY, offsetZ);

        if (useLocalPosition)
        {
            transform.localPosition = nextPosition;
        }
        else
        {
            transform.position = nextPosition;
        }
    }

    private void CacheStartPosition()
    {
        startPosition = useLocalPosition ? transform.localPosition : transform.position;
    }

    private void SanitizeRange()
    {
        sanitizedRange = new Vector3(
            SafeFloat(moveRange.x),
            SafeFloat(moveRange.y),
            SafeFloat(moveRange.z));
    }

    private static float GetLoopOffset(float timeValue, float range)
    {
        float safeRange = Mathf.Max(0f, SafeFloat(range));
        if (safeRange <= 0f)
        {
            return 0f;
        }

        return Mathf.PingPong(timeValue, safeRange * 2f) - safeRange;
    }

    private static float SafeFloat(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            return 0f;
        }

        return value;
    }
}
