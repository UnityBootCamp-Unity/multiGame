using Game.Chat;
using Game.Client.Network;
using System.Threading;
using Game.Lobbies;
using Grpc.Core;
using UnityEngine;
using System.Threading.Tasks;
using System;

namespace Game.Client.Controllers
{
    public class LobbiesController : MonoBehaviour
    {
        private LobbiesService.LobbiesServiceClient _lobbiesClient;
        private AsyncServerStreamingCall<LobbyEvent> _eventStream;
        private CancellationTokenSource _cts;

        private async void Start()
        {
            await InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            _cts = new CancellationTokenSource();
            _lobbiesClient = new LobbiesService.LobbiesServiceClient(GrpcConnection.channel);
        }

        public async Task<(bool success, int lobbyId, string message)> CreateLobbyAsync(int myClientId, int maxClient)
        {
            try
            {
                var response = await _lobbiesClient.CreateLobbyAsync(new CreateLobbyRequest
                {
                    HostClientId = myClientId,
                    MaxClient = maxClient
                });

                bool success = response.LobbyId >= 0;

                return (success, response.LobbyId, success ? "Lobby Created" : "Failed to create lobby.");
            }
            catch (Exception ex)
            {
                return (false, -1, ex.Message);
            }
        }

        /// <summary>
        /// Server streaming service 를 쓰기 때문에 무한루프형태로 계속 스트림을읽어야함
        /// </summary>
        public async Task SubscribeLobbyAsync(int lobbyId, int clientId, Action<LobbyEvent> onEvent)
        {
            _eventStream = _lobbiesClient.SubscribeLobby(new SubscribeLobbyRequest
            {
                LobbyId = lobbyId,
                ClientId = clientId,
            }, cancellationToken: _cts.Token);

            _ = Task.Run(async () =>
            {
                try
                {
                    await foreach (var lobbyEvent in _eventStream.ResponseStream.ReadAllAsync(_cts.Token))
                        onEvent?.Invoke(lobbyEvent);
                }
                catch
                {
                    // 스트리밍 종료됨
                }
            });
        }
    }
}