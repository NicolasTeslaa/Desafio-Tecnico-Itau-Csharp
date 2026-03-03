using ClassLibrary.Domain.Entities.RebalanceamentoIR;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using ScheduledPurchaseEngineService.Interfaces;
using ScheduledPurchaseEngineService.Settings;
using System.Text.Json;

namespace ScheduledPurchaseEngineService.Services;

public sealed class KafkaFinanceEventsPublisher : IFinanceEventsPublisher, IDisposable
{
    private readonly ILogger<KafkaFinanceEventsPublisher> _logger;
    private readonly KafkaSettings _settings;
    private readonly IProducer<string, string> _producer;

    public KafkaFinanceEventsPublisher(IOptions<KafkaSettings> options, ILogger<KafkaFinanceEventsPublisher> logger)
    {
        _settings = options.Value;
        _logger = logger;

        var config = new ProducerConfig
        {
            BootstrapServers = _settings.BootstrapServers,
            Acks = Acks.All,
            MessageTimeoutMs = 10000
        };

        _producer = new ProducerBuilder<string, string>(config).Build();
    }

    public async Task PublishIrDedoDuroAsync(EventosIR evt, string cpf, string ticker, CancellationToken ct = default)
    {
        var payload = new
        {
            tipo = "IR_DEDO_DURO",
            clienteId = evt.ClienteId,
            cpf,
            ticker,
            valorOperacao = evt.ValorBase,
            valorIR = evt.ValorIR,
            data = evt.DataEvento
        };

        await PublishAsync(_settings.IrDedoDuroTopic, evt.ClienteId.ToString(), payload, ct);
    }

    public async Task PublishIrVendaAsync(EventosIR evt, string cpf, string ticker, CancellationToken ct = default)
    {
        var payload = new
        {
            tipo = "IR_VENDA",
            clienteId = evt.ClienteId,
            cpf,
            ticker,
            valorOperacao = evt.ValorBase,
            valorIR = evt.ValorIR,
            data = evt.DataEvento
        };

        await PublishAsync(_settings.IrVendaTopic, evt.ClienteId.ToString(), payload, ct);
    }

    private async Task PublishAsync(string topic, string key, object payload, CancellationToken ct)
    {
        var value = JsonSerializer.Serialize(payload);
        var result = await _producer.ProduceAsync(topic, new Message<string, string>
        {
            Key = key,
            Value = value
        }, ct);

        _logger.LogInformation("Evento publicado no Kafka topic {Topic} partition {Partition} offset {Offset}",
            topic, result.Partition.Value, result.Offset.Value);
    }

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
    }
}
