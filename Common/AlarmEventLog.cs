namespace Common
{
    public abstract class AlarmEventRecord : object
    {
        public abstract string GetWriteSQL();

        public abstract string GetUpdateLastRecordSQL();

        public bool Valid = true;
    }
}