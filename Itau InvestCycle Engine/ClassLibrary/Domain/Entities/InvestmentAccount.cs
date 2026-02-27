using Itau.InvestCycleEngine.Common.Auditing;

namespace Itau.InvestCycleEngine.Domain.Entities;

public sealed class InvestmentAccount : AuditableEntity<Guid>
{
    public Guid OwnerUserId { get; private set; }
    public string BrokerName { get; private set; } = "";
    public string AccountCode { get; private set; } = "";

    private InvestmentAccount() { }

    public InvestmentAccount(Guid id, Guid ownerUserId, string brokerName, string accountCode)
    {
        Id = id;
        OwnerUserId = ownerUserId;
        BrokerName = brokerName;
        AccountCode = accountCode;
    }
}
