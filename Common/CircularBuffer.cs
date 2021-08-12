using System.Collections;
using System.Collections.Generic;

namespace Common
{
    public class CircularBuffer<T> : IEnumerable
    {
        private readonly T[] _buffer;
        private int _idx;
        private int _count;

        public int Count { get => _count; }
        public T this[int index] { get { return _buffer[index]; } }

        public CircularBuffer(int size)
        {
            _buffer = new T[size];
            _idx = 0;
            _count = 0;
        }

        public List<T> GetBuffer()
        {
            List<T> bufferCopy = new(_buffer.Length);
            foreach (T item in _buffer)
                bufferCopy.Add(item);
            return bufferCopy;
        }

        public IEnumerator GetEnumerator()
        {
            return _buffer.GetEnumerator();
        }

        public void AddItem(T item)
        {
            _buffer[_idx] = item;
            _idx++;
            if (_idx == _buffer.Length)
                _idx = 0;
            if (_count < _buffer.Length)
                _count++;
        }

        public T[] GetAll()
        {
            T[] temp = new T[_count];
            int ptr = _idx - 1;
            if (ptr < 0) ptr = _buffer.Length - 1;

            for (int i = 0; i < _count; i++)
            {
                temp[i] = _buffer[ptr];
                ptr--;
                if (ptr < 0)
                    ptr = _buffer.Length - 1;
            }

            return temp;
        }

        public T GetMostRecent()
        {
            int ptr = _idx - 1;
            if (ptr < 0) ptr = _buffer.Length - 1;
            return _buffer[ptr];
        }

        public T GetOldest()
        {
            int ptr = _idx - _count;
            if (ptr < 0)
                ptr = _buffer.Length + ptr;
            return _buffer[ptr];
        }

        public void Clear()
        {
            _count = 0;
        }
    }
}