﻿using DNDocs.Domain.Entity.App;
using DNDocs.API.Model.DTO.Admin;

namespace DNDocs.Domain.Repository
{
    public interface IHttpLogRepository : IRepository<HttpLog>
    {
        IList<HttpLog> TableDataLogs(TableDataRequest request);
        int UniqueIP(DateTime maxAge);
    }
}
