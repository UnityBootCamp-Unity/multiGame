using Game.Chat;
using Game.Client.Network;
using System.Threading;
using Game.Lobbies;
using Grpc.Core;
using UnityEngine;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using Google.Protobuf.WellKnownTypes;
using System.Linq;
using Game.NetworkContracts;
using static Game.Client.LobbiesConstants;

namespace Game.Client.Controllers
{
    public class LobbiesController : MonoBehaviour
    {
        private LobbiesService.LobbiesServiceClient _lobbiesClient;
        private AsyncServerStreamingCall<LobbyEvent> _eventStream;
        private CancellationTokenSource _cts;

        /// <summary>
        /// 클라이언트에서 Lobby 정보 캐싱용
        /// </summary>
        public class LocalLobby
        {
            public bool empty
            {
                get => lobbyId < 0;
            }

            public int lobbyId = -1;
            public int hostClientId = -1;
            public int maxClient = -1;
            public int numClient = -1;
            public Dictionary<string, string> customProperties = new Dictionary<string, string>();
            public Dictionary<int, Dictionary<string, string>> userCustomProperties = new Dictionary<int, Dictionary<string, string>>();

            public void ApplyLobbyInfo(LobbyInfo info)
            {
                this.lobbyId = info.LobbyId;
                this.hostClientId = info.HostClientId;
                this.maxClient = info.MaxClient;
                this.numClient = info.NumClient;

                foreach (var item in info.CustomProperties)
                {
                    customProperties[item.Key] = item.Value;
                }
            }
            public void Clear()
            {
                lobbyId = -1;
                hostClientId = -1;
                maxClient = -1;
                numClient = -1;
                customProperties.Clear();
                userCustomProperties.Clear();
            }
        }

        

        private LocalLobby _cachedLobby;

        public event Action<int> onMemberJoin;
        public event Action<int> onMemberLeft;
        public event Action<IDictionary<string, string>> onLobbyPropsChanged;
        public event Action<int, IDictionary<string, string>> onUserPropsChanged;

        private async void Start()
        {
            await InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            _cachedLobby = new LocalLobby();
            _cts = new CancellationTokenSource();
            _lobbiesClient = new LobbiesService.LobbiesServiceClient(GrpcConnection.channel);
        }

        public async Task<(bool success, string message)> CreateLobbyAsync(int maxClient)
        {
            try
            {
                var response = await _lobbiesClient.CreateLobbyAsync(new CreateLobbyRequest
                {
                    HostClientId = GrpcConnection.clientInfo.ClientId,
                    MaxClient = maxClient
                });

                if (response.Success)
                {
                    _cachedLobby.ApplyLobbyInfo(response.LobbyInfo);
                    SubscribeLobby(HandleLobbyEvents);
                    await SetUserCustomPropertiesAsync(GrpcConnection.clientInfo.ClientId, new Dictionary<string, string>
                    {
                        { IS_MASTER, bool.TrueString }, // 내가 방장
                        { IS_READY, bool.TrueString } // 방장은 항상 준비완료
                    });
                }

                return (response.Success, response.Success ? "Lobby Created" : "Failed to create lobby.");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public async Task<(bool success, string message, IList<UserInLobbyInfo> members)> JoinLobbyAsync(int lobbyId)
        {
            try
            {
                var response = await _lobbiesClient.JoinLobbyAsync(new JoinLobbyRequest
                {
                    LobbyId = lobbyId,
                    ClientId = GrpcConnection.clientInfo.ClientId,
                });

                if (response.Success)
                {
                    _cachedLobby.ApplyLobbyInfo(response.LobbyInfo);
                    SubscribeLobby(HandleLobbyEvents);
                    await SetUserCustomPropertiesAsync(GrpcConnection.clientInfo.ClientId, new Dictionary<string, string>
                    {
                        { IS_MASTER, bool.FalseString }, // 난 방장 아님
                        { IS_READY, bool.FalseString } // 일단 준비 안함
                    });
                }

                return (response.Success, response.Success ? "Lobby Joined" : "Failed to join lobby.", response.UserInLobbyInfos);
            }
            catch (Exception ex)
            {
                return (false, ex.Message, null);
            }
        }

        public async Task<(bool success, string message)> LeaveLobbyAsync()
        {
            try
            {
                bool isMaster = bool.Parse(_cachedLobby.userCustomProperties[GrpcConnection.clientInfo.ClientId][IS_MASTER]);

                LeaveLobbyResponse response = await _lobbiesClient.LeaveLobbyAsync(new LeaveLobbyRequest
                {
                    LobbyId = _cachedLobby.lobbyId,
                    ClientId = GrpcConnection.clientInfo.ClientId,
                });

                if (response.Success)
                {
                    if (isMaster)
                    {
                        if (_cachedLobby.userCustomProperties.Keys.Count > 2)
                        {
                            int nextMasterId = _cachedLobby.userCustomProperties.Keys.First(clientId => clientId != GrpcConnection.clientInfo.ClientId);

                            _ = ChangeMasterAsync(nextMasterId);
                        }
                    }

                    _cachedLobby.Clear();
                    _cts.Cancel();
                    _eventStream?.Dispose();
                    _cts = new CancellationTokenSource();
                    return (true, "Left lobby.");
                }
                else
                {
                    return (false, "Failed to leave lobby.");
                }
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public async Task ChangeMasterAsync(int nextMasterClientId)
        {
            int clinetId = GrpcConnection.clientInfo.ClientId;
            bool IsMaster = bool.Parse(_cachedLobby.userCustomProperties[clinetId][IS_MASTER]);

            if (IsMaster == false)
                throw new Exception($"{clinetId} is not a master.");

            // TODO : 서버데이터 검증 부분 및 각 gRPC Response 예외처리

            await SetUserCustomPropertiesAsync(clinetId, new Dictionary<string, string>
            {
                { IS_MASTER, bool.FalseString }
            });

            await SetUserCustomPropertiesAsync(nextMasterClientId, new Dictionary<string, string>
            {
                { IS_MASTER, bool.TrueString }
            });
        }

        public async Task<(bool success, IList<LobbyInfo>)> GetLobbyListAsync()
        {
            try
            {
                var response = await _lobbiesClient.GetLobbyListAsync(new Empty());
                var lobbyList = response.LobbyInfos.ToList();
                return (true, lobbyList);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to get lobbylist {ex.Message}");
                return (false, null);
            }
        }

        public async Task<(bool success, string message)> SetLobbyCustomPropertiesAsync(IDictionary<string, string> properties)
        {
            try
            {
                var response = await _lobbiesClient.SetLobbyCustomPropertiesAsync(new SetLobbyCustomPropertiesRequest
                {
                    LobbyId = _cachedLobby.lobbyId,
                    Kv = { properties }
                });

                return (response.Success, response.Success ? "Changed lobby properties" : "Failed to change lobby properties");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public async Task<(bool success, string message)> SetUserCustomPropertiesAsync(int targetClientId, IDictionary<string, string> properties)
        {
            try
            {
                var response = await _lobbiesClient.SetUserInLobbyCustomPropertiesAsync(new SetUserInLobbyCustomPropertiesRequest
                {
                    LobbyId = _cachedLobby.lobbyId,
                    ClientId = targetClientId,
                    Kv = { properties }
                });

                return (response.Success, response.Success ? "Changed user properties" : "Failed to change lobby properties");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        /// <summary>
        /// Server streaming service 를 쓰기 때문에 무한루프형태로 계속 스트림을읽어야함
        /// </summary>
        public void SubscribeLobby(Action<LobbyEvent> onEvent)
        {
            // TODO : lobbyid 및 clientid 예외처리

            _eventStream = _lobbiesClient.SubscribeLobby(new SubscribeLobbyRequest
            {
                LobbyId = _cachedLobby.lobbyId,
                ClientId = GrpcConnection.clientInfo.ClientId,
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
            }, cancellationToken: _cts.Token);
        }

        async void HandleLobbyEvents(LobbyEvent e)
        {
            switch (e.Type)
            {
                case LobbyEvent.Types.EventType.MemberJoin:
                    {
                        _cachedLobby.userCustomProperties[e.ClientId] = new Dictionary<string, string>();

                        _cachedLobby.numClient++;
                        onMemberJoin?.Invoke(e.ClientId);
                    }
                    break;
                case LobbyEvent.Types.EventType.MemberLeft:
                    {
                        _cachedLobby.userCustomProperties.Remove(e.ClientId);

                        _cachedLobby.numClient--;
                        onMemberLeft?.Invoke(e.ClientId);
                    }
                    
                    break;
                case LobbyEvent.Types.EventType.LobbyPropChanged:
                    {
                        foreach (var (k, v) in e.Kv)
                        {
                            _cachedLobby.customProperties[k] = v;
                        }

                        onLobbyPropsChanged?.Invoke(e.Kv);
                    }
                    break;
                case LobbyEvent.Types.EventType.UserPropChanged:
                    {
                        foreach (var (k, v) in e.Kv)
                        {
                            _cachedLobby.userCustomProperties[e.ClientId][k] = v;
                        }

                        onUserPropsChanged?.Invoke(e.ClientId, e.Kv);
                    }
                    break;
                default:
                    throw new ArgumentException($"{e.Type} event does not implemented.");
            }
        }
    }
}