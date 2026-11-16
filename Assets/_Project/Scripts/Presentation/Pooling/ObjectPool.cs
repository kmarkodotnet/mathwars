// ObjectPool.cs
// F1-14 — generic reuse pool for enemies, projectiles and short-lived effects
// (math-horde.md 18. szakasz: TILOS Instantiate/Destroy a harci ciklusban,
// TILOS allokálni Update()-ben).
//
// Deliberately engine-free: T is constrained only to `class`, and creation/
// activation/deactivation are caller-supplied delegates. This lets the pool
// wrap MonoBehaviour-derived views in production (factory = () =>
// Object.Instantiate(prefab).GetComponent<EnemyView>()) while staying fully
// unit-testable with a plain POCO stand-in — no UnityEngine type appears in
// this file. Steady-state Get()/Release() never allocates a new instance
// while a free one exists; CreatedCount only grows when the pool must grow
// (e.g. during a documented prewarm, never silently inside the combat loop).
using System;
using System.Collections.Generic;

namespace MathHorde.Presentation.Pooling
{
    public sealed class ObjectPool<T> where T : class
    {
        private readonly Func<T> _factory;
        private readonly Action<T> _onGet;
        private readonly Action<T> _onRelease;
        private readonly Stack<T> _free = new Stack<T>();

        /// <summary>Total instances ever created by this pool (factory invocations).</summary>
        public int CreatedCount { get; private set; }

        /// <summary>Instances currently handed out (Get'd but not yet Released).</summary>
        public int ActiveCount { get; private set; }

        /// <summary>Instances sitting idle, ready to be reused by the next Get().</summary>
        public int FreeCount => _free.Count;

        /// <param name="factory">Creates one new instance. Called only on growth.</param>
        /// <param name="onGet">Optional: runs on an instance right before it is handed out (e.g. SetActive(true)).</param>
        /// <param name="onRelease">Optional: runs on an instance right before it rejoins the free list (e.g. SetActive(false)).</param>
        /// <param name="prewarmCount">How many instances to create immediately, up front — outside the combat loop.</param>
        public ObjectPool(Func<T> factory, Action<T> onGet = null, Action<T> onRelease = null, int prewarmCount = 0)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _onGet = onGet;
            _onRelease = onRelease;

            for (int i = 0; i < prewarmCount; i++)
            {
                _free.Push(CreateNew());
            }
        }

        /// <summary>
        /// Hands out a reused instance when one is free; only creates a new
        /// one when the pool is empty (i.e. demand exceeded every prior
        /// prewarm/return). This is the one place CreatedCount can grow after
        /// construction.
        /// </summary>
        public T Get()
        {
            T item = _free.Count > 0 ? _free.Pop() : CreateNew();
            ActiveCount++;
            _onGet?.Invoke(item);
            return item;
        }

        /// <summary>Returns an instance to the free list for later reuse. Never destroys it.</summary>
        public void Release(T item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            _onRelease?.Invoke(item);
            ActiveCount = Math.Max(0, ActiveCount - 1);
            _free.Push(item);
        }

        private T CreateNew()
        {
            CreatedCount++;
            return _factory();
        }
    }
}
