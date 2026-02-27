namespace Itau.InvestCycleEngine.Domain.Enums;

public enum AssetType { Stock = 1, Etf = 2, Crypto = 3, Fund = 4, Bdr = 5 }
public enum PlanStatus { Active = 1, Paused = 2, Cancelled = 3 }
public enum FrequencyType { Daily = 1, Weekly = 2, Monthly = 3 }
public enum ExecutionStatus { Executed = 1, Failed = 2, Skipped = 3 }
public enum CurrencyCode { BRL = 1, USD = 2, EUR = 3 }
public enum TipoConta { Master = 1, Filhote = 2}
public enum TipoMercado { LOTE = 1, FRACIONARIO = 2 }
public enum TipoRebalanceamento { MUDANCA_CESTA = 1, DESVIO = 2}
public enum TipoIR { DEDO_DURO = 1, IR_Venda = 2}