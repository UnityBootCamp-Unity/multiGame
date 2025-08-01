namespace Server.Network
{
    /// <summary>
    /// 고유 Id 를 부여하기위한 생성기
    /// </summary>
    class IdGenerator
    {
        public IdGenerator(int maxClients = 100)
        {
             _idSet = new HashSet<int>(maxClients);
            _availableIdQueue = new Queue<int>(maxClients);

            for (int i = 0; i < maxClients; i++)
            {
                _availableIdQueue.Enqueue(i);
            }
        }

        readonly HashSet<int> _idSet; // 현재 연결된 세션들의 id 집합
        readonly Queue<int> _availableIdQueue; // id 를 재사용하기위한 큐

        /// <summary>
        /// ID 를 부여하기위함
        /// </summary>
        /// <returns> 할당되는 id </returns>
        public int AssignId()
        {
            if (_availableIdQueue.Count > 0)
            {
                int id = _availableIdQueue.Dequeue();
                _idSet.Add(id);
                return id;
            }
            else
            {
                Console.WriteLine($"[{nameof(IdGenerator)}] : Server is fulled... reached to max clients");
                return -1;
            }
        }

        /// <summary>
        /// ID 를 반납하기 위함
        /// </summary>
        /// <param name="Id"> 반납할 id </param>
        public void ReleaseId(int Id)
        {
            if (_idSet.Remove(Id))
            {
                _availableIdQueue.Enqueue(Id);
            }
            else
            {
                // TODO : 예외
            }
        }
    }
}
