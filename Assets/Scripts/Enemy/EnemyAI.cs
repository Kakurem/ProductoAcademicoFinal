using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAI : MonoBehaviour
{
    private enum State { Wander, Investigate, Chase }

    [Header("Referencias")]
    [SerializeField] private PlayerController player;
    [SerializeField] private Transform waypointsRoot;

    [Header("Velocidades (m/s)")]
    [SerializeField] private float wanderSpeed = 2.2f;
    [SerializeField] private float investigateSpeed = 3f;
    [SerializeField] private float chaseSpeed = 4.2f;

    [Header("Visión")]
    [SerializeField] private float viewRange = 14f;
    [SerializeField] private float viewAngle = 100f;
    [SerializeField] private float eyeHeight = 0.6f;
    [SerializeField] private float targetHeight = 0.5f;
    [SerializeField] private float sneakViewMultiplier = 0.6f;
    [SerializeField] private float closeSenseRadius = 2f;

    [Header("Oído")]
    [SerializeField] private float wallNoiseMultiplier = 0.5f;

    [Header("Predicción")]
    [SerializeField] private float predictionTime = 0.7f;

    [Header("Comportamiento")]
    [SerializeField] private float waypointWait = 1.5f;
    [SerializeField] private float investigateLookTime = 3f;
    [SerializeField] private float loseSightTime = 2.5f;
    [SerializeField] private float catchDistance = 1.3f;
    [SerializeField] private float arriveDistance = 0.8f;
    [SerializeField] private float lookSpinSpeed = 120f;

    [Header("Optimización")]
    [SerializeField] private float senseInterval = 0.1f;

    [Header("Depuración")]
    [SerializeField] private bool logStateChanges = true;

    private NavMeshAgent agent;
    private Transform[] waypoints;
    private State state = State.Wander;
    private int currentWaypoint = -1;

    private float waitTimer;
    private float investigateTimer;
    private float lostSightTimer;
    private float senseTimer;

    private bool canSeePlayer;
    private bool canHearPlayer;

    // Memoria temporal del enemigo: solo la última posición conocida del jugador.
    // No guarda nada sobre pasajes ni otros lugares.
    private bool hasLastKnown;
    private Vector3 lastKnownPosition;

    private Vector3 playerVelocity;
    private Vector3 previousPlayerPosition;
    private bool hasPreviousPlayerPosition;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

        if (player == null)
        {
            player = FindFirstObjectByType<PlayerController>();
        }

        if (waypointsRoot != null)
        {
            waypoints = new Transform[waypointsRoot.childCount];
            for (int i = 0; i < waypoints.Length; i++)
            {
                waypoints[i] = waypointsRoot.GetChild(i);
            }
        }
        else
        {
            waypoints = new Transform[0];
        }
    }

    private void Start()
    {
        if (!agent.isOnNavMesh)
        {
            Debug.LogWarning("EnemyAI: el enemigo no está sobre el NavMesh.");
        }

        if (waypoints.Length == 0)
        {
            Debug.LogWarning("EnemyAI: no hay waypoints asignados (arrastra el objeto Waypoints al campo Waypoints Root).");
        }

        EnterWander();
    }

    private void Update()
    {
        if (player == null || !agent.isOnNavMesh)
        {
            return;
        }

        if (GameManager.Instance != null &&
            GameManager.Instance.State != GameManager.GameState.Playing)
        {
            agent.isStopped = true;
            return;
        }

        TrackPlayerVelocity();

        // Optimización: la percepción se calcula cada 'senseInterval' segundos
        // y no en cada fotograma.
        senseTimer -= Time.deltaTime;
        if (senseTimer <= 0f)
        {
            senseTimer = senseInterval;
            Sense();
        }

        switch (state)
        {
            case State.Wander:
                UpdateWander();
                break;
            case State.Investigate:
                UpdateInvestigate();
                break;
            case State.Chase:
                UpdateChase();
                break;
        }

        CheckCatch();
    }

    // ---------- Percepción ----------

    private void Sense()
    {
        canSeePlayer = false;
        canHearPlayer = false;

        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        Vector3 target = player.transform.position + Vector3.up * targetHeight;
        Vector3 toPlayer = target - eye;
        float distance = toPlayer.magnitude;

        // Optimización: si el jugador está fuera de todo alcance, no se hace ningún Raycast.
        float maxRange = Mathf.Max(viewRange, player.NoiseRadius);
        if (distance > maxRange)
        {
            return;
        }

        bool lineOfSight = HasLineOfSight(eye, toPlayer, distance);

        // Visión: distancia + cono + línea de vista (Raycast)
        float range = viewRange;
        if (player.CurrentMode == PlayerController.MoveMode.Sneak)
        {
            range *= sneakViewMultiplier;
        }

        if (lineOfSight && distance <= range)
        {
            Vector3 flat = new Vector3(toPlayer.x, 0f, toPlayer.z);
            float angle = Vector3.Angle(transform.forward, flat);

            if (angle <= viewAngle * 0.5f || distance <= closeSenseRadius)
            {
                canSeePlayer = true;
            }
        }

        // Oído: el radio de ruido del jugador, amortiguado si hay paredes de por medio
        float noise = player.NoiseRadius;
        if (noise > 0f)
        {
            float effective = lineOfSight ? noise : noise * wallNoiseMultiplier;
            if (distance <= effective)
            {
                canHearPlayer = true;
            }
        }
    }

    private bool HasLineOfSight(Vector3 from, Vector3 toPlayer, float distance)
    {
        if (distance <= 0.01f)
        {
            return true;
        }

        if (Physics.Raycast(from, toPlayer / distance, out RaycastHit hit, distance, ~0, QueryTriggerInteraction.Ignore))
        {
            return hit.collider.CompareTag("Player");
        }

        return true;
    }

    private void TrackPlayerVelocity()
    {
        Vector3 current = player.transform.position;

        if (hasPreviousPlayerPosition && Time.deltaTime > 0f)
        {
            Vector3 instant = (current - previousPlayerPosition) / Time.deltaTime;
            instant.y = 0f;
            playerVelocity = Vector3.Lerp(playerVelocity, instant, 10f * Time.deltaTime);
        }

        previousPlayerPosition = current;
        hasPreviousPlayerPosition = true;
    }

    // ---------- Estados ----------

    private void UpdateWander()
    {
        if (canSeePlayer)
        {
            EnterChase();
            return;
        }

        if (canHearPlayer)
        {
            lastKnownPosition = player.transform.position;
            hasLastKnown = true;
            EnterInvestigate();
            return;
        }

        if (waypoints.Length == 0)
        {
            return;
        }

        if (HasArrived())
        {
            waitTimer += Time.deltaTime;
            if (waitTimer >= waypointWait)
            {
                waitTimer = 0f;
                ChooseNextWaypoint();
            }
        }
    }

    private void UpdateInvestigate()
    {
        if (canSeePlayer)
        {
            EnterChase();
            return;
        }

        if (canHearPlayer)
        {
            lastKnownPosition = player.transform.position;
            hasLastKnown = true;
            investigateTimer = 0f;
            agent.SetDestination(lastKnownPosition);
            return;
        }

        if (!HasArrived())
        {
            return;
        }

        // Llegó al lugar: mira a su alrededor un momento y vuelve a patrullar
        investigateTimer += Time.deltaTime;
        transform.Rotate(0f, lookSpinSpeed * Time.deltaTime, 0f);

        if (investigateTimer >= investigateLookTime)
        {
            EnterWander();
        }
    }

    private void UpdateChase()
    {
        if (canSeePlayer)
        {
            lostSightTimer = 0f;
            lastKnownPosition = player.transform.position;
            hasLastKnown = true;
            agent.SetDestination(GetPredictedPosition());
            return;
        }

        lostSightTimer += Time.deltaTime;

        if (hasLastKnown)
        {
            agent.SetDestination(lastKnownPosition);
        }

        if (lostSightTimer >= loseSightTime)
        {
            EnterInvestigate();
        }
    }

    private void EnterWander()
    {
        SetState(State.Wander);
        ForgetAll();
        agent.isStopped = false;
        agent.speed = wanderSpeed;
        waitTimer = 0f;
        ChooseNextWaypoint();
    }

    private void EnterInvestigate()
    {
        SetState(State.Investigate);
        agent.isStopped = false;
        agent.speed = investigateSpeed;
        investigateTimer = 0f;

        if (hasLastKnown)
        {
            agent.SetDestination(lastKnownPosition);
        }
    }

    private void EnterChase()
    {
        SetState(State.Chase);
        agent.isStopped = false;
        agent.speed = chaseSpeed;
        lostSightTimer = 0f;
    }

    private void SetState(State newState)
    {
        if (state == newState)
        {
            return;
        }

        state = newState;

        if (logStateChanges)
        {
            Debug.Log("Enemigo: " + newState);
        }
    }

    // El enemigo "olvida" todo lo que sabía al volver a patrullar.
    private void ForgetAll()
    {
        hasLastKnown = false;
        lastKnownPosition = Vector3.zero;
        lostSightTimer = 0f;
        investigateTimer = 0f;
        playerVelocity = Vector3.zero;
    }

    // ---------- Movimiento ----------

    private void ChooseNextWaypoint()
    {
        if (waypoints == null || waypoints.Length == 0)
        {
            return;
        }

        int next = currentWaypoint;

        if (waypoints.Length > 1)
        {
            while (next == currentWaypoint)
            {
                next = Random.Range(0, waypoints.Length);
            }
        }
        else
        {
            next = 0;
        }

        currentWaypoint = next;
        agent.SetDestination(waypoints[currentWaypoint].position);
    }

    private bool HasArrived()
    {
        return !agent.pathPending && agent.remainingDistance <= arriveDistance;
    }

    // Predicción: hacia dónde estará el jugador en 'predictionTime' segundos
    private Vector3 GetPredictedPosition()
    {
        Vector3 playerPosition = player.transform.position;
        Vector3 predicted = playerPosition + playerVelocity * predictionTime;

        // Si hay una pared entre el jugador y el punto predicho, no se predice a través de ella
        Vector3 from = playerPosition + Vector3.up * targetHeight;
        Vector3 to = predicted + Vector3.up * targetHeight;
        if (Physics.Linecast(from, to, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
        {
            if (!hit.collider.CompareTag("Player"))
            {
                return playerPosition;
            }
        }

        if (NavMesh.SamplePosition(predicted, out NavMeshHit navHit, 2f, NavMesh.AllAreas))
        {
            return navHit.position;
        }

        return playerPosition;
    }

    private void CheckCatch()
    {
        Vector3 offset = player.transform.position - transform.position;
        offset.y = 0f;

        if (offset.magnitude <= catchDistance && GameManager.Instance != null)
        {
            GameManager.Instance.Lose();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 eye = transform.position + Vector3.up * eyeHeight;

        Gizmos.color = Color.red;
        Vector3 left = Quaternion.Euler(0f, -viewAngle * 0.5f, 0f) * transform.forward;
        Vector3 right = Quaternion.Euler(0f, viewAngle * 0.5f, 0f) * transform.forward;
        Gizmos.DrawRay(eye, left * viewRange);
        Gizmos.DrawRay(eye, right * viewRange);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, closeSenseRadius);

        if (hasLastKnown)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(lastKnownPosition, 0.5f);
        }
    }
}