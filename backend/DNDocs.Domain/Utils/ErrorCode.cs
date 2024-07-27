using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DNDocs.Domain.Utils
{
    public enum ErrorCode
    {
        GeneralBadRequest = 400,
        EntityNotFound = 404,

        Unauthorized = 401,
        Forbidden = 403,

        InternalServerError = 500
    }
}
