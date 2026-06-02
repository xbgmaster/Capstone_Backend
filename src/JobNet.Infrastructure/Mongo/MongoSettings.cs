namespace JobNet.Infrastructure.Mongo;

public class MongoSettings
{
    public string ConnectionString { get; set; } = "mongodb://localhost:27017";
    public string Database { get; set; } = "jobnet_logs";
    public string AuditCollection { get; set; } = "audit_logs";
    public string EventCollection { get; set; } = "domain_events";
}
