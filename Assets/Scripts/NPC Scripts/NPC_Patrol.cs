using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NPC_Patrol : MonoBehaviour
{
    public Vector2[] patrolPoints;
    public Vector2 target;
    public float speed = 2;
    private Rigidbody2D rb;
    private int currentPatrolIndex;

    // Start is called before the first frame update
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        currentPatrolIndex = -1;
        SetPatrolPoint();
    }

    // Update is called once per frame
    void Update()
    {
        Vector3 position = transform.position;
        Vector2 direction = ((Vector3)target - position).normalized;
        rb.linearVelocity = direction * speed;
        float distance = Vector2.Distance(position, target);
        if (distance < .1f)
            SetPatrolPoint();
    }

    private void SetPatrolPoint() 
    {
        currentPatrolIndex= (currentPatrolIndex + 1) % patrolPoints.Length;
        target = patrolPoints[currentPatrolIndex];
    }
}
