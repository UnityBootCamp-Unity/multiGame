using Game.Singletons;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game
{
    public enum State
    {
        None,
        WaitForLogin,
        LoggedIn,
        WaitUntilLobbiesSceneLoaded,
        WaitForJoinLobby,
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
                        StartCoroutine(LoadSceneAsync("Lobbies", State.WaitForJoinLobby));
                    }
                    break;
                case State.WaitUntilLobbiesSceneLoaded:
                    break;
                case State.WaitForJoinLobby:
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