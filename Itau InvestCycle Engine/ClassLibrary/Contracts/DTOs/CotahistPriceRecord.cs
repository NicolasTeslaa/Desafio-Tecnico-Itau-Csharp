using System;
using System.Collections.Generic;
using System.Text;

namespace ClassLibrary.Contracts.DTOs;

public sealed record CotahistPriceRecord(
    string Symbol,
    DateOnly TradeDate,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    decimal Volume
);
