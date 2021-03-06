﻿using System;
using System.Threading.Tasks;
using Serilog;
using StackExchange.Redis.Extensions.Core.Configuration;

namespace Defcon.Library.Redis
{
    public class Setup
    {
        public RedisConfiguration RedisConfiguration;
        private readonly ILogger logger;
        private int database;

        public Setup(ILogger logger)
        {
            this.logger = logger;
            var configuration = Configuration();
        }

        private async Task Configuration()
        {
            bool validInput;
            bool validRedisInstance;

            this.logger.Information("Initializing the Redis database configuration...");
            this.logger.Warning("The application does not check if the redis database instance is already in use or not!");

            do
            {
                Console.Write("Please enter your Redis database instance [0-15]: ");
                var input = Console.ReadLine();
                validInput = int.TryParse(input, out database);

                if (!validInput)
                {
                    this.logger.Error("Input is not an integer.");
                }

                if (database > 15 || database < 0)
                {
                    this.logger.Error("Not a valid database instance. Redis supports only zero (0) till fifteen (15).");
                    validRedisInstance = false;
                }
                else
                {
                    this.logger.Information("Valid Redis database instance.");
                    validRedisInstance = true;
                }
            } while (validInput == false || validRedisInstance == false);

            try
            {
                RedisConfiguration = new RedisConfiguration
                {
                    AbortOnConnectFail = true,
                    Hosts = new[]
                    {
                        new RedisHost {Host = "127.0.0.1", Port = 6379}
                    },
                    AllowAdmin = true,
                    Database = database,
                    Ssl = false,
                    ServerEnumerationStrategy = new ServerEnumerationStrategy
                    {
                        Mode = ServerEnumerationStrategy.ModeOptions.All,
                        TargetRole = ServerEnumerationStrategy.TargetRoleOptions.Any,
                        UnreachableServerAction = ServerEnumerationStrategy.UnreachableServerActionOptions.Throw
                    },
                    MaxValueLength = 2048
                };

                this.logger.Information($"Successfully setup configuration for redis database {database}.");
            }
            catch (Exception e)
            {
                this.logger.Fatal(e, "Redis configuration failed.");
                Environment.Exit(102);
            }
        }
    }
}