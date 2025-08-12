using Unity.Netcode;
using UnityEngine;

namespace Game.Shared
{
    public class InGameManager : NetworkBehaviour
    {
        public enum State
        {
            WaitingForPlayers,
            CountdownToStart,
            Playing,
            Finished,
        }

        NetworkVariable<State> _state = new NetworkVariable<State>(
                value: State.WaitingForPlayers,
                readPerm: NetworkVariableReadPermission.Everyone,
                writePerm: NetworkVariableWritePermission.Server
            );

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            Debug.Log($"[{nameof(InGameManager)}] spawned");
        }
    }
}