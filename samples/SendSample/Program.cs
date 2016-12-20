﻿// Copyright (c) Microsoft. All rights reserved.
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
            // Creates a ServiceBusConnectionStringBuilder object from the connection string, and sets the EntityPath.
            var connectionStringBuilder = new ServiceBusConnectionStringBuilder(ServiceBusConnectionString)
            {
                EntityPath = QueueName
            };

            // Initializes the static QueueClient variable that will be used in the ReceiveMessages method.
            queueClient = QueueClient.CreateFromConnectionString(connectionStringBuilder.ToString());

            await SendMessagesToQueue(10);

            // Close the client after the ReceiveMessages method has exited.
            await queueClient.CloseAsync();

            Console.WriteLine("Press any key to exit.");
            Console.ReadLine();
        }

        // Creates a Queue client and sends 10 messages to the queue.
        private static async Task SendMessagesToQueue(int numMessagesToSend)
        {
            for (var i = 0; i < numMessagesToSend; i++)
            {
                try
                {
                    // Create a new brokered message to send to the queue
                    var message = new BrokeredMessage($"Message {i}");

                    // Write the body of the message to the console
                    Console.WriteLine($"Sending message: {message.GetBody<string>()}");

                    // Send the message to the queue
                    await queueClient.SendAsync(message);
                }
                catch (Exception exception)
                {
                    Console.WriteLine($"{DateTime.Now} > Exception: {exception.Message}");
                }

                // Delay by 10 milliseconds so that the console can keep up
                await Task.Delay(10);
            }

            Console.WriteLine($"{numMessagesToSend} messages sent.");
        }
    }
}