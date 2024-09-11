using System.Collections.Generic;
using UnityEngine;

namespace AlexeyH.Common.Universal
{
    public class LocalPool<T> where T : Component
    {
        public IReadOnlyList<T> FreeList => _free;
        public IReadOnlyList<T> BusyList => _busy;

        private List<T> _free;
        private List<T> _busy;

        private T _prototype;
        private Transform _parent;

        public LocalPool(T prototype, Transform parent)
        {
            _prototype = prototype;
            _parent = parent;

            _free = new();
            _busy = new();
        }

        public void UpdateAmount(int targetAmount)
        {
            while (_busy.Count > targetAmount)
            {
                _busy[targetAmount].gameObject.SetActive(false);
                _free.Add(_busy[targetAmount]);
                _busy.RemoveAt(targetAmount);
            }

            while (_busy.Count < targetAmount)
            {
                if (_free.Count > 0)
                {
                    _busy.Add(_free[0]);
                    _free.RemoveAt(0);
                }
                else
                {
                    _busy.Add(Object.Instantiate(_prototype, _parent));
                }
            }
        }
    }
}