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
    private readonly IProducer<string, string>? _producer;
    private readonly Func<string, string, string, CancellationToken, Task> _publishAsync;

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
        _publishAsync = async (topic, key, value, ct) =>
        {
            var result = await _producer.ProduceAsync(topic, new Message<string, string>
            {
                Key = key,
                Value = value
            }, ct);

            _logger.LogInformation("Evento publicado no Kafka topic {Topic} partition {Partition} offset {Offset}",
                topic, result.Partition.Value, result.Offset.Value);
        };
    }

    public KafkaFinanceEventsPublisher(
        IOptions<KafkaSettings> options,
        ILogger<KafkaFinanceEventsPublisher> logger,
        Func<string, string, string, CancellationToken, Task> publishAsync)
    {
        _settings = options.Value;
        _logger = logger;
        _publishAsync = publishAsync;
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

    public async Task PublishIrVendaAsync(EventosIR evt, string cpf, IrVendaKafkaPayload payload, CancellationToken ct = default)
    {
        var message = new
        {
            tipo = "IR_VENDA",
            clienteId = evt.ClienteId,
            cpf,
            mesReferencia = payload.MesReferencia,
            totalVendasMes = payload.TotalVendasMes,
            lucroLiquido = payload.LucroLiquido,
            aliquota = payload.Aliquota,
            valorIR = evt.ValorIR,
            detalhes = payload.Detalhes.Select(x => new
            {
                ticker = x.Ticker,
                quantidade = x.Quantidade,
                precoVenda = x.PrecoVenda,
                precoMedio = x.PrecoMedio,
                lucro = x.Lucro
            }),
            dataCalculo = payload.DataCalculo
        };

        await PublishAsync(_settings.IrVendaTopic, evt.ClienteId.ToString(), message, ct);
    }

    private async Task PublishAsync(string topic, string key, object payload, CancellationToken ct)
    {
        var value = JsonSerializer.Serialize(payload);
        await _publishAsync(topic, key, value, ct);
    }

    public void Dispose()
    {
        if (_producer is null)
        {
            return;
        }

        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
    }
}
