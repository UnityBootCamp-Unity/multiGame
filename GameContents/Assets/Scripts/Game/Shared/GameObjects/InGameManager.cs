using System.Collections.Generic;
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

        public NetworkVariable<State> state = new NetworkVariable<State>(
                value: State.WaitingForPlayers,
                readPerm: NetworkVariableReadPermission.Everyone,
                writePerm: NetworkVariableWritePermission.Server
            );

        private Dictionary<ulong, NetworkObject> _players = new Dictionary<ulong, NetworkObject>();

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            Debug.Log($"[{nameof(InGameManager)}] spawned");
        }

        public void RegisterPlayer(ulong objectId)
        {
            if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(objectId, out NetworkObject networkObject))
            {
                _players[networkObject.OwnerClientId] = networkObject;

                if (IsServer)
                {
                    CheckAllPlayerReady();
                }
            }
            else
            {
                // �÷��̾� ĳ���� ���� �ȵ� ����
            }
        }

        private  void CheckAllPlayerReady()
        {
            int count = _players.Count;

            // TODO : Allocation �����͸� Multiplay blackboard ��� �о �� ����� �غ�Ǿ���ϴ��� ���� ���Ѵ���
            // ���� ���� ����

            if (count == 4)
            {
                state.Value = State.CountdownToStart;
            }
        }
    }
}