using JobNet.Domain.Documents;
using Microsoft.Extensions.Options;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;

namespace JobNet.Infrastructure.Mongo;

public class MongoContext
{
    private readonly IMongoDatabase _db;
    private readonly MongoSettings _settings;

    static MongoContext()
    {
        // Camel-case BSON property names by default to match JSON conventions.
        var pack = new ConventionPack { new CamelCaseElementNameConvention() };
        ConventionRegistry.Register("camelCase", pack, _ => true);
    }

    public MongoContext(IOptions<MongoSettings> options)
    {
        _settings = options.Value;
        var client = new MongoClient(_settings.ConnectionString);
        _db = client.GetDatabase(_settings.Database);
    }

    public IMongoCollection<AuditLog> AuditLogs =>
        _db.GetCollection<AuditLog>(_settings.AuditCollection);

    public IMongoCollection<DomainEvent> DomainEvents =>
        _db.GetCollection<DomainEvent>(_settings.EventCollection);
}
