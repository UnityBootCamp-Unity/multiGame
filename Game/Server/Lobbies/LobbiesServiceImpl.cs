using Game.Lobbies;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace Server.Lobbies
{
    class LobbiesServiceImpl : LobbiesService.LobbiesServiceBase
    {
        public LobbiesServiceImpl(ILogger<LobbiesServiceImpl> logger, LobbiesManager manager)
        {
            _logger = logger;
            _manager = manager;
        }

        ILogger<LobbiesServiceImpl> _logger;
        LobbiesManager _manager;

        public override async Task<CreateLobbyResponse> CreateLobby(CreateLobbyRequest request, ServerCallContext context)
        {
            try
            {
                int lobbyId = _manager.Create(request.HostClientId, request.MaxClient);
                return new CreateLobbyResponse
                {
                    LobbyId = lobbyId,
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create lobby");
                return new CreateLobbyResponse
                {
                    LobbyId = -1
                };
            }
        }

        public override async Task<JoinLobbyResponse> JoinLobby(JoinLobbyRequest request, ServerCallContext context)
        {
            RepeatedField<int> a = new RepeatedField<int> { new List<int>() };

            try
            {
                bool ok = _manager.Join(request.LobbyId, request.ClientId);


                if (ok)
                {
                    if (_manager.TryGetLobby(request.LobbyId, out var lobby))
                    {
                        await _manager.Broadcast(lobby.Id, new LobbyEvent
                        {
                            Type = LobbyEvent.Types.EventType.MemberJoin,
                            LobbyId = lobby.Id,
                            ClientId = request.ClientId,
                            Kv = { new Dictionary<string, string>() },
                            Ts = Timestamp.FromDateTime(DateTime.UtcNow),
                        });

                        var response = new JoinLobbyResponse
                        {
                            Success = ok,
                            LobbyInfo = _manager.ToLobbyInfo(lobby),
                            UserInLobbyInfos = { lobby.Members.Select(cid => _manager.ToUserInLobbyInfo(lobby, cid)) }
                        };

                        return response;
                    }
                }

                return new JoinLobbyResponse { Success = false };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Join lobby error");
                return new JoinLobbyResponse { Success = false };
            }
        }

        public override async Task<GetLobbyListResponse> GetLobbyList(Empty request, ServerCallContext context)
        {
            try
            {
                return new GetLobbyListResponse
                {
                    LobbyInfos = { _manager.All.Select(_manager.ToLobbyInfo)}
                };
            }
            catch (Exception ex) 
            {
                _logger.LogError(ex, "Get lobby list error");

                return new GetLobbyListResponse
                {
                    LobbyInfos = { new List<LobbyInfo>() }
                };
            }
        }

        public override async Task<LeaveLobbyResponse> LeaveLobby(LeaveLobbyRequest request, ServerCallContext context)
        {
            try
            {
                bool ok = _manager.Leave(request.LobbyId, request.ClientId);

                if (ok)
                {

                    if (_manager.TryGetLobby(request.LobbyId, out var lobby))
                    {
                        await _manager.Broadcast(lobby.Id, new LobbyEvent
                        {
                            Type = LobbyEvent.Types.EventType.MemberJoin,
                            LobbyId = lobby.Id,
                            ClientId = request.ClientId,
                            Kv = { new Dictionary<string, string>() },
                            Ts = Timestamp.FromDateTime(DateTime.UtcNow),
                        });

                        return new LeaveLobbyResponse
                        {
                            Success = true
                        };
                    }
                }

                return new LeaveLobbyResponse
                {
                    Success = false
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Leave lobby error.");

                return new LeaveLobbyResponse
                {
                    Success = false
                };
            }
        }

        public override async Task<SetLobbyCustomPropertiesResponse> SetLobbyCustomProperties(SetLobbyCustomPropertiesRequest request, ServerCallContext context)
        {
            try
            {
                bool ok = _manager.SetLobbyCustomProperties(request.LobbyId, request.Kv);

                if (ok)
                {
                    if (_manager.TryGetLobby(request.LobbyId, out var lobby))
                    {
                        await _manager.Broadcast(lobby.Id, new LobbyEvent
                        {
                            Type = LobbyEvent.Types.EventType.LobbyPropChanged,
                            LobbyId = lobby.Id,
                            ClientId = -1,
                            Kv = { request.Kv },
                            Ts = Timestamp.FromDateTime(DateTime.UtcNow),
                        });

                        return new SetLobbyCustomPropertiesResponse
                        {
                            Success = true
                        };
                    }
                }

                return new SetLobbyCustomPropertiesResponse
                {
                    Success = false
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Set lobby custom properties error.");

                return new SetLobbyCustomPropertiesResponse
                {
                    Success = false
                };
            }
        }

        public override async Task<SetUserInLobbyCustomPropertiesResponse> SetUserInLobbyCustomProperties(SetUserInLobbyCustomPropertiesRequest request, ServerCallContext context)
        {
            try
            {
                bool ok = _manager.SetUserCustomProperties(request.LobbyId, request.ClientId, request.Kv);

                if (ok)
                {
                    if (_manager.TryGetLobby(request.LobbyId, out var lobby))
                    {
                        await _manager.Broadcast(lobby.Id, new LobbyEvent
                        {
                            Type = LobbyEvent.Types.EventType.UserPropChanged,
                            LobbyId = lobby.Id,
                            ClientId =request.ClientId,
                            Kv = { request.Kv },
                            Ts = Timestamp.FromDateTime(DateTime.UtcNow),
                        });

                        return new SetUserInLobbyCustomPropertiesResponse
                        {
                            Success = true
                        };
                    }
                }

                return new SetUserInLobbyCustomPropertiesResponse
                {
                    Success = false
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Set user in lobby custom properties error.");

                return new SetUserInLobbyCustomPropertiesResponse
                {
                    Success = false
                };
            }
        }

        /// <summary>
        /// Streaming rpc 가 종료되면 Client 에 event 송신용 스트림이 닫히기때문에 무한루프를 걸어놔야함.
        /// </summary>
        public override async Task SubscribeLobby(SubscribeLobbyRequest request, IServerStreamWriter<LobbyEvent> responseStream, ServerCallContext context)
        {
            _manager.AddLobbyEventStream(request.LobbyId, responseStream);
            await Task.Delay(Timeout.Infinite, context.CancellationToken); // 무한대기
        }
    }
}
