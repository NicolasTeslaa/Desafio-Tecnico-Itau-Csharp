using ClassLibrary.Contracts.DTOs;
using ClassLibrary.Contracts.DTOs.Admin;
using Microsoft.AspNetCore.Mvc;

namespace ScheduledPurchaseEngineService.Controllers;

[ApiController]
[Route("api/admin")]
public sealed class AdminController : ControllerBase
{
    [HttpPost("cesta")]
    [ProducesResponseType(typeof(CadastrarOuAlterarCestaResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public IActionResult CadastrarOuAlterarCesta([FromBody] CadastrarOuAlterarCestaRequest request)
    {
        if (request.Itens.Count != 5)
        {
            return BadRequest(new ApiError(
                $"A cesta deve conter exatamente 5 ativos. Quantidade informada: {request.Itens.Count}.",
                "QUANTIDADE_ATIVOS_INVALIDA"));
        }

        var somaPercentuais = request.Itens.Sum(x => x.Percentual);
        if (somaPercentuais != 100m)
        {
            return BadRequest(new ApiError(
                $"A soma dos percentuais deve ser exatamente 100%. Soma atual: {somaPercentuais}%.",
                "PERCENTUAIS_INVALIDOS"));
        }

        var itensResponse = request.Itens
            .Select(x => new CestaItemResponse(x.Ticker, x.Percentual))
            .ToList();

        var response = new CadastrarOuAlterarCestaResponse(
            CestaId: 2,
            Nome: request.Nome,
            Ativa: true,
            DataCriacao: DateTime.UtcNow,
            Itens: itensResponse,
            CestaAnteriorDesativada: new CestaAnteriorDesativadaResponse(1, "Top Five - Fevereiro 2026", DateTime.UtcNow),
            RebalanceamentoDisparado: true,
            AtivosRemovidos: ["BBDC4", "WEGE3"],
            AtivosAdicionados: ["ABEV3", "RENT3"],
            Mensagem: "Cesta atualizada. Rebalanceamento disparado para 150 clientes ativos.");

        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpGet("cesta/atual")]
    [ProducesResponseType(typeof(CestaAtualResponse), StatusCodes.Status200OK)]
    public IActionResult ConsultarCestaAtual()
    {
        var response = new CestaAtualResponse(
            CestaId: 2,
            Nome: "Top Five - Marco 2026",
            Ativa: true,
            DataCriacao: new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc),
            Itens:
            [
                new CestaAtualItemResponse("PETR4", 25.00m, 37.00m),
                new CestaAtualItemResponse("VALE3", 20.00m, 65.00m),
                new CestaAtualItemResponse("ITUB4", 20.00m, 31.00m),
                new CestaAtualItemResponse("ABEV3", 20.00m, 14.50m),
                new CestaAtualItemResponse("RENT3", 15.00m, 49.00m)
            ]);

        return Ok(response);
    }

    [HttpGet("cesta/historico")]
    [ProducesResponseType(typeof(HistoricoCestasResponse), StatusCodes.Status200OK)]
    public IActionResult HistoricoCestas()
    {
        var response = new HistoricoCestasResponse(
        [
            new CestaHistoricoItemResponse(
                CestaId: 2,
                Nome: "Top Five - Marco 2026",
                Ativa: true,
                DataCriacao: new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc),
                DataDesativacao: null,
                Itens:
                [
                    new CestaItemResponse("PETR4", 25.00m),
                    new CestaItemResponse("VALE3", 20.00m),
                    new CestaItemResponse("ITUB4", 20.00m),
                    new CestaItemResponse("ABEV3", 20.00m),
                    new CestaItemResponse("RENT3", 15.00m)
                ]),
            new CestaHistoricoItemResponse(
                CestaId: 1,
                Nome: "Top Five - Fevereiro 2026",
                Ativa: false,
                DataCriacao: new DateTime(2026, 2, 1, 9, 0, 0, DateTimeKind.Utc),
                DataDesativacao: new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc),
                Itens:
                [
                    new CestaItemResponse("PETR4", 30.00m),
                    new CestaItemResponse("VALE3", 25.00m),
                    new CestaItemResponse("ITUB4", 20.00m),
                    new CestaItemResponse("BBDC4", 15.00m),
                    new CestaItemResponse("WEGE3", 10.00m)
                ])
        ]);

        return Ok(response);
    }

    [HttpGet("conta-master/custodia")]
    [ProducesResponseType(typeof(ContaMasterCustodiaResponse), StatusCodes.Status200OK)]
    public IActionResult ConsultarCustodiaMaster()
    {
        var response = new ContaMasterCustodiaResponse(
            ContaMaster: new ContaMasterInfoResponse(1, "MST-000001", "MASTER"),
            Custodia:
            [
                new CustodiaMasterItemResponse("PETR4", 1, 35.00m, 37.00m, "Residuo distribuicao 2026-02-05"),
                new CustodiaMasterItemResponse("ITUB4", 1, 30.00m, 31.00m, "Residuo distribuicao 2026-02-05"),
                new CustodiaMasterItemResponse("WEGE3", 1, 40.00m, 42.00m, "Residuo distribuicao 2026-02-05")
            ],
            ValorTotalResiduo: 110.00m);

        return Ok(response);
    }
}
