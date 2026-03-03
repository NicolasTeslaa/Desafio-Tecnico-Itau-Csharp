namespace ScheduledPurchaseEngineService.Settings;

public sealed class KafkaSettings
{
    public const string SectionName = "Kafka";

    public string BootstrapServers { get; set; } = "localhost:29092";
    public string IrDedoDuroTopic { get; set; } = "ir-dedo-duro";
    public string IrVendaTopic { get; set; } = "ir-venda";
}

