using System;
using UnityEngine;

namespace Portfolio.Game
{
    public class GameManager : MonoBehaviour
    {
        //------------------------------- Parameters -------------------------------//

        #region Components

        private static GameManager _instance;
        private GameState _currentState = GameState.Normal;

        public static GameManager Instance => _instance;
        public GameState CurrentState => _currentState;
        public bool IsPaused => _currentState == GameState.Paused;

        public static event Action<GameState> GameStateChanged;

        #endregion

        #region Flags

        private bool _initialized;

        #endregion

        //------------------------------- Unity Methods -------------------------------//

        #region Unity

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
            GameStateChanged = null;
            Time.timeScale = 1f;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            Initialize();
        }

        private void OnDestroy()
        {
            if (_instance != this)
            {
                return;
            }

            Time.timeScale = 1f;
            _instance = null;
            GameStateChanged = null;
        }

        #endregion

        //------------------------------- Initialization Methods -------------------------------//

        #region Initialization

        private void Initialize()
        {
            if (_initialized || _instance != this)
            {
                return;
            }

            _currentState = GameState.Normal;
            ApplyTimeScale();
            _initialized = true;
        }

        #endregion

        //------------------------------- Core / Routines Methods -------------------------------//

        #region Game State

        public void SetState(GameState state)
        {
            if (_currentState == state)
            {
                return;
            }

            _currentState = state;
            ApplyTimeScale();
            GameStateChanged?.Invoke(_currentState);
        }

        public void SetPaused(bool paused)
        {
            SetState(paused ? GameState.Paused : GameState.Normal);
        }

        public void TogglePause()
        {
            SetPaused(!IsPaused);
        }

        private void ApplyTimeScale()
        {
            Time.timeScale = IsPaused ? 0f : 1f;
        }

        #endregion
    }
}
