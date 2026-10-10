using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public enum GameState { Playing, Won, Lost }

    public static GameManager Instance { get; private set; }

    [Header("Objetivo")]
    [SerializeField] private int totalKeys = 3;

    public GameState State { get; private set; } = GameState.Playing;
    public int KeysCollected { get; private set; }
    public int TotalKeys => totalKeys;
    public bool AllKeysCollected => KeysCollected >= totalKeys;

    public event Action<int, int> OnKeysChanged;
    public event Action OnAllKeysCollected;
    public event Action<GameState> OnStateChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        Time.timeScale = 1f;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Start()
    {
        OnKeysChanged?.Invoke(KeysCollected, totalKeys);
    }

    public void CollectKey()
    {
        if (State != GameState.Playing)
        {
            return;
        }

        KeysCollected = Mathf.Min(KeysCollected + 1, totalKeys);
        Debug.Log("Llaves: " + KeysCollected + "/" + totalKeys);
        OnKeysChanged?.Invoke(KeysCollected, totalKeys);

        if (AllKeysCollected)
        {
            Debug.Log("Salida abierta");
            OnAllKeysCollected?.Invoke();
        }
    }

    public void Win()
    {
        EndGame(GameState.Won);
    }

    public void Lose()
    {
        EndGame(GameState.Lost);
    }

    private void EndGame(GameState result)
    {
        if (State != GameState.Playing)
        {
            return;
        }

        State = result;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Debug.Log("Fin del juego: " + result);
        OnStateChanged?.Invoke(State);
    }

    public void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}