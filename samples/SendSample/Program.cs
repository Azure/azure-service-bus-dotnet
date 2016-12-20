// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace SendSample
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.ServiceBus;

    public class Program
    {
        private static QueueClient queueClient;
        private const string ServiceBusConnectionString = "{Service Bus connection string}";
        private const string QueueName = "{Queue path/name}";

        public static void Main(string[] args)
        {
            MainAsync(args).GetAwaiter().GetResult();
        }

        private static async Task MainAsync(string[] args)
        {
            // Creates an ServiceBusConnectionStringBuilder object from a the connection string, and sets the EntityPath.
            // Typically the connection string should have the Entity Path in it, but for the sake of this simple scenario
            // we are using the connection string from the namespace.
            var connectionStringBuilder = new ServiceBusConnectionStringBuilder(ServiceBusConnectionString)
            {
                EntityPath = QueueName
            };

            queueClient = QueueClient.CreateFromConnectionString(connectionStringBuilder.ToString());

            await SendMessagesToQueue(10);

            await queueClient.CloseAsync();

            Console.WriteLine("Press any key to exit.");
            Console.ReadLine();
        }

        // Creates a Queue client and sends 100 messages to the queue.
        private static async Task SendMessagesToQueue(int numMessagesToSend)
        {
            for (var i = 0; i < numMessagesToSend; i++)
            {
                try
                {

                    var message = new BrokeredMessage($"Message {i}");
                    Console.WriteLine($"Sending message: {message.GetBody<string>()}");
                    await queueClient.SendAsync(message);
                }
                catch (Exception exception)
                {
                    Console.WriteLine($"{DateTime.Now} > Exception: {exception.Message}");
                }

                await Task.Delay(10);
            }

            Console.WriteLine($"{numMessagesToSend} messages sent.");
        }
    }
}