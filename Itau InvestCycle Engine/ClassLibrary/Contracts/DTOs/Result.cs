using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClassLibrary.Contracts.DTOs;

public readonly record struct Result<TOk, TErr>(TOk? Ok, TErr? Err)
{
    public bool IsSuccess => Ok is not null;

    public static Result<TOk, TErr> Success(TOk ok) => new(ok, default);
    public static Result<TOk, TErr> Failure(TErr err) => new(default, err);
}
