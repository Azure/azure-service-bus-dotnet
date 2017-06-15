﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Microsoft.Azure.ServiceBus.Primitives
{
    internal sealed class ConcurrentExpiringSet<TKey>
    {
        readonly object cleanupSynObject = new object();
        readonly ConcurrentDictionary<TKey, DateTime> dictionary;
        bool cleanupScheduled;

        public ConcurrentExpiringSet()
        {
            dictionary = new ConcurrentDictionary<TKey, DateTime>();
        }

        public void AddOrUpdate(TKey key, DateTime expiration)
        {
            dictionary[key] = expiration;
            ScheduleCleanup();
        }

        public bool Contains(TKey key)
        {
            DateTime expiration;
            if (dictionary.TryGetValue(key, out expiration))
                return true;

            return false;
        }

        void ScheduleCleanup()
        {
            lock (cleanupSynObject)
            {
                if (cleanupScheduled)
                    return;

                cleanupScheduled = true;
                Task.Run(() => CollectExpiredEntries());
            }
        }

        void CollectExpiredEntries()
        {
            lock (cleanupSynObject)
            {
                cleanupScheduled = false;
            }

            foreach (var key in dictionary.Keys)
                if (DateTime.UtcNow > dictionary[key])
                {
                    DateTime entry;
                    dictionary.TryRemove(key, out entry);
                }

            ScheduleCleanup();
        }
    }
}