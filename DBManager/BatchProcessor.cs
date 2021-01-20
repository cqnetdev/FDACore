using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Common;
using System.Threading;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Diagnostics;

namespace FDA
{
    public class CacheManager : IDisposable
    {
        private Dictionary<string, Cache> _caches;

        private int _cacheTimeout;
        private int _cacheLimit;

        public int CacheTimeout { get { return _cacheTimeout; } set { _cacheTimeout = value; UpdateCacheSettings(); } }
        public int CacheLimit { get { return _cacheLimit; } set { _cacheLimit = value; UpdateCacheSettings(); } }

        //private Timer _cacheCheckTimer;

        public delegate void CacheFlushHandler(object sender, CacheFlushEventArgs e);
        public event CacheFlushHandler CacheFlush;

        public CacheManager(int sizeLimit,int timeout)
        {
            CacheLimit = sizeLimit;
            CacheTimeout = timeout;
            _caches = new Dictionary<string, Cache>();
           // _cacheCheckTimer = new Timer(CacheTimerOnTick, null, 1000, 1000);
        }

        public void CacheDataPoint(string destination,Tag tag)
        {
            // create a new cache for the destination if one doesn't already exist
            if (!_caches.ContainsKey(destination))
            {
                lock (_caches)
                {
                    Cache newCache = new Cache(destination, CacheTimeout, CacheLimit);
                    newCache.CacheMaxSizeReached += HandleCacheMaxSize;
                    newCache.CacheTimeout += HandleCacheTimeout;
                    _caches.Add(destination, newCache);
                }
            }

            // add the data to the cache for the destination table
            lock (_caches[destination])
            {
                _caches[destination].CacheData(tag);
            }
        }

        private void UpdateCacheSettings()
        {
            if (_caches != null)
            {
                foreach (Cache cache in _caches.Values)
                {
                    cache.Timeout = _cacheTimeout;
                    cache.MaxSize = _cacheLimit;
                }
            }
        }

        private void HandleCacheMaxSize(object sender, EventArgs e)
        {
            Cache cache = (Cache)sender;
            Globals.SystemManager.LogApplicationEvent(this, "", "cache count for '" + cache.Destination + "' of " + cache.Count + " reached the limit of " + CacheLimit + ", flushing " + cache.Count + " data points to the database",false,true);
            string sql = cache.GetBatch();
            if (sql != null)
            {
                // raise an event for the dbmananger to write the batch
                CacheFlush?.Invoke(this, new CacheFlushEventArgs(sql));
            }
        }

        private void HandleCacheTimeout(object sender, EventArgs e)
        {
            Cache cache = (Cache)sender;
            Globals.SystemManager.LogApplicationEvent(this, "", "cache age of '" + cache.Destination + "' of " + cache.Age + " exceeds the limit of " + CacheTimeout + ", flushing " + cache.Count + " data points to the database",false,true);

            lock (cache)
            {
                string sql = cache.GetBatch();
                if (sql != null)
                {
                    // raise an event for the dbmanager to write the batch
                    CacheFlush?.Invoke(this, new CacheFlushEventArgs(sql));
                }
            }
        }


        public List<string> FlushAll()
        {
            List<string> BatchList = new List<string>();


            foreach (Cache cache in _caches.Values)
            {
                if (cache.Count > 0)
                {
                    lock (cache)
                    {
                        BatchList.Add(cache.GetBatch());
                    }
                }
            }

            return BatchList;
        }

        public void Dispose()
        { 
            
            //_cacheCheckTimer.Dispose();
            //_cacheCheckTimer = null;
        }

        public class CacheFlushEventArgs : EventArgs
        {
            public string Query;

            public CacheFlushEventArgs(string query)
            {
                Query = query; 
            }
        };

    }
    

    internal class Cache : IDisposable
    {
        public StringBuilder SQL;
        public string Destination;

        public long Age = 0;// { get { if (_stopwatch != null) return _stopwatch.ElapsedMilliseconds; else return 0; } }
        public int Count;
 
        private Stopwatch _stopwatch;
        private Timer _ageTimer;

        public int Timeout;
        public int MaxSize;

        public delegate void TimeoutHandler(object sender, EventArgs e);
        public event TimeoutHandler CacheTimeout;

        public delegate void MaxSizeHandler(object sender, EventArgs e);
        public event MaxSizeHandler CacheMaxSizeReached;


        public Cache(string destination,int timeout,int maxsize)
        {
            Destination = destination;
            SQL = new StringBuilder();
            lock (SQL)
            {
                resetBatch();
            }

            Timeout = timeout;
            MaxSize = maxsize;

            _ageTimer = new Timer(this.Ontimeout, null, System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
            _stopwatch = new Stopwatch();
        }

        private void Ontimeout(object o)
        {
            Age = _stopwatch.ElapsedMilliseconds;
            CacheTimeout?.Invoke(this, new EventArgs());
            _ageTimer.Change(System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
        }

        public void CacheData(Tag tag)
        {
            //string sql = SQL;

            // going from empty to not empty, start the cache age timer
            if (Count == 0)
            {              
                _ageTimer.Change(Timeout, System.Threading.Timeout.Infinite);
                _stopwatch.Start();
            }

            if (tag.TagID != Guid.Empty)
            {
                lock (SQL)
                {
                    if (Count > 0)
                        SQL.Append(",");

                    SQL.Append("('");
                    SQL.Append(tag.TagID);
                    SQL.Append("','");
                    SQL.Append(Helpers.FormatDateTime(tag.Timestamp));
                    SQL.Append("',");
                    SQL.Append(tag.Value);
                    SQL.Append(",");
                    SQL.Append(tag.Quality);
                    SQL.Append(")");
                }

                    Count++;
            }
          
            if (Count >= MaxSize)
            {
                CacheMaxSizeReached?.Invoke(this, new EventArgs());
            }

        }

        public string GetBatch()
        {
            try
            {
                if (Count > 0)
                {
                    string batchSQL;
                    lock (SQL)
                    {
                        batchSQL = SQL.ToString(); 
                        resetBatch();
                    }

                    _stopwatch.Stop();
                    _stopwatch.Reset();
                    _ageTimer.Change(System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);

                    return batchSQL;
                }
                else
                    return null;
            }
            catch (Exception ex)
            {
                Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Error while preparing data write batch");
                return null;
            }
        }

        private void resetBatch()
        {
            SQL.Clear();
            SQL.Append("Insert INTO ");
            SQL.Append(Destination);
            SQL.Append(" (DPDUID,Timestamp,Value,Quality) values ");
            Count = 0;
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (_ageTimer != null)
                    {
                        _ageTimer.Change(System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
                        _ageTimer.Dispose();
                        _ageTimer = null;
                    }
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~Cache() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}
