﻿using System.Collections.Generic;

namespace Game.Client.Network
{
    /// <summary>
    /// 의존성이 복잡하게 꼬일것 같을때, 
    /// 범용적으로 다른 객체들이 의존성문제없이 접근해서 해결할수있도록 
    /// 공유할 데이터를 칠판에 써놓는 형태를 Blackboard design 이라고 함
    /// </summary>
    public static class MultiplayMatchBlackboard
    {
        public static int lobbyId;
        public static bool isMaster;
        public static IEnumerable<int> clientIds;
    }
}
