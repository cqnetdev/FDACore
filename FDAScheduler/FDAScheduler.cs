using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Common;
using System.Diagnostics;
using System.Threading.Tasks;

namespace FDA
{
    public enum ScheduleType {Realtime,Hourly,Daily,Monthly,Specific,OneShot};

    //********************************************************************************** FDAScheduler (base class) ************************************************************
    public abstract class FDAScheduler: IDisposable
    {
        public Guid ID { get; set; }
        public string Description { get; set; }
        public ScheduleType ScheduleType;
        protected Timer Timer;
        public bool Enabled { get; set; }
        public List<RequestGroup> RequestGroupList { get; set; }
        public List<FDATask> TasksList { get; set; }

        public class TimerEventArgs:EventArgs
        {
            public Guid ScheduleID;
            public ScheduleType Type;
            public List<RequestGroup> RequestGroupList;
           
            public TimerEventArgs(Guid schedID,ScheduleType type, List<RequestGroup> eventRequestGroupList)
            {
                ScheduleID = schedID;
                Type = type;
                RequestGroupList = eventRequestGroupList;
            }

        
        }

        public delegate void TimerElapsedHandler(object sender, TimerEventArgs e);
        public event TimerElapsedHandler TimerElapsed;

        protected FDAScheduler(Guid TimerID,string description,List<RequestGroup> requestGroupList, List<FDATask> tasksList)
        {
            ID = TimerID;
            Enabled = false;
            RequestGroupList = requestGroupList;
            TasksList = tasksList;
            Description = description;
        }


        protected virtual void TimerTick(Object o)
        {
            if (Enabled)
                RaiseTimerElapsedEvent();
        }


        public void ForceExecute()
        {
            RaiseTimerElapsedEvent();
        }

        protected void RaiseTimerElapsedEvent()
        {
            TimerElapsed?.Invoke(this, new TimerEventArgs(ID, ScheduleType, RequestGroupList));
        }


        public void Dispose()
        {
            Globals.SystemManager.LogApplicationEvent(this, "", "Stopping Scheduler '" + Description + "' (" + ID + ")");
            Enabled = false;
            if (Timer != null)
            {
                Timer.Dispose();
                Timer = null;
            }
            GC.SuppressFinalize(this);
        }
    }

    //******************************************************************** Oneshot scheduler *******************************************
    public class FDASchedulerOneshot : FDAScheduler
    {
        private readonly int delayms = 500;
        public FDASchedulerOneshot(Guid TimerID, string description, List<RequestGroup> RequestGroupList, List<FDATask> tasksList,bool suppressExecution=false) : base(TimerID, description, RequestGroupList, tasksList)
        {
            ScheduleType = ScheduleType.OneShot;
            if (!suppressExecution)
                Timer = new Timer(TimerTick, ID, new TimeSpan(0, 0, 0, 0, delayms), new TimeSpan(0, 0, 0, 0,-1));
        }
    }

    //*******************************************************************************  Realtime Scheduler ****************************************************************

    public class FDASchedulerRealtime : FDAScheduler
    {
        private int _TimerSeconds;
        public DateTime NextTick { get; private set; }
        private TimeSpan TickInterval;
        private readonly TimeSpan InitialDelay;

        public int TimerSeconds { get { return _TimerSeconds; } set { _TimerSeconds = value; UpdateTimer(_TimerSeconds); }  }

        public FDASchedulerRealtime(Guid TimerID,string description,List<RequestGroup> RequestGroupList,List<FDATask> tasksList,int timerSeconds,TimeSpan delay) : base(TimerID, description, RequestGroupList,tasksList)
        {
            ScheduleType = ScheduleType.Realtime;
            base.TimerElapsed += Tick;
            InitialDelay = delay;
            if (InitialDelay < TimeSpan.Zero)
                InitialDelay = TimeSpan.Zero;
            TimerSeconds = timerSeconds;
        }

        private void Tick(object sender,TimerEventArgs e)
        {
            NextTick = Globals.FDANow().Add(TickInterval);
        }

        
        private void UpdateTimer(int newrate)
        {
            if (Timer == null)
            {
                TickInterval = TimeSpan.FromSeconds(newrate);
                Timer = new Timer(TimerTick, ID,InitialDelay, TickInterval);
            }
            else
            {
                if (newrate > 0)
                {
                    TickInterval = TimeSpan.FromSeconds(newrate);
                    Timer.Change(TimeSpan.FromSeconds(0), TickInterval);
                }
                else
                    Globals.SystemManager.LogApplicationEvent(this, Description, "Invalid RealTime scan rate " + newrate);
            }
        }
        
      
    }
    //*******************************************************************************   Hourly Scheduler *************************************************************

    public class FDASchedulerHourly: FDAScheduler
    {
        public FDASchedulerHourly(Guid TimerID,string description,List<RequestGroup> requestGroupList,List<FDATask> tasksList,DateTime scheduleTime): base(TimerID,description,requestGroupList,tasksList)
        {
            ScheduleType = ScheduleType.Hourly;
            TimeSpan timeToFirstRun = TimeSpan.MinValue;
            TimeSpan delayTime = TimeSpan.FromHours(1);

            DateTime currentTime = DateTime.Now;
            DateTime nextScheduledTime;

            nextScheduledTime = new DateTime(currentTime.Year, currentTime.Month, currentTime.Day, currentTime.Hour, scheduleTime.Minute, scheduleTime.Second, DateTimeKind.Local);
            if (currentTime.CompareTo(nextScheduledTime) > 0)
                nextScheduledTime = nextScheduledTime.AddHours(1);

            timeToFirstRun = nextScheduledTime.Subtract(currentTime);

            Timer = new Timer(TimerTick, ID, timeToFirstRun, delayTime);
        }

        
        public void Update(int minute,int second)
        {
            TimeSpan timeToFirstRun;
            //TimeSpan delayTime = TimeSpan.FromHours(1);

            DateTime currentTime = DateTime.Now;
            DateTime nextScheduledTime;

            nextScheduledTime = new DateTime(currentTime.Year, currentTime.Month, currentTime.Day, currentTime.Hour, minute, second, DateTimeKind.Local);
            if (currentTime.CompareTo(nextScheduledTime) > 0)
                nextScheduledTime = nextScheduledTime.AddHours(1);

            timeToFirstRun = nextScheduledTime.Subtract(currentTime);

            if (Timer != null)
                Timer.Change(timeToFirstRun, TimeSpan.FromHours(1));
        }
        
    }

    //******************************************************************************  Daily Scheduler ****************************************************************
    public class FDASchedulerDaily : FDAScheduler
    {
       
        public FDASchedulerDaily(Guid TimerID,string description,List<RequestGroup> requestGroupList,List<FDATask> tasksList,DateTime scheduleTime) : base(TimerID,description,requestGroupList, tasksList)
        {
            ScheduleType = ScheduleType.Daily;
            TimeSpan timeToFirstRun = TimeSpan.MinValue;
            TimeSpan delayTime = TimeSpan.MaxValue;

            DateTime currentTime = Globals.FDANow();
            DateTime nextScheduledTime;

                          
            nextScheduledTime = new DateTime(currentTime.Year, currentTime.Month, currentTime.Day, scheduleTime.Hour, scheduleTime.Minute, scheduleTime.Second, DateTimeKind.Local);
            if (currentTime.CompareTo(nextScheduledTime) > 0)
                nextScheduledTime = nextScheduledTime.AddDays(1);

            timeToFirstRun = nextScheduledTime.Subtract(currentTime);
            delayTime = TimeSpan.FromDays(1);
                    
            Timer = new Timer(TimerTick, ID, timeToFirstRun, delayTime);
        }

        
        public void Update(int hour,int minute,int second)
        {
            TimeSpan timeToFirstRun;
            TimeSpan delayTime;

            DateTime currentTime = Globals.FDANow();
            DateTime nextScheduledTime;


            nextScheduledTime = new DateTime(currentTime.Year, currentTime.Month, currentTime.Day, hour, minute, second, DateTimeKind.Local);
            if (currentTime.CompareTo(nextScheduledTime) > 0)
                nextScheduledTime = nextScheduledTime.AddDays(1);

            timeToFirstRun = nextScheduledTime.Subtract(currentTime);
            delayTime = TimeSpan.FromDays(1);

            if (Timer != null)
                Timer.Change(timeToFirstRun, delayTime);
        }
        

       
    }

    //****************************************************************************  Monthly Scheduler ****************************************************************
    public class FDASchedulerMonthly : FDAScheduler
    {
        private DateTime ScheduleTime;

        public FDASchedulerMonthly(Guid TimerID, string description, List<RequestGroup> requestGroupList,List<FDATask> tasksList, DateTime scheduleTime) : base(TimerID, description, requestGroupList,tasksList)
        {
            ScheduleType = ScheduleType.Monthly;
            ScheduleTime = scheduleTime;

            Timer = new Timer(TimerTick, ID, TimeSpan.Zero, TimeSpan.FromSeconds(15));          
        }

        // override the default TimerTick behaviour (don't raise a message every time the timer ticks!)
        protected override void TimerTick(Object o)
        {
            DateTime currentTime = Globals.FDANow();
            if (Enabled)
                if (currentTime.Day == ScheduleTime.Day && currentTime.Hour == ScheduleTime.Hour && currentTime.Minute == ScheduleTime.Minute)
                    RaiseTimerElapsedEvent();
        }

        
        public void Update(int day,int hour,int minute,int second)
        {
            DateTime currentTime = DateTime.Now;
            ScheduleTime = new DateTime(currentTime.Year,currentTime.Month,day, hour, minute, second);
        }
        

      
    }


}
