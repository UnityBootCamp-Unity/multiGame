using Game.MultiGamePlay;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using static Game.Server.MultiGameplay.MultiGameServerManager;

namespace Game.Server.MultiGameplay
{
    class MultiGameServerManager
    {
        public MultiGameServerManager(ILogger<MultiGameServerManager> logger)
        {
            _logger = logger;
            _matches = new ConcurrentDictionary<string, MatchInfo>();
        }

        private readonly ConcurrentDictionary<string, MatchInfo> _matches; // k: MatchId, v: MatchInfo
        private readonly ConcurrentDictionary<long, ServerStatusInfo> _serverStatuses; // k: ServerId, v: ServerStatusInfo
        private ILogger<MultiGameServerManager> _logger;
        
        public Task RegisterMatchAsync(MatchInfo matchInfo)
        {
            _matches[matchInfo.MatchId] = matchInfo;
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

        public Task UpdateServerStatusAsync(long serverId, ServerStatusInfo serveStatus)
        {
            _serverStatuses[serverId] = serveStatus;
            return Task.CompletedTask;
        }

        public ServerStatusInfo GetServerStatus(long serverId)
        {
            return _serverStatuses[serverId];
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
