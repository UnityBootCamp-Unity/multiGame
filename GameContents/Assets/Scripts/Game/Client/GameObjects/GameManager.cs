using Utils;
using Utils.Singletons;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Client
{
    public enum State
    {
        None,
        WaitForLogin,
        LoggedIn,
        WaitUntilLobbiesSceneLoaded,
        InLobbies,
        StartupGamePlay,
        WaitForGamePlay,
        InGamePlay,
    }



    public class GameManager : SingletonMonoBase<GameManager>
    {
        public State state { get; private set; }

        public event Action<State, State> OnStateChanged;

        private void Update()
        {
            Workflow();
        }

        void Workflow()
        {
            switch (state)
            {
                case State.None:
                    break;
                case State.WaitForLogin:
                    break;
                case State.LoggedIn:
                    {
                        ChangeState(State.WaitUntilLobbiesSceneLoaded);
                        StartCoroutine(LoadSceneAsync("Lobbies", State.InLobbies));
                    }
                    break;
                case State.WaitUntilLobbiesSceneLoaded:
                    break;
                case State.InLobbies:
                    break;
                case State.StartupGamePlay:
                    {
                        ChangeState(State.WaitForGamePlay);
                        StartCoroutine(SceneTransitionUtility.C_LoadAndSwitchAsync("InGame", null, null, () => ChangeState(State.InGamePlay)));
                    }
                    break;
                case State.WaitForGamePlay:
                    break;
                case State.InGamePlay:
                    break;
                default:
                    break;
            }
        }

        public void ChangeState(State newState)
        {
            if (state == newState)
                return;

            State old = state;
            state = newState;
            OnStateChanged?.Invoke(old, newState);
        }


        /// <summary>
        /// �� ��ȯ �� ���� ����
        /// </summary>
        private IEnumerator LoadSceneAsync(string sceneName, State targetState)
        {
            AsyncOperation loadOp = SceneManager.LoadSceneAsync(sceneName);

            while (!loadOp.isDone)
            {
                // loadOp.progress; // ui �� ������� ����ߵǸ� �̰� ����
                yield return null;
            }

            ChangeState(targetState);
        }
    }
}