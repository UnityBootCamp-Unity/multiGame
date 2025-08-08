using Game.Client.Network;
using Unity.Multiplayer;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Shared.GameObjects
{
    public class InGameBootstrap : MonoBehaviour
    {
        private void Start()
        {
            Init();
        }

        public void Init()
        {
            MultiplayerRoleFlags roleflags = MultiplayerRolesManager.ActiveMultiplayerRoleMask;

            bool isServer = roleflags.HasFlag(MultiplayerRoleFlags.Server);
            bool isClient = roleflags.HasFlag(MultiplayerRoleFlags.Client);

            if (isServer)
            {
                Debug.Log($"[{nameof(Bootstrap)}] Role : Server (Dedicated server)");
                SceneManager.LoadScene("Server", LoadSceneMode.Additive);
            }
            else if (isClient)
            {
                Debug.Log($"[{nameof(Bootstrap)}] Role : Client");
                SceneManager.LoadScene("Client", LoadSceneMode.Additive);
            }
            else
            {
#if UNITY_EDITOR
                EditorApplication.ExitPlaymode();
#else
                Application.Quit();
#endif
                return;
            }
        }
    }
}