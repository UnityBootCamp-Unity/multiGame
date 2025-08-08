using Game.MultiGamePlay;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using static Google.Protobuf.Reflection.UninterpretedOption.Types;
using System.Net;
using System.Reflection.PortableExecutable;
using System;

namespace Game.Server.MultiGameplay
{
    class MultiGameplayServiceImpl : MultiGamePlayService.MultiGamePlayServiceBase
    {
        public MultiGameplayServiceImpl(ILogger<MultiGameplayServiceImpl> logger, MultiGameServerManager serverManager)
        {
            _logger = logger;
            _serverManager = serverManager;
        }

        private readonly ILogger<MultiGameplayServiceImpl> _logger;
        private readonly MultiGameServerManager _serverManager;

        public override async Task<CreateAllocationResponse> CreateAllocation(CreateAllocationRequest request, ServerCallContext context)
        {
            try
            {
                var payload = JsonConvert.DeserializeObject<UnityMultiplayerGameServerHostingFacade.AllocationPayload>(request.Payload);

                _logger.LogInformation($"Request queue an allocation for {payload.lobbyId}");

                var (allocationId, href) = await UnityMultiplayerGameServerHostingFacade.CreateAllocationAsync
                    (
                        request.AllocationId,
                        request.BuildConfigurationId,
                        request.RegionId,
                        request.Restart,
                        payload
                    );

                if (!string.IsNullOrEmpty(allocationId))
                {
                    return new CreateAllocationResponse
                    {
                        Allocation = new AllocationInfo
                        {
                            AllocationId = "-1",
                            BuildConfigurationId = -1,
                            FleetId = "-1",
                            GamePort = 0,
                            IpAddress = "-1",
                            MachineId = 0,
                            IsReady = false,
                            RegionId = "-1",
                            ServerId = -1,
                        },
                        Href = string.Empty
                    };
                }

                var allocation = await UnityMultiplayerGameServerHostingFacade.GetAllocationAsync(allocationId);

                string matchId = allocationId; //Guid.NewGuid().ToString(); 일단 임시로 allocationId를 matchId 로 쓸거임.

                await _serverManager.RegisterMatchAsync(new MultiGameServerManager.MatchInfo
                {
                    MatchId = matchId,
                    ServerId = allocation.ServerId,
                    ServerIp = allocation.IpAddress,
                    ServerPort = allocation.Port,
                    LobbyId = payload.lobbyId,
                    ClientIds = payload.clientIds,
                });

                return new CreateAllocationResponse
                {
                    Allocation = new AllocationInfo
                    {
                        AllocationId = allocationId,
                        BuildConfigurationId = allocation.BuildConfigurationId,
                        FleetId = allocation.FleetId,
                        GamePort = allocation.Port,
                        IpAddress = allocation.IpAddress,
                        MachineId = allocation.MachineId,
                        IsReady = allocation.IsReady,
                        RegionId = allocation.Region,
                        ServerId = allocation.ServerId,
                    },
                    Href = string.Empty
                };
            }
            catch (Exception ex)
            {
                return new CreateAllocationResponse
                {
                    Allocation = new AllocationInfo
                    {
                        AllocationId = "-1",
                        BuildConfigurationId = -1,
                        FleetId = "-1",
                        GamePort = 0,
                        IpAddress = "-1",
                        MachineId = 0,
                        IsReady = false,
                        RegionId = "-1",
                        ServerId = -1,
                    },
                    Href = string.Empty
                };
            }
        }

        public override async Task<Empty> DeleteAllocation(DeleteAllocationRequest request, ServerCallContext context)
        {
            _logger.LogInformation($"Request deallocate {request.AllocationId}");

            try
            {
                await UnityMultiplayerGameServerHostingFacade.DeleteAllocationAsync(request.AllocationId);
                _logger.LogInformation($"Deallocated server {request.AllocationId}");
                return new Empty();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to deallocate server {request.AllocationId}, {ex.Message}");
                return new Empty();
            }
        }

        public override async Task<GetAllocationResponse> GetAllocation(GetAllocationRequest request, ServerCallContext context)
        {
            _logger.LogInformation($"Request get allocation {request.AllocationId}");
            try
            {
                var allocation = await UnityMultiplayerGameServerHostingFacade.GetAllocationAsync(request.AllocationId);

                if (allocation == null)
                {
                    throw new RpcException(new Status(StatusCode.NotFound, "Allocation not found"));
                }

                return new GetAllocationResponse
                {
                    Allocation = new AllocationInfo
                    {
                        AllocationId = allocation.AllocationId,
                        BuildConfigurationId = allocation.BuildConfigurationId,
                        FleetId = allocation.FleetId,
                        RegionId = allocation.Region,
                        ServerId = allocation.ServerId,
                        MachineId = allocation.MachineId,
                        IpAddress = allocation.IpAddress,
                        GamePort = allocation.Port,
                        IsReady = allocation.IsReady
                    }
                };
            }
            catch (Exception ex)
            {
                throw new RpcException(new Status(StatusCode.Internal, ex.Message));
            }
        }

        public override async Task<GetAllocationsResponse> GetAllocations(GetAllocationsRequest request, ServerCallContext context)
        {
            _logger.LogInformation($"Request get allocations.");
            try
            {
                var allocations = await UnityMultiplayerGameServerHostingFacade.GetAllocationsAsync
                    (
                        request.Age,
                        request.Limit > 0 ? request.Limit : null,
                        request.Offset > 0 ? request.Offset : null,
                        request.AllocationIds.Count > 0 ? request.AllocationIds : null
                    );

                List<AllocationInfo> allocationInfos = new List<AllocationInfo>();

                foreach ( var allocation in allocations)
                {
                    allocationInfos.Add(new AllocationInfo
                    {
                        AllocationId = allocation.AllocationId,
                        BuildConfigurationId = allocation.BuildConfigurationId,
                        FleetId = allocation.FleetId,
                        RegionId = allocation.Region,
                        ServerId = allocation.ServerId,
                        MachineId = allocation.MachineId,
                        IpAddress = allocation.IpAddress,
                        GamePort = allocation.Port,
                        IsReady = allocation.IsReady
                    });
                }

                return new GetAllocationsResponse
                {
                    Allocations = { allocationInfos },
                    Pagination = new PaginationInfo
                    {
                        Limit = request.Limit,
                        Offset = request.Offset,
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get allocations");
                throw new RpcException(new Status(StatusCode.Internal, ex.Message));
            }
        }

        public override async Task<Empty> UpdateGameplayStatus(UpdateGameplayStatusRequest request, ServerCallContext context)
        {
            _logger.LogInformation($"Request update gameplay status {request.AllocationId}.");

            try
            {
                var currentStatus = _serverManager.GetServerStatus(request.ServerId);

                await _serverManager.UpdateServerStatusAsync(request.ServerId, new MultiGameServerManager.ServerStatusInfo
                {
                    Status = request.Status,
                    TotalPlayers = currentStatus.TotalPlayers,
                    MaxPlayers = currentStatus.MaxPlayers,
                });

                return new Empty();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to update game play status of server {request.ServerId}");
                throw new RpcException(new Status(StatusCode.Internal, ex.Message));
            }
        }
    }
}
