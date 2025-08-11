using Game.Client.GameObjects.Characters;
using Game.Client.Models;
using Game.Client.Network;
using Game.Multigameplay.V1;
using Grpc.Core;
using Newtonsoft.Json;
using NUnit.Framework.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
        private AsyncServerStreamingCall<AllocationEvent> _eventStream;
        private CancellationTokenSource _cts;

        public event Action<AllocationInfo> onAllocationCreated;
        public event Action<AllocationInfo> onAllocationReady;
        public event Action onAllocationDeleted;
        public event Action<string> onAllocationFailed;
        public event Action<GameplayStatus> onAllocationGameplayStatusChanged;

        public record AllocationPayload
        {
            public int lobbyId { get; set; }
            public List<int> clientIds { get; set; }
            public Dictionary<string, string> gameSettings { get; set; }
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

            SubscribeToAllocationEvents();

            // TODO : 모든 Client 가 구독 완료될때까지 기다림

            if (MultiplayMatchBlackboard.isMaster)
            {
                var allocateResponse = await AllocateAsync(); // 클라이언트가 직접 Allocation 요청하는로직보다는 서버가 상태 확인하면서 알아서 처리하는게 보안상 나음

                if (!allocateResponse.success)
                    throw new Exception(allocateResponse.message); // TODO : 재시도 및 예외처리, 로비복귀 등 해야함

                await UpdateStatusAsync(GameplayStatus.Starting);
            }

        }

        private async void OnApplicationQuit()
        {
            await _multiGameplayClient.DeleteAllocationAsync(new DeleteAllocationRequest
            {
                AllocationId = currentMatchId,
            });
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

        public async Task<(bool success, string message, string allocationId)> AllocateAsync()
        {
            AllocationPayload payload = new AllocationPayload
            {
                lobbyId = MultiplayMatchBlackboard.lobbyId,
                clientIds = new List<int>(MultiplayMatchBlackboard.clientIds),
                gameSettings = new Dictionary<string, string>()
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

                    return (true, "Allocated game server.", currentAllocation.AllocationId);
                }
                else
                {
                    string message = "Failed to allocate game server.";
                    return (false, message, null);
                }
            }
            catch (Exception ex)
            {
                string message = $"Failed to allocate game server\n{ex.Message}.";
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

        public async Task<(bool success, AllocationInfo allocatiion)> GetAllocation(string allocationId)
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
                    LobbyId = MultiplayMatchBlackboard.lobbyId,
                    Status = newStatus
                });

                currentStatus = newStatus;
                return (true, newStatus);
            }
            catch (Exception ex)
            {
                return (false, GameplayStatus.Unknown);
            }
        }

        public void SubscribeToAllocationEvents()
        {
            try
            {
                _cts = new CancellationTokenSource();

                _eventStream = _multiGameplayClient.SubscribeAllocationEvents(new SubscribeAllocationEventsRequest
                {
                    ClientId = GrpcConnection.clientInfo.ClientId,
                    LobbyId = MultiplayMatchBlackboard.lobbyId,
                }, cancellationToken: _cts.Token);

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await foreach (var e in _eventStream.ResponseStream.ReadAllAsync(_cts.Token))
                        {
                            HandleAllocationEvent(e);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.Log("Error in allocationevent stream.");
                    }
                });

                Debug.Log($"Subscribed to allocation event for lobby {MultiplayMatchBlackboard.lobbyId}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to subscribe to allocation event. {ex}");
            }
        }

        /// <summary>
        /// 이 이벤트는 메인쓰레드에서 호출되지않음.
        /// 유니티 로직은 동기화Context 를 통해 Send/Post 하거나 Unity Awaitable 로 동기화 해주어야함.
        /// </summary>
        /// <param name="e"></param>
        async void HandleAllocationEvent(AllocationEvent e)
        {
            await Awaitable.MainThreadAsync();
            Debug.Log($"AllocationEvent occurred: {e.Type}");

            switch (e.Type)
            {
                case AllocationEvent.Types.EventType.AllocationCreated:
                    {
                        onAllocationCreated?.Invoke(e.Allocation);
                    }
                    break;
                case AllocationEvent.Types.EventType.AllocationReady:
                    {
                        OnAllocationReady();
                        onAllocationReady?.Invoke(e.Allocation);
                    }
                    break;
                case AllocationEvent.Types.EventType.AllocationDeleted:
                    {
                        onAllocationDeleted?.Invoke();
                    }
                    break;
                case AllocationEvent.Types.EventType.AllocationFailed:
                    {
                        onAllocationFailed?.Invoke(e.ErrorMessage);
                    }
                    break;
                case AllocationEvent.Types.EventType.AllocationStatusChanged:
                    {
                        onAllocationGameplayStatusChanged?.Invoke(e.NewStatus);
                    }
                    break;
                default:
                    break;
            }
        }

        async void OnAllocationReady()
        {
            await Awaitable.MainThreadAsync();
            _player = Instantiate(_playerPrefab);
            _player.onStatusChanged += OnPlayerStatusChanged;
        }
    }
}