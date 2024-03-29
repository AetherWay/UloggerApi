﻿using LoggerApi.Models;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace LoggerApi.Services
{
    public class LoggerService : ILoggerService
    {
        private readonly IMongoCollection<CustomFields> _customFieldsCollection;
        private readonly IMongoCollection<Log> _logsCollection;

        public LoggerService(IOptions<ULoggerDatabaseSettings> uLoggerDatabaseSettings)
        {
            var mongoClient = new MongoClient(uLoggerDatabaseSettings.Value.ConnectionString);

            var mongoDatabase = mongoClient.GetDatabase(uLoggerDatabaseSettings.Value.DatabaseName);

            _customFieldsCollection = mongoDatabase.GetCollection<CustomFields>(uLoggerDatabaseSettings.Value.CustomFieldsCollectionName);

            _logsCollection = mongoDatabase.GetCollection<Log>(uLoggerDatabaseSettings.Value.LogsCollectionName);
        }

        public async Task<CustomFields> GetCustomFieldsAsync(string id)
        {
            var customFields = await _customFieldsCollection.Find(id).FirstOrDefaultAsync();

            var customFieldsDocument = customFields.ToBsonDocument();

            if (customFieldsDocument != null)
            {               
                foreach (var element in customFieldsDocument.Elements)
                {
                    if (element.Name.ToLower() != "appname" && element.Name.ToLower() != "environment")
                    {
                        if (customFields.Fields == null) 
                        { 
                            customFields.Fields = new Dictionary<string, string>(); 
                        }
                        customFields.Fields.Add(element.Name, element.Value?.ToString());
                    }
                }

                return customFields;
            }
            return null;
        }

        public async Task<string?> GetCustomFieldsIdByAppName(string appName, string environment)
        {
            try
            {
                var filter = Builders<CustomFields>.Filter.And(
                    Builders<CustomFields>.Filter.Eq("AppName", appName),
                    Builders<CustomFields>.Filter.Eq("Environment", environment)
                );

                var result = await _customFieldsCollection.Find(filter).FirstOrDefaultAsync();

                return result?.Id;
            }
            catch (Exception ex)
            {
                var log = new Log("ULogger", environment, LogLevel.Error, ex.Message);
                await _logsCollection.InsertOneAsync(log);
                return null;
            }
        }

        public async Task<List<Log>> GetLogsAsync(string appName, string environment, DateTime? startDateTime = null, DateTime? endDateTime = null) 
            => await _logsCollection.Find(x => x.AppName == appName && x.Environment == environment && x.CreatedWhen >= startDateTime && x.CreatedWhen <= endDateTime).ToListAsync();

        public async Task CreateCustomFieldsAsync(CustomFields customFields) => await _customFieldsCollection.InsertOneAsync(customFields);
        
        public async Task CreateLogAsync(Log log) => await _logsCollection.InsertOneAsync(log);
        
        public async Task UpdateCustomFieldsAsync(string id, CustomFields customFields) => await _customFieldsCollection.ReplaceOneAsync(id, customFields);

        public async Task DeleteCustomFieldsAsync(string id) => await _customFieldsCollection.DeleteOneAsync(x => x.Id == id);
    }
}
