using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Game.Client.GameObjects.Characters;
using Game.Client.Models;
using Game.Client.Network;
using Game.MultiGamePlay;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Client.Controllers
{
    public class MultigameplayController : MonoBehaviour
    {
        public AllocationInfo currentAllocation { get; private set; }
        public string currentMatchId { get; private set; }

        public GameplayStatus currentStatus { get; private set; }

        [SerializeField] Player _playerPrefab;
        private Player _player;
        private MultiGamePlayService.MultiGamePlayServiceClient _multiGameplayClient;
        private MultiplaySettings _multiplaySettings;

        public event Action<AllocationInfo> onMultiGameplayServerReady;
        public event Action<string> onMultiGameplayServerAllocationError;
        public event Action<GameplayStatus> onStatusChanged;

        public record AllocationPayload
        {
            public int lobbyId { get; set; }
            public List<int> clientIds { get; set; }
            public Dictionary<string, string> gameSettiings { get; set; }
        }

        private async void Start()
        {
            await InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            _multiGameplayClient = new MultiGamePlayService.MultiGamePlayServiceClient(GrpcConnection.channel);
#if TEST_ALPHA
            _multiplaySettings = Resources.Load<MultiplaySettings>("Network/MultiplaySettings_Alpha");
#elif TEST_BETA
            _multiplaySettings = Resources.Load<MultiplaySettings>("Network/MultiplaySettings_Beta");
#else
            _multiplaySettings = Resources.Load<MultiplaySettings>("Network/MultiplaySettings_Release");
#endif
            var allocateResponse = await AllocateAsync();

            if (!allocateResponse.success)
                throw new Exception(); // TODO : 재시도 및 예외처리, 로비복귀 등 해야함


            await UpdateStatusAsync(GameplayStatus.Starting);
            await Awaitable.MainThreadAsync();
            _player = Instantiate(_playerPrefab);
            _player.onStatusChanged += OnPlayerStatusChanged;
        }

        private async void OnPlayerStatusChanged(PlayerStatus before, PlayerStatus after)
        {
            // TODO : 
            // 일단 클라이언트가 게임상태를 직접 변경하는 컨셉인데..
            // 플레이어 상태값을 서버에 주면, 서버가 알아서 상태를 변경하고 통지하는 Server-streaming 으로 바꿀필요가있음.

            // ready 됨
            if (!before.isReady && after.isReady)
            {
                await UpdateStatusAsync(GameplayStatus.Ready);
            }
            // 끝남
            if (!before.isFinished && after.isFinished)
            {
                await UpdateStatusAsync(GameplayStatus.Ending);
                await DeallocateAsync();
                await UpdateStatusAsync(GameplayStatus.Terminated);
                SceneManager.LoadScene("Lobbies");
            }
        }

        

        public async Task<(bool success, string message, string allocation)> AllocateAsync()
        {
            AllocationPayload payload = new AllocationPayload
            {
                lobbyId = MultiplayMatchBlackboard.lobbyId,
                clientIds = new List<int>(MultiplayMatchBlackboard.clientIds),
                gameSettiings = new Dictionary<string, string>()
                {
                    // TODO : setting 에 커스텀세팅 데이터 추가하고 이거 초기화할때 써야함.
                }
            };

            try
            {
                var payloadJson = JsonConvert.SerializeObject(payload);

                var response = await _multiGameplayClient.CreateAllocationAsync(new CreateAllocationRequest
                {
                    AllocationId = Guid.NewGuid().ToString(),
                    BuildConfigurationId = _multiplaySettings.buildConfigurationId,
                    RegionId = _multiplaySettings.regionId,
                    Restart = false,
                    Payload = payloadJson
                });

                if (response.Allocation.ServerId > 0)
                {
                    currentAllocation = response.Allocation;
                    currentMatchId = response.Allocation.AllocationId;

                    onMultiGameplayServerReady?.Invoke(currentAllocation);
                    return (true, "Allocated game server.", currentAllocation.AllocationId);
                }
                else
                {
                    string message = "Failed to allocate game server.";
                    onMultiGameplayServerAllocationError?.Invoke(message);
                    return (false, message, null);
                }
            }
            catch (Exception ex)
            {
                string message = $"Failed to allocate game server.\n{ex.Message}.";
                onMultiGameplayServerAllocationError?.Invoke(message);
                return (false, message, null);
            }
        }

        public async Task<bool> DeallocateAsync()
        {
            if (string.IsNullOrEmpty(currentMatchId))
                return true;

            try
            {
                await _multiGameplayClient.DeleteAllocationAsync(new DeleteAllocationRequest
                {
                    AllocationId = currentMatchId,
                });
                ClearAllocationInfo();
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to deallocate server {currentMatchId}, {ex.Message}");
                return false;
            }
        }

        public async Task<(bool success, AllocationInfo allocation)> GetAllocation(string allocationId)
        {
            try
            {
                var response = await _multiGameplayClient.GetAllocationAsync(new GetAllocationRequest
                {
                    AllocationId = allocationId,
                });

                if (response.Allocation.ServerId > 0)
                {
                    currentAllocation = response.Allocation;
                    currentMatchId = response.Allocation.AllocationId;
                    return (true, currentAllocation);
                }
                else
                {
                    ClearAllocationInfo();
                    return (false, null);
                }
            }
            catch
            {
                ClearAllocationInfo();
                return (false, null);
            }
        }

        public async Task<(bool success, IList<AllocationInfo> allocations)> GetAllocations(int age, int limit, int offset, IEnumerable<string> allocationIds)
        {
            try
            {
                var response = await _multiGameplayClient.GetAllocationsAsync(new GetAllocationsRequest
                {
                    Age = $"{age}h",
                    Limit = limit,
                    Offset = offset,
                    AllocationIds = { allocationIds }
                });

                return (true, response.Allocations);
            }
            catch (Exception ex)
            {
                return (false, null);
            }
        }

        private void ClearAllocationInfo()
        {
            currentAllocation = null;
            currentMatchId = null;
        }

        public async Task<(bool success, GameplayStatus status)> UpdateStatusAsync(GameplayStatus newStatus)
        {
            try
            {
                await _multiGameplayClient.UpdateGameplayStatusAsync(new UpdateGameplayStatusRequest
                {
                    AllocationId = currentMatchId,
                    ServerId = currentAllocation.ServerId,
                    Status = newStatus
                });

                currentStatus = newStatus;
                onStatusChanged?.Invoke(newStatus);
                return (true, newStatus);
            }
            catch (Exception ex)
            {
                return (false, GameplayStatus.Unknown);
            }
        }
    }
}
