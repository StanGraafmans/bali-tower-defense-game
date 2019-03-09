﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Entities.Interfaces;

namespace Game.Entities.MovingEntities
{
	/// <inheritdoc cref="Singleton{T}"/>
	/// <summary>
	/// Object pool for generic objects.
	/// T is constrained to UnityEngine.Object
	/// Can be used as collection, indexer is get only;
	/// </summary>
	/// <typeparam name="T"> Class or object you wish to pool.</typeparam>
	public class ObjectPool<T> : ICollection<T> where T : IPoolable
	{
		private static ObjectPool<T> s_Instance;

        public static ObjectPool<T> Instance => s_Instance ?? (s_Instance = new ObjectPool<T>());

        private readonly List<T> _objects = new List<T>();

		public T this[int index] => _objects[index];

		public int Count => _objects.Count;

		public bool IsReadOnly => false;

		public IEnumerator<T> GetEnumerator() => _objects.GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		public void Add(T item) => _objects.Add(item);

		public void Clear() => _objects.Clear();

		public bool Contains(T item) => _objects.Any(x => x.Equals(item));

		public void CopyTo(T[] array, int arrayIndex) => _objects.CopyTo(array, arrayIndex);

		public bool Remove(T item) => _objects.Remove(item);

		/// <summary>
        /// Activates the first object if any that is not active.
        /// Any poolable object with '<see cref="IPoolable.IsConducting"/>'
        /// set to false will be included.
        /// </summary>
        /// <param name="predicate">any predicate selecting what to enable.</param>
		public void ActivateObject(Func<T, bool> predicate)
		{
			 _objects.Where(x => !x.IsConducting()).FirstOrDefault(predicate)?.Activate();
        }
	}
}