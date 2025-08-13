using Game.Multigameplay.V1;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Game.Server.MultiGameplay
{
    class MultiGameServerManager
    {
        public MultiGameServerManager(ILogger<MultiGameServerManager> logger)
        {
            _logger = logger;
            _matches = new ConcurrentDictionary<string, MatchInfo>();
            _serverStatuses = new ConcurrentDictionary<int, ServerStatusInfo>();
            _allocationEventStreams = new ConcurrentDictionary<int, ConcurrentDictionary<int, IServerStreamWriter<AllocationEvent>>>();
        }

        private readonly ConcurrentDictionary<string, MatchInfo> _matches; // k: MatchId, v: MatchInfo
        private readonly ConcurrentDictionary<int, ServerStatusInfo> _serverStatuses; // k: LobbyId, v: ServerStatusInfo
        private readonly ConcurrentDictionary<int, ConcurrentDictionary<int, IServerStreamWriter<AllocationEvent>>> _allocationEventStreams; // k: lobbyId, v: (clientId, stream)
        private ILogger<MultiGameServerManager> _logger;


        public Task RegisterMatchAsync(MatchInfo matchInfo)
        {
            _matches[matchInfo.MatchId] = matchInfo;
            _serverStatuses[matchInfo.LobbyId] = new ServerStatusInfo
            {
                Status = GameplayStatus.Unknown,
            };
            _logger.LogInformation($"Registered match {matchInfo.MatchId}");
            return Task.CompletedTask;
        }

        public Task UnregisterMatchAsync(string matchId)
        {
            if (_matches.TryRemove(matchId, out var matchInfo))
            {
                _logger.LogInformation($"Unregistered match {matchId}");
            }

            return Task.CompletedTask;
        }

        public Task<MatchInfo> GetMatchAsync(string matchId)
        {
            _matches.TryGetValue(matchId, out var matchInfo);
            return Task.FromResult(matchInfo);
        }

        public Task<MatchInfo> GetMatchByLobbyIdAsync(int lobbyId)
        {
            var match = _matches.Values.FirstOrDefault(m => m.LobbyId == lobbyId);
            return Task.FromResult(match);
        }

        public int GetSubscriberCount(int lobbyId)
        {
            return _allocationEventStreams[lobbyId].Count;
        }

        public Task UpdateServerStatusAsync(int lobbyId, ServerStatusInfo serverStatus)
        {
            _serverStatuses[lobbyId] = serverStatus;
            return Task.CompletedTask;
        }

        public ServerStatusInfo GetServerStatus(int lobbyId)
        {
            return _serverStatuses[lobbyId];
        }

        public void AddAllocationEventStream(int lobbyId, int clientId, IServerStreamWriter<AllocationEvent> stream)
        {
            var lobbyStreams = _allocationEventStreams.GetOrAdd(lobbyId, _ => new ConcurrentDictionary<int, IServerStreamWriter<AllocationEvent>>());
            lobbyStreams[clientId] = stream;
            _logger.LogInformation($"Added allocation event stream for client {clientId} in lobby {lobbyId}");
        }

        public void RemoveAllocationEventStream(int lobbyId, int clientId)
        {
            if (_allocationEventStreams.TryGetValue(lobbyId, out var lobbyStreams))
            {
                lobbyStreams.TryRemove(clientId, out _);

                // 방금 접속종료된 클라이언트가 현재로비의 마지막 클라이언트였다면 로비의 ConcurrentDictonary 제거
                if (lobbyStreams.IsEmpty)
                {
                    _allocationEventStreams.TryRemove(lobbyId, out _);
                }
            }

            _logger.LogInformation($"Removed allocation event stream for client {clientId} in lobby {lobbyId}");
        }

        public async Task BroadcastAllocationEventAsync(int lobbyId, AllocationEvent e)
        {
            if (!_allocationEventStreams.TryGetValue(lobbyId, out var lobbyStreams))
                return;

            var copy = lobbyStreams.ToList();

            var tasks = copy.Select(async stream =>
            {
                try
                {
                    await stream.Value.WriteAsync(e);
                    return (stream, success: true);
                }
                catch
                {
                    return (stream, success: false);
                }
            });

            var results = await Task.WhenAll(tasks);

            var failedStreams = results
                .Where(r => !r.success)
                .Select(r => r.stream);

            foreach (var stream in failedStreams)
            {
                lobbyStreams.TryRemove(stream.Key, out _);
            }
        }



        public class MatchInfo
        {
            public string MatchId { get; set; }
            public long ServerId { get; set; }
            public string ServerIp { get; set; }
            public ulong ServerPort { get; set; }
            public int LobbyId { get; set; }
            public List<int> ClientIds { get; set; }
        }

        public class ServerStatusInfo
        {
            public GameplayStatus Status { get; set; }
            public int TotalPlayers { get; set; } // 현재 총 플레이어수
            public int MaxPlayers { get; set; }
        }
    }
}