using UnityEngine;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Renderer))]
public class ExitDoor : MonoBehaviour
{
    [Header("Colores")]
    [SerializeField] private Color lockedColor = new Color(0.8f, 0.1f, 0.1f);
    [SerializeField] private Color openColor = new Color(0.1f, 0.8f, 0.2f);

    private Collider doorCollider;
    private Renderer doorRenderer;
    private bool isOpen;

    private void Awake()
    {
        doorCollider = GetComponent<Collider>();
        doorRenderer = GetComponent<Renderer>();
        SetOpen(false);
    }

    private void Start()
    {
        if (GameManager.Instance == null)
        {
            Debug.LogWarning("ExitDoor: no hay GameManager en la escena.");
            return;
        }

        GameManager.Instance.OnAllKeysCollected += OpenDoor;

        if (GameManager.Instance.AllKeysCollected)
        {
            OpenDoor();
        }
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnAllKeysCollected -= OpenDoor;
        }
    }

    private void OpenDoor()
    {
        SetOpen(true);
    }

    private void SetOpen(bool open)
    {
        isOpen = open;
        doorCollider.isTrigger = open;
        doorRenderer.material.color = open ? openColor : lockedColor;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isOpen || !other.CompareTag("Player"))
        {
            return;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.Win();
        }
    }
}