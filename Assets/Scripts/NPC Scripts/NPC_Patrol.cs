using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class NPC_Patrol : MonoBehaviour
{
    [Header("Patrol")]
    public Vector2[] patrolPoints;
    public float speed = 1f;
    public float arriveThreshold = 0.1f;

    [Header("Turning / Anim")]
    public Animator animator;                 // Doit contenir un int "direction"
    public float turnStepDuration = 0.06f;    // Temps par cran (8 crans = 360�)
    public bool clockwiseIndexing = false;    // Si ton index 0..7 tourne dans le sens horaire

    private Rigidbody2D rb;
    private int currentPatrolIndex = -1;
    private Vector2 target;
    private int facingIndex = 0;              // 0..7 (0 = up)
    private State state = State.Turning;
    private Coroutine turnRoutine;

    private enum State { Moving, Turning }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (!animator) animator = GetComponent<Animator>();
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            Debug.LogError($"{nameof(NPC_Patrol)}: patrolPoints is empty.");
            enabled = false;
            return;
        }
    }

    void Start()
    {
        // Prime first target then orient before moving.
        SetNextPatrolPoint();
        StartTurnToward(DesiredIndexToTarget());
    }

    void FixedUpdate()
    {
        if (state != State.Moving)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 pos = rb.position;
        Vector2 toTarget = (target - pos);
        float dist = toTarget.magnitude;

        if (dist <= arriveThreshold)
        {
            // Arrived: stop, pick next, then turn before moving again
            rb.linearVelocity = Vector2.zero;
            SetNextPatrolPoint();
            StartTurnToward(DesiredIndexToTarget());
            return;
        }

        // Move strictly in the facing direction, not the geometric direction,
        // to avoid sliding backwards while turning.
        Vector2 moveDir = DirectionVectorFromIndex(facingIndex);
        rb.linearVelocity = moveDir * speed;

        // (Optionnel) si tu veux autoriser un l�ger �lead� (<= 90�), commente la ligne au-dessus
        // et d�commente ci-dessous pour suivre la route sans �moonwalk� trop brutal :
        //
        // int desired = DesiredIndexToTarget();
        // int delta = SmallestStepDelta(facingIndex, desired);
        // if (Mathf.Abs(delta) <= 1) facingIndex = desired; // tol�rance l�g�re
        // rb.velocity = DirectionVectorFromIndex(facingIndex) * speed;

        PushAnim();
    }

    // --- Turning ---

    private void StartTurnToward(int desiredIndex)
    {
        if (turnRoutine != null) StopCoroutine(turnRoutine);
        state = State.Turning;
        turnRoutine = StartCoroutine(TurnTo(desiredIndex));
    }

    private IEnumerator TurnTo(int desiredIndex)
    {
        rb.linearVelocity = Vector2.zero;

        // Tourne par le chemin le plus court, un cran par step
        while (facingIndex != desiredIndex)
        {
            int step = Mathf.Clamp(SmallestStepDelta(facingIndex, desiredIndex), -1, 1);
            facingIndex = Mod8(facingIndex + step);
            PushAnim();
            yield return new WaitForSeconds(turnStepDuration);
        }

        state = State.Moving;
        turnRoutine = null;
    }

    private int SmallestStepDelta(int from, int to)
    {
        // delta dans [-7..+7], positif = CCW
        int diff = Mod8(to - from);
        if (diff > 4) diff -= 8; // choisir le plus court
        return diff;
    }

    // --- Patrol ---

    private void SetNextPatrolPoint()
    {
        currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
        target = patrolPoints[currentPatrolIndex];
    }

    private int DesiredIndexToTarget()
    {
        Vector2 pos = rb.position;
        Vector2 dir = (target - pos);
        return DirectionIndexFrom(dir);
    }

    // --- Direction mapping (0 = Up, 1 = UpRight, 2 = Right, ... 7 = UpLeft) ---

    private int DirectionIndexFrom(Vector2 v)
    {
        if (v.sqrMagnitude < 1e-8f) return facingIndex;

        // Angle CCW depuis "Up" (0� = Up, 90� = Right, 180� = Down, 270� = Left)
        float angle = Vector2.SignedAngle(Vector2.up, v);
        angle = (angle + 360f) % 360f;

        int idx = Mathf.RoundToInt(angle / 45f) % 8; // 8 secteurs de 45�
        if (clockwiseIndexing) idx = Mod8(8 - idx);  // si ton tableau est horaire
        return idx;
    }

    private static Vector2 DirectionVectorFromIndex(int idx)
    {
        // Index CCW depuis Up. (0,1) = Up, (1,1) = UpRight, (1,0)=Right,...
        switch (Mod8(idx))
        {
            case 0: return new Vector2(0, -1); // UP
            case 1: return new Vector2(-1, -1).normalized;// UP Right
            case 2: return new Vector2(-1, 0);//Right
            case 3: return new Vector2(-1, 1).normalized;//DOWN RIGHT
            case 4: return new Vector2(0, 1); //DOWN
            case 5: return new Vector2(1, 1).normalized; // DOWN LEFT
            case 6: return new Vector2(1, 0); // Left
            case 7: return new Vector2(1, -1).normalized;// UP LEFT
            default: return Vector2.up;
        }
    }

    private static int Mod8(int x) => (x % 8 + 8) % 8;

    private void PushAnim()
    {
        if (animator) animator.SetInteger("direction", facingIndex);
    }

#if UNITY_EDITOR
    // Petit gizmo pour voir la cible
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        if (patrolPoints != null)
        {
            for (int i = 0; i < patrolPoints.Length; i++)
            {
                Gizmos.DrawSphere(patrolPoints[i], 0.06f);
                Vector2 a = patrolPoints[i];
                Vector2 b = patrolPoints[(i + 1) % patrolPoints.Length];
                Gizmos.DrawLine(a, b);
            }
        }
    }
#endif
}
