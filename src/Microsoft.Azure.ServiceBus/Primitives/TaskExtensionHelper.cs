// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Reflection;
using System.Threading.Tasks;

namespace Microsoft.Azure.ServiceBus.Primitives
{
    internal static class TaskExtensionHelper
    {
        public static void Schedule(Func<Task> func)
        {
            Task.Run(async () =>
            {
                try
                {
                    await func().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    MessagingEventSource.Log.ScheduleTaskFailed(func.Target.GetType().FullName, func.GetMethodInfo().Name, ex.ToString());
                }
            });
        }
    }
}