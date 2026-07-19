using UnityEngine;
using System.Collections;

public class Template : MonoBehaviour
{
    //------------------------------- Game Pause -------------------------------//

    //private void OnGameStateChanged(GameState newGameState)
    //{
    //    enabled = newGameState == GameState.Normal;
    //}

    //------------------------------- Debugging Methods -------------------------------//

    private void Debugging()
    {
        // Debug logic here
    }

    //------------------------------- Parameters -------------------------------//

    #region Components

    [Header("COMPONENTS")]
    public Object publicRef;
    public Object[] publicArray;

    [SerializeField] private Object _serializedRef;
    public Object serializedRef
    {
        get => _serializedRef;
        set => _serializedRef = value;
    }

    [SerializeField] private Object _readOnlyRef;
    public Object readOnlyRef
    {
        get => _readOnlyRef;
    }

    private Object _cachedRef;
    public Object cachedRef
    {
        get => _cachedRef;
    }

    #endregion

    #region Flags

    [SerializeField] private bool _flag;
    public bool flag
    {
        get => _flag;
        set => _flag = value;
    }

    [SerializeField] private bool _initialized;
    public bool initialized
    {
        get => _initialized;
        set => _initialized = value;
    }

    #endregion

    //------------------------------- Unity Methods -------------------------------//

    #region Unity

    private void Start()
    {
        Initialize();
    }

    private void Update()
    {
        UpdateLoop();
    }

    private void FixedUpdate()
    {
        FixedUpdateLoop();
    }

    private void OnDestroy()
    {
        //GameplayManager.GameStateChangedEvent -= OnGameStateChanged;
        UnsubscribeEvents();
    }

    #endregion

    //------------------------------- Initialization Methods -------------------------------//

    #region Initialization

    [Header("INITIALIZATION")]
    [SerializeField] private string _temp;

    private void Initialize()
    {
        InitializeModules();
        InitializeComponents();
        InitializeEvents();
    }

    private void InitializeModules()
    {
        // Module setup here
    }

    private void InitializeComponents()
    {
        // Cache components here
    }

    private void InitializeEvents()
    {
        ///GameplayManager.GameStateChangedEvent += OnGameStateChanged;
        // Subscribe to other events here
    }

    private void UnsubscribeEvents()
    {
        // Unsubscribe from any additional events here
    }

    #endregion

    //------------------------------- Core / Routines Methods -------------------------------//

    private void UpdateLoop()
    {
        // Core update logic
    }

    private void FixedUpdateLoop()
    {
        // Core fixed update logic
    }

    protected IEnumerator CoreRoutine(float duration)
    {
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            // Respect the custom game pause system
            //if (GameplayManager.IsGamePaused)
            //{
            //    yield return null;
            //    continue;
            //}

            elapsedTime += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        // Ensure routine resumes fully after pause
        //yield return new WaitUntil(() => !GameplayManager.IsGamePaused);
    }

#if UNITY_EDITOR
    //------------------------------- Editor -------------------------------//
    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            InitializeComponents();
        }
    }
#endif
}
