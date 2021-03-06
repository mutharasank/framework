﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;

namespace Signum.Utilities.DataStructures
{
    public class RecentDictionary<K, V>: IEnumerable<KeyValuePair<K,V>> 
    {
        int capacity;
        LinkedList<V> orderList = new LinkedList<V>();
        Dictionary<LinkedListNode<V>, K> linkToKey = new Dictionary<LinkedListNode<V>, K>();
        Dictionary<K, LinkedListNode<V>> keyToLink = new Dictionary<K, LinkedListNode<V>>();

        /// <summary>
        /// Default constructor for the most recently used items using the default size (50)
        /// </summary>
        public RecentDictionary(): this(50, null)
        {
        }

        /// <summary>
        /// Construct a most recently used items list with the maximum number of items
        /// allowed in the list.
        /// </summary>
        /// <param name="maxItems">Maximum number of items allowed</param>
        public RecentDictionary(int capacity): this(capacity, null)
        {
        }

        public RecentDictionary(int capacity, IEqualityComparer<K> comparer)
        {
            this.capacity = capacity;
            keyToLink = new Dictionary<K, LinkedListNode<V>>(comparer);
        }

        void MoveToHead(LinkedListNode<V> value)
        {
            orderList.Remove(value);
            orderList.AddFirst(value);
        }

        public void Add(K key, V value)
        {
            if (keyToLink.ContainsKey(key))
                throw new ArgumentException("Key already in the dictionary");

            LinkedListNode<V> link = orderList.AddFirst(value);

            if (keyToLink.Keys.Count >= capacity)
            {
                // Purge an item from the cache
                LinkedListNode<V> tail = orderList.Last;

                if (tail != null)
                {
                    K purgeKey = linkToKey[tail];

                    // Fire the event
                    if (Purged != null)
                    {
                        Purged(purgeKey, tail.Value);
                    }

                    Remove(purgeKey);
                }
            }

            keyToLink.Add(key, link);

            // Keep a reverse index from the link to the key
            linkToKey[link] = key;

        }

        public bool Contains(K key)
        {
            LinkedListNode<V> node;
            if (keyToLink.TryGetValue(key, out node))
            {
                MoveToHead(node);
                return true;
            }

            return false;
        }

        public void Remove(K key)
        {
            LinkedListNode<V> link = keyToLink[key];

            keyToLink.Remove(key);

            if (link != null)
            {
                orderList.Remove(link);

                // Keep a reverse index from the link to the key
                linkToKey.Remove(link);
            }
        }

        public V this[K key]
        {
            get
            {
                LinkedListNode<V> value;
                if (keyToLink.TryGetValue(key, out value))
                {
                    MoveToHead(value);
                    return value.Value;
                }
                throw new KeyNotFoundException("Key {0} not found".Formato(key));
            }
            set
            {
                LinkedListNode<V> link = null;

                if (keyToLink.TryGetValue(key, out link))
                {
                    link.Value = value;

                    MoveToHead(link);

                    keyToLink[key] = link;

                    // Keep a reverse index from the link to the key
                    linkToKey[link] = key;
                }
                else
                {
                    Add(key, value);
                }
            }
        }

        public bool TryGetValue(K key, out V value)
        {
            LinkedListNode<V> node;
            if (keyToLink.TryGetValue(key, out node))
            {
                MoveToHead(node);
                value = node.Value;
                return true;
            }

            value = default(V);
            return false;
        }

        public V GetOrCreate(K key, Func<V> createValue)
        {
            V value;
            if (!TryGetValue(key, out value))
            {
                value = createValue();
                Add(key, value);
            }
            return value;
        }

        /// <summary>
        /// The maximum capacity of the list
        /// </summary>
        public int Capacity
        {
            get { return capacity; }
            set { capacity = value; }
        }

        public int Count
        {
            get { return linkToKey.Count; }
        }

        /// <summary>
        /// Event that is fired when an item falls outside of the cache
        /// </summary>
        public event Action<K,V> Purged;

        public override string ToString()
        {
            StringBuilder buff = new StringBuilder(Convert.ToInt32(capacity));

            buff.Append("[");

            foreach (V item in orderList)
            {
                if (buff.Length > 1)
                    buff.Append(", ");

                buff.Append(item.ToString());
            }

            buff.Append("]");

            return buff.ToString();
        }


        public IEnumerator<KeyValuePair<K, V>> GetEnumerator()
        {
            foreach (var item in keyToLink)
            {
                yield return new KeyValuePair<K, V>(item.Key, item.Value.Value);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator(); 
        }
    }

    //public class Program
    //{
    //    static string[] nombre = { "cero", "uno", "dos", "tres", "cuatro", "cinco", "seis", "siete", "ocho" };


    //    public static void Main(string[] args)
    //    {
    //        RecentsDictionary<int, string> mru = new RecentsDictionary<int, string>(3);

    //        mru.OnPurgedFromCache += mru_OnPurgedFromCache;

    //        Console.WriteLine(">> State: " + mru);

    //        for (int i = 0; i < 5; i++)
    //        {
    //            Console.WriteLine("Adding " + i);

    //            mru[i] = nombre[i];

    //            Console.WriteLine(">> State: " + mru);
    //        }

    //        // Reference a couple of items
    //        Console.WriteLine("Query for " + mru[3]);
    //        Console.WriteLine(">> State: " + mru);
    //        Console.WriteLine("Query for " + mru[2]);
    //        Console.WriteLine(">> State: " + mru);
    //        Console.WriteLine("Query for " + mru[4]);
    //        Console.WriteLine(">> State: " + mru);

    //        // Add a few more
    //        for (int i = 5; i < 7; i++)
    //        {
    //            Console.WriteLine("Adding " + i);

    //            mru[i] = nombre[i];

    //            Console.WriteLine(">> State: " + mru);
    //        }

    //        // Reference the tail
    //        Console.WriteLine("Query for " + mru[4]);
    //        Console.WriteLine(">> State: " + mru);

    //    }

    //    private static void mru_OnPurgedFromCache(int key, string value)
    //    {
    //        Console.WriteLine("item purged from cache: " + key.ToString() + " , " + value.ToString());
    //    }
    //}
}
