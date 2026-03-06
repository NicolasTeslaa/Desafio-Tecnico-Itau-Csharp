using System.Text.Json;
using ClassLibrary.Domain.Entities.RebalanceamentoIR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ScheduledPurchaseEngineService.Interfaces;
using ScheduledPurchaseEngineService.Services;
using ScheduledPurchaseEngineService.Settings;

namespace ScheduledPurchaseEngineService.Tests;

public sealed class KafkaFinanceEventsPublisherTests
{
    [Fact]
    public async Task PublishIrDedoDuroAsync_BuildsPayloadExpectedByRequirements()
    {
        var captured = new CapturedKafkaMessage();
        using var publisher = CreatePublisher(captured);

        var evt = new EventosIR
        {
            ClienteId = 7,
            ValorBase = 280m,
            ValorIR = 0.01m,
            DataEvento = new DateTime(2026, 2, 5, 10, 0, 0, DateTimeKind.Utc)
        };

        await publisher.PublishIrDedoDuroAsync(evt, "12345678901", "PETR4");

        Assert.Equal("ir-dedo-duro", captured.Topic);
        Assert.Equal("7", captured.Key);

        using var json = JsonDocument.Parse(captured.Value!);
        var root = json.RootElement;

        Assert.Equal("IR_DEDO_DURO", root.GetProperty("tipo").GetString());
        Assert.Equal(7, root.GetProperty("clienteId").GetInt32());
        Assert.Equal("12345678901", root.GetProperty("cpf").GetString());
        Assert.Equal("PETR4", root.GetProperty("ticker").GetString());
        Assert.Equal(280m, root.GetProperty("valorOperacao").GetDecimal());
        Assert.Equal(0.01m, root.GetProperty("valorIR").GetDecimal());
    }

    [Fact]
    public async Task PublishIrVendaAsync_BuildsAggregatedPayloadExpectedByRequirements()
    {
        var captured = new CapturedKafkaMessage();
        using var publisher = CreatePublisher(captured);

        var evt = new EventosIR
        {
            ClienteId = 9,
            ValorBase = 3100m,
            ValorIR = 620m,
            DataEvento = new DateTime(2026, 3, 6, 12, 0, 0, DateTimeKind.Utc)
        };

        var payload = new IrVendaKafkaPayload(
            MesReferencia: "2026-03",
            TotalVendasMes: 21500m,
            LucroLiquido: 3100m,
            Aliquota: 0.20m,
            Detalhes:
            [
                new IrVendaKafkaDetail("BBDC4", 500, 16m, 14m, 1000m),
                new IrVendaKafkaDetail("WEGE3", 300, 45m, 38m, 2100m)
            ],
            DataCalculo: new DateTime(2026, 3, 6, 12, 0, 0, DateTimeKind.Utc));

        await publisher.PublishIrVendaAsync(evt, "98765432109", payload);

        Assert.Equal("ir-venda", captured.Topic);
        Assert.Equal("9", captured.Key);

        using var json = JsonDocument.Parse(captured.Value!);
        var root = json.RootElement;

        Assert.Equal("IR_VENDA", root.GetProperty("tipo").GetString());
        Assert.Equal("2026-03", root.GetProperty("mesReferencia").GetString());
        Assert.Equal(21500m, root.GetProperty("totalVendasMes").GetDecimal());
        Assert.Equal(3100m, root.GetProperty("lucroLiquido").GetDecimal());
        Assert.Equal(0.20m, root.GetProperty("aliquota").GetDecimal());
        Assert.Equal(620m, root.GetProperty("valorIR").GetDecimal());

        var detalhes = root.GetProperty("detalhes").EnumerateArray().ToList();
        Assert.Equal(2, detalhes.Count);
        Assert.Equal("BBDC4", detalhes[0].GetProperty("ticker").GetString());
        Assert.Equal(500, detalhes[0].GetProperty("quantidade").GetInt32());
        Assert.Equal(16m, detalhes[0].GetProperty("precoVenda").GetDecimal());
        Assert.Equal(14m, detalhes[0].GetProperty("precoMedio").GetDecimal());
        Assert.Equal(1000m, detalhes[0].GetProperty("lucro").GetDecimal());
    }

    private static KafkaFinanceEventsPublisher CreatePublisher(CapturedKafkaMessage captured)
    {
        var options = Options.Create(new KafkaSettings
        {
            BootstrapServers = "localhost:29092",
            IrDedoDuroTopic = "ir-dedo-duro",
            IrVendaTopic = "ir-venda"
        });

        return new KafkaFinanceEventsPublisher(
            options,
            NullLogger<KafkaFinanceEventsPublisher>.Instance,
            (topic, key, value, _) =>
            {
                captured.Topic = topic;
                captured.Key = key;
                captured.Value = value;
                return Task.CompletedTask;
            });
    }

    private sealed class CapturedKafkaMessage
    {
        public string? Topic { get; set; }
        public string? Key { get; set; }
        public string? Value { get; set; }
    }
}
