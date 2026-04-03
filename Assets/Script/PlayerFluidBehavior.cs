using UnityEngine;

public class PlayerFluidBehavior : MonoBehaviour
{
    [Header("Fluid Controller Reference")]
    [SerializeField] private PlayerInstabilityController controller;
    [SerializeField] private Rigidbody2D rb;

    [Header("Wobble")]
    [SerializeField] private bool enableWobble = true;
    [SerializeField, Range(0f, 1f)] private float wobbleIntensity = 0.15f;
    [SerializeField, Range(1f, 10f)] private float wobbleFrequency = 3f;

    [Header("Squish & Stretch")]
    [SerializeField] private bool enableSquish = true;
    [SerializeField, Range(0f, 1f)] private float squishIntensity = 0.1f;
    [SerializeField, Min(0f)] private float speedThreshold = 0.5f;

    [Header("Child Tentacles (Optional)")]
    [SerializeField] private Transform[] tentacles;
    [SerializeField] private float tentacleWaveDistance = 0.3f;
    [SerializeField] private float tentacleWaveAmplitude = 0.1f;
    [SerializeField] private float tentacleWaveFreq = 2f;

    private Vector3 baseScale;
    private float wobbleTimer;

    private void Awake()
    {
        if (controller == null)
        {
            controller = GetComponent<PlayerInstabilityController>();
        }

        if (controller == null)
        {
            controller = GetComponentInParent<PlayerInstabilityController>();
        }

        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
        }

        if (rb == null)
        {
            rb = GetComponentInParent<Rigidbody2D>();
        }

        baseScale = transform.localScale;
    }

    private void Update()
    {
        if (controller == null || controller.CurrentForm != PlayerInstabilityController.FormState.Liquid)
        {
            ResetToBaseScale();
            return;
        }

        wobbleTimer += Time.deltaTime;

        if (enableWobble)
        {
            ApplyWobble();
        }

        if (enableSquish && rb != null)
        {
            ApplySquish();
        }

        if (tentacles != null && tentacles.Length > 0)
        {
            AnimateTentacles();
        }
    }

    private void ApplyWobble()
    {
        float wobbleX = Mathf.Sin(wobbleTimer * wobbleFrequency * Mathf.PI) * wobbleIntensity;
        float wobbleY = Mathf.Cos(wobbleTimer * wobbleFrequency * Mathf.PI * 0.7f) * wobbleIntensity;

        Vector3 wobbleScale = baseScale + new Vector3(wobbleX, wobbleY, 0f);

        if (enableSquish && rb != null && rb.linearVelocity.magnitude > speedThreshold)
        {
            float squishAmount = Mathf.Min(rb.linearVelocity.magnitude * 0.1f, squishIntensity);
            wobbleScale.x *= (1f + squishAmount);
            wobbleScale.y *= (1f - squishAmount * 0.5f);
        }

        transform.localScale = wobbleScale;
    }

    private void ApplySquish()
    {
        float speed = rb.linearVelocity.magnitude;

        if (speed < speedThreshold)
        {
            transform.localScale = Vector3.Lerp(transform.localScale, baseScale, Time.deltaTime * 4f);
            return;
        }

        float moveDirection = Mathf.Sign(rb.linearVelocity.x);
        float speedScalar = Mathf.Min(speed * 0.15f, squishIntensity);

        Vector3 squishScale = baseScale;
        squishScale.x *= (1f + speedScalar * moveDirection);
        squishScale.y *= (1f - speedScalar * 0.5f);

        transform.localScale = Vector3.Lerp(transform.localScale, squishScale, Time.deltaTime * 5f);
    }

    private void AnimateTentacles()
    {
        if (rb == null)
        {
            return;
        }

        float speed = rb.linearVelocity.magnitude;
        float wavePhase = wobbleTimer * tentacleWaveFreq;

        for (int i = 0; i < tentacles.Length; i++)
        {
            if (tentacles[i] == null)
            {
                continue;
            }

            Vector3 localPos = tentacles[i].localPosition;

            float waveOffset = Mathf.Sin(wavePhase + i * Mathf.PI / tentacles.Length) * tentacleWaveAmplitude;
            float speedInfluence = Mathf.Clamp01(speed / 5f);

            localPos.x += waveOffset * speedInfluence;
            localPos.y = tentacleWaveDistance * (i - tentacles.Length * 0.5f);

            tentacles[i].localPosition = Vector3.Lerp(tentacles[i].localPosition, localPos, Time.deltaTime * 4f);
        }
    }

    private void ResetToBaseScale()
    {
        transform.localScale = Vector3.Lerp(transform.localScale, baseScale, Time.deltaTime * 3f);

        if (tentacles != null)
        {
            foreach (Transform tentacle in tentacles)
            {
                if (tentacle != null)
                {
                    tentacle.localPosition = Vector3.Lerp(tentacle.localPosition, Vector3.zero, Time.deltaTime * 3f);
                }
            }
        }
    }

    public void ResetScale()
    {
        transform.localScale = baseScale;
    }
}
