using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Collectible : MonoBehaviour
{
    [Header("Flotación")]
    [SerializeField] private float bobHeight = 0.1f;
    [SerializeField] private float bobSpeed = 2f;

    private Vector3 startPosition;
    private bool collected;

    private void Start()
    {
        startPosition = transform.position;
    }

    private void Update()
    {
        Vector3 position = startPosition;
        position.y += Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.position = position;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (collected || !other.CompareTag("Player"))
        {
            return;
        }

        collected = true;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.CollectKey();
        }

        gameObject.SetActive(false);
    }
}