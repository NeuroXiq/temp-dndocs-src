using DNDocs.Application.Shared;
using DNDocs.API.Model.DTO.MyAccount;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DNDocs.Application.Queries.MyAccount
{
    public class GetGithubRepositoriesQuery : Query<IList<GithubRepositoryDto>>
    {
        public bool FlushCache { get; set; }
    }
}
