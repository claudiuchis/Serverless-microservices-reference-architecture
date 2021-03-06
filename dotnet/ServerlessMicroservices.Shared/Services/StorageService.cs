﻿using Azure.Storage;
using Azure.Storage.Queues;
using Newtonsoft.Json;
using ServerlessMicroservices.Models;
using System;
using System.Threading.Tasks;

namespace ServerlessMicroservices.Shared.Services
{
    public class StorageService : IStorageService
    {
        public const string LOG_TAG = "StorageService";

        private bool _isStorageInitialized = false;

        private ISettingService _settingService;
        private ILoggerService _loggerService;

        private QueueClient _tripManagersQueue;
        private QueueClient _tripMonitorsQueue;
        private QueueClient _tripDemosQueue;
        private QueueClient _tripDriversQueue;

        public StorageService(ISettingService setting, ILoggerService logger)
        {
            _settingService = setting;
            _loggerService = logger;
        }

        public async Task Enqueue(TripItem trip)
        {
            await InitializeStorage();

            if (_tripManagersQueue != null)
            {
                await _tripManagersQueue.SendMessageAsync(JsonConvert.SerializeObject(trip));
            }
        }

        public async Task Enqueue(TripDemoState tripDemoState)
        {
            await InitializeStorage();

            if (_tripDemosQueue != null)
            {
                var queueMessage = JsonConvert.SerializeObject(tripDemoState);
                await _tripDemosQueue.SendMessageAsync(queueMessage);
            }
        }

        public async Task Enqueue(string tripCode, string driverCode)
        {
            await InitializeStorage();

            if (_tripDriversQueue != null)
            {
                var queueMessage = JsonConvert.SerializeObject(new TripDriver
                {
                    TripCode = tripCode,
                    DriverCode = driverCode
                });
                await _tripDriversQueue.SendMessageAsync(queueMessage);
            }
        }

        // PRIVATE//

        private async Task InitializeStorage()
        {
            if (_isStorageInitialized)
                return;

            var error = "";

            try
            {
                if (!string.IsNullOrEmpty(_settingService.GetStorageAccount()))
                {
                    // Queues Initialization
                    var storageAccount = _settingService.GetStorageAccount();
                    //queueClient.DefaultRequestOptions.RetryPolicy = new LinearRetry(TimeSpan.FromSeconds(3), 3);

                    var tripManagersQueueName = _settingService.GetTripManagersQueueName();
                    if (!string.IsNullOrEmpty(tripManagersQueueName))
                    {
                        _tripManagersQueue = new QueueClient(storageAccount, tripManagersQueueName);
                        await _tripManagersQueue.CreateIfNotExistsAsync();
                    }
                    else
                        _loggerService.Log("tripManagersQueueName is empty");

                    var tripMonitorsQueueName = _settingService.GetTripMonitorsQueueName();
                    if (!string.IsNullOrEmpty(tripMonitorsQueueName))
                    {
                        _tripMonitorsQueue = new QueueClient(storageAccount, tripMonitorsQueueName);
                        await _tripMonitorsQueue.CreateIfNotExistsAsync();
                    }
                    else
                        _loggerService.Log("tripMonitorsQueueName is empty");

                    var tripDemosQueueName = _settingService.GetTripDemosQueueName();
                    if (!string.IsNullOrEmpty(tripDemosQueueName))
                    {
                        _tripDemosQueue = new QueueClient(storageAccount, tripDemosQueueName);
                        await _tripDemosQueue.CreateIfNotExistsAsync();
                    }
                    else
                        _loggerService.Log("tripDemosQueueName is empty");

                    var tripDriversQueueName = _settingService.GetTripDriversQueueName();
                    if (!string.IsNullOrEmpty(tripDriversQueueName))
                    {
                        _tripDriversQueue = new QueueClient(storageAccount, tripDriversQueueName);
                        await _tripDriversQueue.CreateIfNotExistsAsync();
                    }
                    else
                        _loggerService.Log("tripDriversQueueName is empty");

                    _isStorageInitialized = true;
                }
            }
            catch (Exception ex)
            {
                error = $"Error while initializing storage: {ex.Message}";
                throw new Exception(error);
            }
            finally
            {
                _loggerService.Log($"{LOG_TAG} - InitializeStorage - Error: {error}");
            }
        }
    }
}
