using Common;
using System;
using System.Collections.Generic;

namespace FDA
{
    public class QueueManager
    {
        public class QueueEventArgs : EventArgs
        {
            private readonly int _queueNumber;
            private readonly int _queueSize;

            public int QueueNumber { get => _queueNumber; }
            public int QueueSize { get => _queueSize; }

            public QueueEventArgs(int priority, int size)
            {
                _queueNumber = priority;
                _queueSize = size;
            }
        }

        private readonly int _priorityCount;
        public int PriorityCount { get => _priorityCount; }

        private readonly List<Dictionary<Guid, RequestGroup>> _queuedGroups;

        private readonly dynamic _owner;
        private readonly List<Queue<RequestGroup>> _queues;

        //private List<CircularBuffer<RequestGroup>> _recentDequeues;
        private RequestGroup superPriorityGroup;

        //private int _totalCount;

        public int TotalQueueCount { get => GetTotalCount(); }//_totalCount;}

        public delegate void QueueActivatedEventHandler(object sender, QueueCountEventArgs e);

        public event QueueActivatedEventHandler QueueActivated;

        public delegate void QueueEmptyHandler(object sender, QueueCountEventArgs e);

        public event QueueEmptyHandler QueueEmpty;

        public QueueManager(dynamic caller, int priorityCount)
        {
            _owner = caller;
            _queues = new List<Queue<RequestGroup>>();
            //_recentDequeues = new List<CircularBuffer<RequestGroup>>();
            _queuedGroups = new List<Dictionary<Guid, RequestGroup>>();
            //_queueCountHistory = new List<LinkedList<QueueHistoryPoint>>();
            _priorityCount = priorityCount;
            superPriorityGroup = null;

            Queue<RequestGroup> temp;
            int i;
            for (i = 0; i < PriorityCount; i++)
            {
                temp = new Queue<RequestGroup>();
                _queues.Add(temp);
                //_recentDequeues.Add(new CircularBuffer<RequestGroup>(5));
                _queuedGroups.Add(new Dictionary<Guid, RequestGroup>());
                // _queueCountHistory.Add(new LinkedList<QueueHistoryPoint>());

                // disable queue history collection
                //CollectHistoryPoint(i);
            }

            // disable queue history collection
            //_historyCleanupTmr = new Timer(HistoryTimerTick, null, _historyCleanupRate*1000,Timeout.Infinite);

            //_totalCount = 0;
        }

        /*
        private void HistoryTimerTick(object o)
        {
            QueueHistoryPoint oldestDataPoint;
            QueueHistoryPoint newestDataPoint;
            DateTime currentTime = Globals.FDANow();
            TimeSpan age;
            try
            {
                for (int Qn = 0; Qn < _queueCountHistory.Count; Qn++)
                {
                    // remove entries older than the maximum
                    if (_queueCountHistory[Qn].Count > 0)
                    {
                        do
                        {
                            oldestDataPoint = _queueCountHistory[Qn].First.Value;
                            age = currentTime.Subtract(oldestDataPoint.Timestamp);
                            if (age.TotalMinutes > _historyLength)
                            {
                                _queueCountHistory[Qn].RemoveFirst();
                            }
                        } while (_queueCountHistory[Qn].Count > 0 && age.TotalMinutes > _historyLength);

                        // if the newest (Last) data point is older than the minimum collection time, collect a data point
                        newestDataPoint = _queueCountHistory[Qn].Last.Value;
                        age = currentTime.Subtract(newestDataPoint.Timestamp);
                        if (age.TotalSeconds > _historyCleanupRate)
                        {
                            CollectHistoryPoint(Qn);
                        }
                    }
                    else
                    {
                        // no queue count history has been collected, so get a history datapoint
                        CollectHistoryPoint(Qn);
                    }
                }
            }
            catch (Exception ex)
            {
                Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Error occured while collected queue history point");
            }

            // reset the timer
            _historyCleanupTmr.Change(_historyCleanupRate*1000,Timeout.Infinite);
        }
        */

        /*
        private void CollectHistoryPoint(int Qn)
        {
            _queueCountHistory[Qn].AddLast(new QueueHistoryPoint(Globals.FDANow(), _queues[Qn].Count));
        }*/

        /*
    public QueueHistoryPoint[] GetHistory(int priority)
    {
        return _queueCountHistory[priority].ToArray();
    }
    */

        /*
    public struct QueueHistoryPoint
    {
        public readonly DateTime Timestamp;
        public readonly int Count;

        public QueueHistoryPoint(DateTime timestamp,int count)
        {
            Timestamp = timestamp;
            Count = count;
        }

        public string GetString()
        {
            return Timestamp.Ticks + ":" + Count;
        }
    }
    */

        /*
        public TimeSpan GetAverageQueueTime(int priority)
        {
            // make sure priority level exists
            if (priority >= _queues.Count)
                return TimeSpan.Zero;

            List<TimeSpan> recentQueueTimes = new List<TimeSpan>();

            if (_recentDequeues[priority].Count == 0)
                return TimeSpan.Zero;

            foreach (DataRequest req in _recentDequeues[priority])
            {
                if (req != null)
                    if (req.RequestTimestamp != null && req.QueuedTimestamp!=null)
                        recentQueueTimes.Add(req.RequestTimestamp.Subtract(req.QueuedTimestamp));
            }
            double doubleAverageTicks = recentQueueTimes.Average(timeSpan => timeSpan.Ticks);
            long longAverageTicks = Convert.ToInt64(doubleAverageTicks);

            return new TimeSpan(longAverageTicks);
        }

        public TimeSpan GetEstimatedBacklogtime(int priority)
        {
            // make sure priority level exists
            if (priority >= _queues.Count)
                return TimeSpan.Zero;

            TimeSpan avgQueueTime = GetAverageQueueTime(priority);

            return TimeSpan.FromTicks(avgQueueTime.Ticks * _queues[priority].Count);
        }
        */

        public UInt16[] GetQueueCounts()
        {
            UInt16[] counts = new UInt16[_queues.Count];

            for (int i = 0; i < _queues.Count; i++)
                counts[i] = (UInt16)_queues[i].Count;

            return counts;
        }

        internal int GetQueueCount(int priority)
        {
            if (priority < _queues.Count)
                return _queues[priority].Count;
            else
                return 0;
        }

        // queue the supplied request group (the group has a priority property that will determine which queue it goes into
        // if superPriority=true, this group will be the next one processed, regardless of priority
        public bool QueueTransactionGroup(RequestGroup requestGroup, bool superPriority = false)
        {
            int priority;
            try
            {
                if (requestGroup.Priority < _queues.Count && requestGroup.Priority >= 0)
                {
                    priority = requestGroup.Priority;
                }
                else
                {
                    priority = _queues.Count - 1; // set to the lowest priority if the supplied one was out of range
                    Globals.SystemManager.LogApplicationEvent(this, "Connection " + _owner.Description, "Requested priority " + requestGroup.Priority + " for group " + requestGroup.ID + " is not valid. Using priority " + priority + " instead");
                }

                //lock (_queues[priority])  // prevent other threads from accessing the queue while we're messing with it
                //{
                bool changeFromZero;
                // check if a request group with the same ID is not already in the queue
                if (!_queuedGroups[priority].ContainsKey(requestGroup.ID))
                {
                    requestGroup.QueuedTimestamp = Globals.FDANow();
                    lock (_queues[priority])
                    {
                        changeFromZero = (_queues[priority].Count == 0);
                        if (!superPriority)
                        {
                            _queues[priority].Enqueue(requestGroup);

                            _queuedGroups[priority].Add(requestGroup.ID, requestGroup);
                            UpdateCountProperty(priority);
                        }
                        else
                        {
                            if (superPriorityGroup == null)
                            {
                                superPriorityGroup = requestGroup;
                            }
                            else
                            {
                                Globals.SystemManager.LogApplicationEvent(this, "Queue manager for connection " + _owner, "Could not queue group " + requestGroup.ID + " as super priority, because there is already a group in the super priority slot");
                            }
                        }
                    }

                    //Globals.SystemManager.LogApplicationEvent(this, _owner.Description, "Queued request group: " + requestGroup.ID + " at priority " + priority);
                    QueueActivated?.Invoke(this, new QueueCountEventArgs(priority, changeFromZero));
                    return true;
                }
                else
                {  // request group is already in the queue, does it have the same destination?
                    RequestGroup queuedRequest = _queuedGroups[priority][requestGroup.ID];
                    if (queuedRequest.DestinationID == requestGroup.DestinationID)
                    {
                        Globals.SystemManager.LogApplicationEvent(this, "Connection " + _owner.Description, "Group " + requestGroup.ID + " is already queued at priority " + priority + ", with the same destination table.");
                        QueueActivated?.Invoke(this, new QueueCountEventArgs(priority, false));
                        return false;
                    }
                    else
                    {
                        Globals.SystemManager.LogApplicationEvent(this, "Connection " + _owner.Description, "Group " + requestGroup.ID + " is already queued at priority " + priority + ", but with a different destination. Appending this destination table to the existing request instead of adding the new request group to the queue");
                        queuedRequest.DestinationID += "," + requestGroup.DestinationID;
                        QueueActivated?.Invoke(this, new QueueCountEventArgs(priority, false));
                        return true;
                    }
                }
                //  }
            }
            catch (Exception ex)
            {
                Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Error occurred while queuing group " + requestGroup.ID + ", in connection " + _owner.ConnectionID);
                return false;
            }
        }

        private void UpdateCountProperty(int priority)
        {
            switch (priority)
            {
                case 0: _owner.Priority0Count = _queues[0].Count; break;
                case 1: _owner.Priority1Count = _queues[1].Count; break;
                case 2: _owner.Priority2Count = _queues[2].Count; break;
                case 3: _owner.Priority3Count = _queues[3].Count; break;
            }
        }

        public RequestGroup GetNextRequestGroup()
        {
            RequestGroup selectedRequestGroup = null;

            // first, check if there is any group in the superPriority slot
            // if there is, that's the next group to be processed, return it and clear the superpriority slot
            if (superPriorityGroup != null)
            {
                selectedRequestGroup = superPriorityGroup;
                superPriorityGroup = null;
                return selectedRequestGroup;
            }

            // no SuperPriority group, so find the highest priority queue that is not empty, and dequeue the first DataRequestGroup from it
            for (int i = 0; i < _queues.Count; i++)
            {
                if (_queues[i].Count > 0)
                {
                    lock (_queues[i])
                    {
                        selectedRequestGroup = _queues[i].Dequeue();
                    }
                    _queuedGroups[i].Remove(selectedRequestGroup.ID);

                    if (_queues[i].Count <= 0)
                    {
                        lock (_queues[i])
                        {
                            _queues[i].Clear();
                        }
                        QueueEmpty?.Invoke(this, new QueueCountEventArgs(i, false));
                    }

                    //_recentDequeues[i].AddItem(selectedRequestGroup);
                    //_totalCount--;
                    //CollectHistoryPoint(i);
                    UpdateCountProperty(i);
                    break;
                }
            }
            return selectedRequestGroup;
        }

        public void ClearQueues()
        {
            foreach (Queue<RequestGroup> queue in _queues)
            {
                lock (queue)
                {
                    queue.Clear();
                }
            }
        }

        private int GetTotalCount()
        {
            int count = 0;
            foreach (Queue<RequestGroup> queue in _queues)
            {
                count += queue.Count;
            }

            if (superPriorityGroup != null)
                count++;

            return count;
        }

        public class QueueCountEventArgs : EventArgs
        {
            public int QueueNumber;
            public bool ChangeFromZero;
            public DateTime Timestamp;

            public QueueCountEventArgs(int queueNumber, bool changefromZeroCount)
            {
                Timestamp = Globals.FDANow();
                QueueNumber = queueNumber;
                ChangeFromZero = changefromZeroCount;
            }
        }
    }
}