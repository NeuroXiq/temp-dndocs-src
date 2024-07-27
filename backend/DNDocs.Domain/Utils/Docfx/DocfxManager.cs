using Microsoft.Extensions.Logging;
using DNDocs.Shared.Log;
using Microsoft.Extensions.Options;
using DNDocs.Domain.Entity.App;
using DNDocs.Shared.Configuration;
using System.Text;
using System.Text.Json;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using DNDocs.Resources;

namespace DNDocs.Domain.Utils.Docfx
{
    public interface IDocfxManager
    {
        string DocfxJsonFileContent { get; }
        string OSPathSiteDirectory { get; }
        string OSPathBinDir { get; }
        string OSPathArticlesDir { get; }
        string OSPathIndexHtml { get; }
        string RootDirectory { get; }

        void Init(string directory, Project p);
        void Build();
        void SetTemplate(string templateName);
        bool TemplateExists(string templateName);

        // void SetHomepageContent(string content);
        void CleanAfterBuild();
        void AutogenerateArticlesTOC();
    }

    public class DocfxManager : IDocfxManager
    {
        const string _site = "_site";
        private ILog<DocfxManager> logger;
        private IOptions<DNDocsSettings> dsettings;
        private ISystemProcess process;
        private IAppManager appManager;

        // private string tempFolder;
        // private string binFolder;
        private string pathSiteDirectory;
        private string ospathApiDir;

        private string _rootDirectory = null;
        private string rootDirectory
        {
            get
            {
                return _rootDirectory ?? throw new InvalidOperationException("Root dir not set. Call 'Open' method");
            }
        }
        private DocfxJson docfxJson = null;
        private RobiniaProjectInfoJson robiniaProjectInfo = null;

        string filePath_DocfxJson;

        public string DocfxJsonFileContent { get { return File.ReadAllText(filePath_DocfxJson); } }
        public string OSPathSiteDirectory { get { return pathSiteDirectory; } }
        public string OSPathApiDirectory { get { return ospathApiDir ?? throw new RobiniaException("internal: not initialized apidir"); } }
        public string OSPathArticlesDir { get { return Path.Combine(rootDirectory, "articles"); } }
        public string RootDirectory { get { return this.rootDirectory; } }
        public string OSPathBinDir => Path.Combine(rootDirectory, "bin");
        public string OSPathIndexHtml => Path.Combine(rootDirectory, "index.md");

        public RobiniaProjectInfoJson RobiniaProjectInfo
        {
            get
            {
                if (robiniaProjectInfo == null)
                {
                    robiniaProjectInfo = JsonSerializer.Deserialize<RobiniaProjectInfoJson>(DocfxJsonFileContent);
                }

                return robiniaProjectInfo;
            }
        }

        public DocfxManager(
            ISystemProcess process,
            IAppManager appManager,
            IOptions<DNDocsSettings> robiniaSettings,
            ILog<DocfxManager> logger)
        {
            this.logger = logger;
            this.dsettings = robiniaSettings;
            this.process = process;
            this.appManager = appManager;
        }

        private void SetRootDirectory(string directory)
        {
            if (!Directory.Exists(directory))
                throw new RobiniaException($"Cannot open docfx project because directory does not exist. Directory: '{directory ?? ""}'");

            this._rootDirectory = directory;
            this.pathSiteDirectory = Path.Combine(rootDirectory, "_site");
            this.ospathApiDir = Path.Combine(rootDirectory, "api");
            this.filePath_DocfxJson = Path.Combine(rootDirectory, "docfx.json");
        }

        private void DeleteCurrentFolder(string directory)
        {
            if (!directory.EndsWith("docfx_project"))
                throw new InvalidOperationException("not ends with 'docfx_project' throws for safety ");

            Directory.Delete(directory, true);
        }

        public void Init(string directory, Project project)
        {
            directory = Path.Combine(directory, "docfx_project");

            if (Directory.Exists(directory))
            {
                DeleteCurrentFolder(directory);
            }

            Directory.CreateDirectory(directory);
            this.SetRootDirectory(directory);

            // create dirs structure
            var substringLen = AppResources.DocfxInitDir.Length;
            var allDirs = Directory.GetDirectories(AppResources.DocfxInitDir, "*", SearchOption.AllDirectories)
                .Select(t => t.Substring(substringLen))
                .Order()
                .ToList();

            var allFiles = Directory.GetFiles(AppResources.DocfxInitDir, "*", SearchOption.AllDirectories)
                .Select(t => t.Substring(substringLen + 1))
                .ToList();

            foreach (var dir in allDirs) Directory.CreateDirectory(OSFullPath(dir));
            foreach (var file in allFiles)
            {
                File.Copy(Path.Combine(AppResources.DocfxInitDir, file), OSFullPath("/" + file));
            }

            Directory.CreateDirectory(OSFullPath("/bin"));

            this.Open(directory);

            var ngm = new DocfxJson.NBuild.NGlobalMetadata();

            string wwwrobinia = dsettings.Value.PublicDNDocsDNSName;
            string robiniaName = dsettings.Value.PublicDNDocsName;
            var options = dsettings.Value.Strings;
            var versionsHtml = string.Empty;

            //ngm._appFooter =
            //    "" +
            //    "<div style=\"width: 100%\">" +
            //    "<div style=\"display: flex; align-items: center; \">" + 
            //    $"    <div style=\"flex: 1;\">{versionsHtml}</div>" +
            //    "    <div>" +
            //    $"     <a href=\"https://{wwwrobinia}\">Hosted with {robiniaName}</a>" +
            //    "      <span style=\"display: inline-block; margin: 0 0.5em;\">|</span>" +
            //    "      <a href=\"https://dotnet.github.io/docfx\">Docfx</a>" +
            //    "    </div>" +
            //    "    <div style=\"flex: 1;\"></div>" +
            //    "</div>" +
            //    $"<script type=\"text/javascript\" src=\"{options.DndocsDocfxScriptUrl}\"></script>" +
            //    "</div>" +
            //    "";

           var info =  Newtonsoft.Json.JsonConvert.SerializeObject(new
           {
               projectId = project.Id,
               isVersioning = project.PVProjectVersioningId.HasValue,
           });

            ngm._appFooter =
                $"<div id=\"id-dndocs-footer\">" +
                "<script type=\"application/json\" id=\"id-dndocs-info-json\">" +
                $"{info}" +
                "</script>" +
                $"<script type=\"text/javascript\" src=\"{options.DndocsDocfxScriptUrl}\"></script>" +
                "</div>" +
                "";

            string appTitle = "";
            switch (project.ProjectType)
            {
                case ProjectType.Singleton: appTitle = $"{project.ProjectName} | {dsettings.Value.PublicDNDocsName}";  break;
                case ProjectType.Version: appTitle = $"{project.ProjectName} {project.PVGitTag} | {dsettings.Value.PublicDNDocsName}"; break;
                case ProjectType.NugetOrg: appTitle = $"{project.NugetOrgPackageName} {project.NugetOrgPackageVersion} | {dsettings.Value.PublicDNDocsName}"; break; 
                default: throw new NotImplementedException("project type not implemented _apptitle");
            }

            ngm._enableSearch = true;
            ngm._appTitle = project.ProjectName;

            docfxJson.Build.GlobalMetadata = ngm;
            SaveDocfxJson();

            var sb = new StringBuilder();
            sb.AppendLine($"# Welcome on {project.ProjectName}!");
            sb.AppendLine("---");
            sb.AppendLine($"- Project github page:  [{project.ProjectName}]({project.GithubUrl} \"{ project.GithubUrl }\")");
            sb.AppendLine("-  See: ");
            sb.AppendLine("-  Articles section for documentation");
            sb.AppendLine("-  API section for API objects explorer");
            sb.AppendLine("---");

            var homepageContent = sb.ToString();

            SetHomepageContent(homepageContent);

            sb.Clear();
            sb.Append($"# {project.ProjectName} API browser");

            var apiHomepage = sb.ToString();
            ContentUpdateFile("/index.md", apiHomepage);
            ContentUpdateFile("/api/index.md", "# Api Documentation");

            this.Commit();
        }

        void SaveDocfxJson()
        {
            var serializeOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            };

            var json = JsonSerializer.Serialize(this.docfxJson, serializeOptions);
            File.WriteAllText(this.filePath_DocfxJson, json);
        }

        public bool TemplateExists(string templateName)
        {
            var templatesDir = dsettings.Value.OSPathDocfxTemplatesDir;
            Validation.AppEx(string.IsNullOrWhiteSpace(templateName), nameof(templateName));
            var osPathTemplate = Path.Combine(templatesDir, templateName);

            return Directory.Exists(osPathTemplate);
        }

        public void SetTemplate(string templateName)
        {
            if (!string.IsNullOrWhiteSpace(templateName))
            {
                var templatesDir = dsettings.Value.OSPathDocfxTemplatesDir;
                Validation.AppEx(!TemplateExists(templateName), $"Template '{templateName}' does not exists in templates dir: {templatesDir}");

                Validation.AppEx(string.IsNullOrWhiteSpace(templateName), nameof(templateName));
                var osPathTemplate = Path.Combine(templatesDir, templateName);


                docfxJson.Build.Template.Add(osPathTemplate);
            }
            else
            {
            }

            SaveDocfxJson();
        }

        public void Open(string directory)
        {
            this.SetRootDirectory(directory);

            docfxJson = DocfxJson.FromJson(File.ReadAllText(filePath_DocfxJson));
        }

        public void Build()
        {
            // during investigation other bug code from here was moved to ConsoleTools project,
            // but moving this didn;t solve that bug (other solution found). But I leave this code in separate
            // project anyway. (Code from ConsoleTools can be copied here directly and should work ok)
            //
            // But not sure:
            // 1. All ..DocAsCode... is static classes/methods - is this thread safe??
            // 2. additionally it creates its own 'templates' folder that is not needed
            // 3.Lots of console logs (not sure if the are mixed if multiple thread are running, is this thread-safe? to use this static class)
            // so for now leaving it in separate process seems to be safer

            logger.LogTrace("enter build");
            string docfxJsonPath = Path.Combine(rootDirectory, "docfx.json");
            string args = $@"{dsettings.Value.ConsoleToolsDllFilePath} docfx {docfxJsonPath}";
            process.Start("dotnet", args, 120, out var exitcode, out var stdo, out var stderr);

        }

        public void Commit()
        {
            
        }

        //public void BinSetFiles(IList<BlobData> files)
        //{
        //    var bpath = OSFullPath($"/bin");
        //    var dirinfo = new DirectoryInfo(bpath);
        //    foreach (var f in dirinfo.GetFiles()) f.Delete();

        //    foreach (var file in files)
        //    {
        //        Validation.ThrowError(
        //            string.IsNullOrWhiteSpace(file.OriginalName),
        //            "DfxManager: File name null or white space");

        //        Validation.ThrowError(
        //            !file.OriginalName.All(c =>
        //                (c >= 'A' && c <= 'Z') ||
        //                (c >= 'a' && c <= 'z') ||
        //                (c >= '0' && c <= '9') ||
        //                (c == '.') ||
        //                (c == '-')),
        //            "DfxManager: cannot set bin files, invalid char in filename");

        //        Validation.ThrowError(
        //            (!file.OriginalName.EndsWith(".dll") && !file.OriginalName.EndsWith(".xml")),
        //            "DfxManager: Invalid files (not xml and dll).");

        //        Validation.ThrowError(
        //            file.OriginalName.Contains(".."),
        //            "DfxManager: file name double dots '..'");

        //        var binpath = OSFullPath($"/bin/{file.OriginalName}");

        //        File.WriteAllBytes(binpath, file.ByteData);
        //    }
        //}


        public void ContentUpdateFile(string vPath, string newContent)
        {
            ValidateVPathFile(vPath, nameof(vPath), mustExists: true);

            Validation.ThrowError(
                    !vPath.EndsWith(".md") &&
                    !vPath.EndsWith("toc.yml"),
                    "This file is not .md and toc.yml file");

            Validation.ThrowError(
                    newContent != null && newContent.Length > (5000000),
                    "Max file size 5000000 bytes");

            var filename = vPath.Split('/').Last();
            var parentVPath = vPath.Substring(0, vPath.Length - filename.Length);

            var ospath = OSFullPath(vPath);

            File.WriteAllText(ospath, newContent);
        }

        public void CleanAfterBuild()
        {
            DeleteCurrentFolder(this.RootDirectory);

            // preserve this files, they are needed
            // var gitignorePath = OSFullPath("/api/.gitignore");
            // var indexPath = OSFullPath("/api/index.md");
            // var tocPath = OSFullPath("/api/toc.yml");
            // 
            // var gitignore = File.ReadAllBytes(gitignorePath);
            // var index = File.ReadAllBytes(indexPath);
            // var toc = File.ReadAllBytes(tocPath);
            // 
            // Directory.Delete(OSPathSiteDirectory, true);
            // Directory.Delete(OSPathApiDirectory, true);
            // 
            // Directory.CreateDirectory(OSPathApiDirectory);
            // File.WriteAllBytes(gitignorePath, gitignore);
            // File.WriteAllBytes(indexPath, index);
            // File.WriteAllBytes(tocPath, toc);
        }

        //vdirectory == virtual directory
       

        private string VCombine(string p1, string p2)
        {
            p1 = p1.TrimEnd('/');
            p2 = p2.TrimStart('/');

            return $"{p1}/{p2}";
        }

        private string OSFullPath(string vpath)
        {
            vpath = vpath.TrimStart('/', '\\');
            vpath = vpath.Replace('/', Path.DirectorySeparatorChar);

            var result = Path.Combine(this.rootDirectory, vpath);

            if (!result.StartsWith(this.rootDirectory))
                throw new RobiniaException("Safety: something is bad with directory");

            return result;
        }

        void ValidateVPathShared(
            string vpath,
            string nameofArg)
        {
            bool isvalid = !string.IsNullOrWhiteSpace(vpath);
            isvalid &= vpath.StartsWith("/articles") || vpath == "/index.md" || vpath == "/api/index.md";


            Validation.ThrowError(!isvalid, $"Invalid path '{vpath}'");
            Validation.ThrowError(
                !vpath.All(d => ((d >= 'a' && d <= 'z') || (d >= '0' && d <= '9') || d == '/' || d == '.' || d == '-')),
                $"Invalid character in path. '{vpath}'");

            Validation.ThrowError(
                vpath.Length > 80,
                $"Path is too big (max 80 chars) '{vpath}'");
        }

        void ValidateVPathDir(string vpath, string argname, bool mustExists = false)
        {
            ValidateVPathShared(vpath, argname);
            Validation.ThrowError(
                !vpath.All(d => ((d >= 'a' && d <= 'z') || d == '/' || d == '-' || (d >= '0' && d <= '9'))),
                "Invalid character in path. Valid chars: ['a-z', '/', '-']");

            var split = vpath.Split("/");

            for (int i = 1; i < split.Length; i++)
                Validation.ThrowError(split[i].Length < 1, "Empty directory of empty subdirectory");

            var osfull = OSFullPath(vpath);

            if (mustExists)
            {
                Validation.ThrowError(!Directory.Exists(osfull), $"File does not exists, VPAHT: '{vpath}'");
            }
        }

        void ValidateVPathFile(string vpath, string argName, bool mustExists = false)
        {
            ValidateVPathShared(vpath, argName);

            Validation.ThrowError(string.IsNullOrWhiteSpace(vpath), "null or empty path");

            Validation.ThrowError(
                !vpath.EndsWith(".md") &&
                !vpath.EndsWith(".yml"),
                $"Invalid extension. file: {vpath}");

            Validation.ThrowError(vpath.Count(c => c == '.') > 1, $"InvalidName contains '.'. ''{vpath}''");

            var split = vpath.Split('.');
            var filename = split[1].Split('/').Last();
            Validation.ThrowError(filename.Length < 1, $"Empty file name {vpath}");

            var osfull = OSFullPath(vpath);

            if (mustExists)
            {
                Validation.ThrowError(!File.Exists(osfull), $"file not exists '{vpath}'");
            }
        }

        public void ContentMoveDirectory(string vpathSrc, string vpathDest)
        {
            ValidateVPathDir(vpathSrc, "vpathSrc", mustExists: true);
            ValidateVPathDir(vpathDest, "vpathDest", mustExists: false);

            var osSrc = OSFullPath(vpathSrc);
            var osDest = OSFullPath(vpathDest);

            Validation.ThrowError(!Directory.Exists(osSrc), "Source does not exist");
            Validation.ThrowError(Directory.Exists(osDest), "Destination already exists");

            Directory.Move(osSrc, osDest);
        }

        public void SetHomepageContent(string content)
        {
            ContentUpdateFile("/index.md", content);
        }

        public void AutogenerateArticlesTOC()
        {
            var files = Directory.GetFiles(OSPathArticlesDir, "*.*", SearchOption.TopDirectoryOnly);

            if (!files.Any(f => Path.GetFileName(f).ToLower() == "toc.yml"))
            {
                File.Create(Path.Combine(OSPathArticlesDir, "toc.yml")).Dispose();
            }

            File.WriteAllText(Path.Combine(OSPathArticlesDir, "toc.yml"), "");

            var toc = GenerateTOC(OSPathArticlesDir);

            var serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            var yaml = serializer.Serialize(toc);

            File.WriteAllText(Path.Combine(OSPathArticlesDir, "toc.yml"), yaml);
        }

        public IList<TOCItem> GenerateTOC(string parentFolder)
        {
            var currentFiles = Directory
                .GetFiles(parentFolder, "*.md", SearchOption.TopDirectoryOnly).OrderBy(t => t.Length)
                .Select(t => t.Substring(OSPathArticlesDir.Length + 1))
                .ToList();

            var currentDirs = Directory.GetDirectories(parentFolder, "*", SearchOption.TopDirectoryOnly);

            List<TOCItem> result = new List<TOCItem>();

            foreach (var f in currentFiles)
            {
                var fileRow = new TOCItem()
                {
                    Name = TOCName(Path.GetFileName(f)),
                    Href = f.Replace('\\', '/')
                };

                result.Add(fileRow);
            }

            foreach (var dir in currentDirs)
            {
                var di = new DirectoryInfo(dir);
                var dirRow = new TOCItem()
                {
                    Name = TOCName(di.Name),
                    Items = GenerateTOC(dir)
                };

                result.Add(dirRow);
            }

            result = result.OrderBy(t => t.Name).ToList();

            return result;
        }

        private string TOCName(string filename)
        {
            filename = filename.EndsWith(".md") ? filename.Substring(0, filename.Length - 3) : filename;

            if (filename.Length < 2) return filename;

            filename = char.ToUpper(filename[0]) + filename.Substring(1);
            var tosplit = new char[] { '-', '.', ' ' };
            filename = string.Join(" ", filename.Split(tosplit));

            return filename;
        }
    }
}
