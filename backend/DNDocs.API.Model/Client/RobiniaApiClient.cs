//using DNDocs.API.Model.DTO;
//using DNDocs.API.Model.DTO.ProjectManage;
//using System.Text.Json;

//namespace DNDocs.APIClient
//{
//    public interface IRobiniaApiClient
//    {
//        Task<IList<ProjectDto>> GetAllProjects();
//        Task DeployProjectFromPrebuildDb(int projectid, byte[] sqlitedb);
//    }

//    public class RobiniaApiClient : IRobiniaApiClient
//    {
//        private HttpClient httpClient;
//        private RobiniaClientConfig config;
//        private Urls urls;
//        private static string jwtToken = null;

//        public RobiniaApiClient(RobiniaClientConfig config)
//        {
//            // should be temporary for testing if not x509 tls needed

//            var httpClientHandler = new HttpClientHandler();
//            httpClientHandler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => { return true; };

//            httpClient = new HttpClient(httpClientHandler);
//            this.config = config;
//            this.urls = new Urls(config.RobiniaWebApiUrl);
//        }

//        public async Task DeployProjectFromPrebuildDb(int projectid, byte[] sqlitedb)
//        {
//            using (var content = new MultipartFormDataContent())
//            {

//                content.Add(new StreamContent(new MemoryStream(sqlitedb)), "sqlitedb", "sqlitedb");
//                content.Add(new StringContent(projectid.ToString()), "projectid");

//                await DoRequest(HttpMethod.Post, this.urls.DeployProjectFromPrebuildDb, content: content);
//            }
//        }

//        public async Task<ProjectDto> GetProject(int id)
//        {
//            return await GetData<ProjectDto>(HttpMethod.Get, urls.GetProject, new Dictionary<string, string> { { "id", id.ToString() } });
//        }

//        public async Task<IList<ProjectDto>> GetAllProjects()
//        {
//            return await GetData<List<ProjectDto>>(HttpMethod.Get, this.urls.GetAllProjects);
//        }

//        //public async Task<byte[]> GetProjectFiles(int projectId)
//        //{
//        //    var response = await DoRequest(
//        //        HttpMethod.Get,
//        //        this.urls.GetProjectFiles,
//        //        urlParams: new Dictionary<string, string>()
//        //        {
//        //            {  "projectid", projectId.ToString() }
//        //        });

//        //    var bytes = await response.Content.ReadAsByteArrayAsync();

//        //    return bytes;
//        //}

//        private async Task<T> GetData<T>(HttpMethod method, string url, Dictionary<string, string> urlParams = null)
//        {
//            var result = await DoRequest(method, url, urlParams: urlParams);
//            var json = await result.Content.ReadAsStringAsync();

//            var deserialized = JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
//            {
//                PropertyNameCaseInsensitive = true
//            });

//            return deserialized;
//        }

//        private async Task<HttpResponseMessage> DoRequest(
//            HttpMethod method,
//            string url,
//            Dictionary<string, string> urlParams = null,
//            HttpContent content = null)
//        {
//            if (jwtToken == null) await this.Authenticate();

//            if (urlParams != null)
//            {
//                var queryParams = await (new FormUrlEncodedContent(urlParams)).ReadAsStringAsync();
//                url += $"?{queryParams}";
//            }

//            var request = new HttpRequestMessage(method, url);

//            request.Headers.Add("Authorization", "Bearer " + jwtToken);

//            if (content != null) request.Content = content;

//            var result = await this.httpClient.SendAsync(request);

//            if (!result.IsSuccessStatusCode)
//            {
//                throw new Exception("RobiniaApiClient: Request not success. Status Code: " + result.StatusCode.ToString());
//            }

//            return result;
//        }

//        private async Task Authenticate()
//        {
//            var a = new FormUrlEncodedContent(new Dictionary<string, string> { { "password", this.config.AdminPassword } });
//            var response = await httpClient.PostAsync(this.urls.AuthenticateUrl, a);

//            var token = await response.Content.ReadAsStringAsync();

//            jwtToken = token;
//        }
//    }
//}
