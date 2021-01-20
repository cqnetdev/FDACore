using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Common
{
    public static class Console2
    {
        private static readonly StringBuilder _sb = new StringBuilder();
        private static volatile CancellationTokenSource _cts;
        private static int _count;
        private static int _flushRate = 500;

        public static void Write(string value)
        {
            lock (_sb) _sb.Append(value);
            ScheduleFlush();
        }
        public static void Write(string format, params object[] args)
        {
            lock (_sb) _sb.AppendFormat(format, args);
            ScheduleFlush();
        }
        public static void WriteLine(string value)
            => Write(value + Environment.NewLine);

        public static void WriteLine(string format, params object[] args)
            => Write(format + Environment.NewLine, args);

        public static void WriteLine()
            => WriteLine("");

        private static void ScheduleFlush()
        {
            _cts?.Cancel();
            var count = Interlocked.Increment(ref _count);
            if (count % 100 == 0) // periodically flush without cancellation
            {
                var fireAndForget = Task.Run((Action)Flush);
            }
            else
            {
                _cts = new CancellationTokenSource();
                var token = _cts.Token;
                var fireAndForget = Task.Run(async () =>
                {
                    await Task.Delay(_flushRate, token);
                    Flush();
                }, token);
            }
        }

        public static void Flush()
        {
            _cts?.Cancel();
            string text;
            lock (_sb)
            {
                if (_sb.Length == 0) return;
                text = _sb.ToString();
                _sb.Clear();
            }
            Console.Write(text);
        }
    }
}
