
using DNDocs.Application.Commands.Projects;
using DNDocs.Application.Shared;
using DNDocs.Docs.Api.Client;
using DNDocs.Domain.Entity.App;
using DNDocs.Domain.Enums;
using DNDocs.Domain.Service;
using DNDocs.Domain.Services;
using DNDocs.Domain.UnitOfWork;
using DNDocs.Domain.Utils;
using DNDocs.Domain.Utils.Docfx;
using DNDocs.Shared.Utils;
using Ganss.Xss;
using Markdig;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace DNDocs.Application.CommandHandlers.Projects
{
    internal class BuildProjectHandler : CommandHandlerA<BuildProjectCommand>
    {
        private IAppUnitOfWork appUow;
        private ISystemMessages systemMessages;
        private IAppManager appManager;
        private IDocfxManager docfxManager;
        private IDDocsApiClient ddocsApiClient;
        private INugetRepositoryFacade nugetRepositoryFacade;

        public BuildProjectHandler(
            IAppUnitOfWork appUow,
            ISystemMessages systemMessages,
            IAppManager appManager,
            IDocfxManager docfxManager,
            IDDocsApiClient ddocsApiClient,
            INugetRepositoryFacade nugetRepositoryFacade)
        {
            this.appUow = appUow;
            this.systemMessages = systemMessages;
            this.appManager = appManager;
            this.docfxManager = docfxManager;
            this.ddocsApiClient = ddocsApiClient;
            this.nugetRepositoryFacade = nugetRepositoryFacade;
        }

        class BuildChainContext
        {
            public Project Project
            {
                get { return project; }
                set
                {
                    if (project != null) throw new InvalidOperationException();
                    project = value;
                }
            }

            public IOSTempFolder DocfxBuildFolder
            {
                get { return docfxBuildFolder; }
                set { docfxBuildFolder = docfxBuildFolder == null ? value : throw new InvalidOperationException(); }
            }

            public IDocfxManager DocfxManager
            {
                get { return docfxManager; }
                set { docfxManager = docfxManager == null ? value : throw new InvalidOperationException(); }
            }

            private Project project = null;
            private IOSTempFolder docfxBuildFolder;
            private IDocfxManager docfxManager;
        }

        public override async Task Handle(BuildProjectCommand command)
        {
            logger.LogInformation("starting to build project id: {0}", command.ProjectId);
            var context = new BuildChainContext();
            var p = await this.appUow.GetSimpleRepository<Project>().GetByIdCheckedAsync(command.ProjectId);
            // load related data ef will setup 
            this.appUow.GetSimpleRepository<NugetPackage>().Query().Where(t => t.ProjectId == p.Id).ToList();

            Validation.ThrowError(p.StateDetails == ProjectStateDetails.Building, "Project is building now, cannot run build again");
            context.Project = p;
            p.LastDocfxBuildTime = DateTime.UtcNow;

            List<Action<BuildChainContext>> steps = new List<Action<BuildChainContext>>()
            {
                Step_DocfxInit,
                Step_FetchDataIntoDocfxProject,
                Step_DocfxBuildCurrentState,
                Step_SendDocfxBuildResultToDndocs,
                Step_CleanDocfxAfterBuild,
                Step_BuildCompletedSuccess
            };

            Exception exception = null;

            foreach (var step in steps)
            {
                try
                {
                    logger.LogInformation("starting to execute build step project id: {0}, step: {1}", command.ProjectId, step.Method.Name);
                    step(context);
                }
                catch (Exception e)
                {
                    logger.LogError(e, "failed build project on step: {0}, projectid: {1}", step.GetMethodInfo().Name, command.ProjectId);
                    
                    p.State = ProjectState.NotActive;
                    p.StateDetails = ProjectStateDetails.BuildFailed;
                    p.LastDocfxBuildErrorDateTime = DateTime.UtcNow;
                    p.StateDetails = ProjectStateDetails.BuildFailed;
                    p.State = ProjectState.NotActive;
                    p.LastDocfxBuildErrorLog = Helpers.ExceptionToStringForLogs(e);
                    await appUow.SaveChangesAsync();

                    Validation.ThrowError($"Failed to build project on step: {step.GetMethodInfo().Name}");
                }
            }

            await appUow.SaveChangesAsync();
        }


        void Step_BuildCompletedSuccess(BuildChainContext c)
        {
            var project = c.Project;
            project.LastDocfxBuildTime = DateTime.UtcNow;
            project.State = ProjectState.Active;
            project.StateDetails = ProjectStateDetails.Ready;

            systemMessages.Success(project, "Build project completed successfully", $"Build project completed successfully");
        }

        void Step_CleanDocfxAfterBuild(BuildChainContext c)
        {
            var docfxManager = c.DocfxManager;
            var project = c.Project;
            docfxManager.CleanAfterBuild();

            c.DocfxBuildFolder.Dispose();
        }

        void Step_SendDocfxBuildResultToDndocs(BuildChainContext c)
        {
            var project = c.Project;

            using (var tempZipFolder = appManager.CreateTempFolder())
            {
                var zipOsPath = Path.Combine(tempZipFolder.OSFullPath, "site.zip");
                ZipFile.CreateFromDirectory(docfxManager.OSPathSiteDirectory, zipOsPath);

                using (var zipStream = new FileStream(zipOsPath, FileMode.Open, FileAccess.Read))
                {
                    logger.LogInformation("Starting createorreplace on docs.dndocs. id: {0} name: {1} urlprefix: {2} project_type: {3}", project.Id, project.ProjectName, project.UrlPrefix, project.ProjectType);

                    var task = ddocsApiClient.Management_CreateProject(
                        project.Id,
                        project.ProjectName,
                        $"",
                        project.UrlPrefix,
                        project.PVGitTag,
                        project.NugetOrgPackageName,
                        project.NugetOrgPackageVersion,
                        (int)project.ProjectType,
                        zipStream);

                    task.Wait();

                    logger.LogInformation("completed createorreplace on docs.dndocs. id: {0} name: {1} urlprefix: {2} project_type: {3} ", project.Id, project.ProjectName, project.UrlPrefix, project.ProjectType);

                    var result = task.Result;
                }
            }
        }

        void Step_FetchDataIntoDocfxProject(BuildChainContext c)
        {
            var project = c.Project;
            FetchNugetXmlAndDlls(project.ProjectNugetPackages, docfxManager.OSPathBinDir);
            FetchMdDocs(project, docfxManager.OSPathIndexHtml, docfxManager.OSPathArticlesDir);
        }

        void Step_DocfxBuildCurrentState(BuildChainContext c)
        {
            docfxManager.AutogenerateArticlesTOC();
            docfxManager.SetTemplate(c.Project.DocfxTemplate);
            docfxManager.Build();
        }

        void Step_DocfxInit(BuildChainContext c)
        {
            var tempFolder = appManager.CreateTempFolder();
            var project = c.Project;

            systemMessages.Trace(project, "Build project started", $"Starting to build a project {project}");
            project.State = ProjectState.NotActive;
            project.StateDetails = ProjectStateDetails.Building;

            docfxManager.Init(tempFolder.OSFullPath, project);

            c.DocfxBuildFolder = tempFolder;
            c.DocfxManager = docfxManager;
        }

        void FetchNugetXmlAndDlls(List<NugetPackage> newPackages, string outputFolder)
        {
            foreach (var package in newPackages)
            {
                try
                {
                    var packageBinData = nugetRepositoryFacade.FetchDllAndXmlFromPackage(package.IdentityId, package.IdentityVersion);

                    foreach (var item in packageBinData)
                    {
                        File.WriteAllBytes(Path.Combine(outputFolder, item.FileName), item.ByteData);
                    }
                }
                catch (Exception e)
                {
                    logger.LogError(e, "failed to fetch nuget packages");

                    throw e;
                }
            }
        }

        private IGit GetGit(Project project)
        {
            IGit git = appManager.OpenGitRepo(project.GitMdRepoUrl);
            git.CheckoutBranch(project.GitMdBranchName);
            git.Pull();

            return git;
        }

        private void FetchMdDocs(Project project, string outputReadmeFilePath, string outputArticlesFolder)
        {
            if (string.IsNullOrWhiteSpace(project.GitMdRepoUrl)) return;

            using (var git = GetGit(project))
            {
                FetchMdDocs2(project, outputReadmeFilePath, outputArticlesFolder, git);
            }
        }

        private void FetchMdDocs2(Project project, string outputReadmeFilePath, string outputArticlesFolder, IGit git)
        {
            if (string.IsNullOrWhiteSpace(project.GitMdRelativePathReadme) && string.IsNullOrWhiteSpace(project.GitMdRelativePathDocs))
                return;

            Validation.AppEx(string.IsNullOrWhiteSpace(project.GitDocsCommitHash), "git commit hash empty cannot build docs (application exception)");

            systemMessages.Trace(project,
                "Starting to prepare MD docs.",
                $"System started to prepare MD docs.\r\nMD docs git commit hash: {project.GitDocsCommitHash}");

            git.PruneRemote();
            git.FetchAll();
            git.CheckoutBranch(project.GitMdBranchName);
            git.Pull();
            git.CheckoutCommit(project.GitDocsCommitHash);
            string projectRepoOsPath = git.RepoOSPath;

            // TODO: this  is expensive to create,
            // but not sure if this is thread safe, maybe create static prop and use semaphore for building all concurrent project??
            // only in single thread?

            var pp = (new Markdig.MarkdownPipelineBuilder())
                .UseAdvancedExtensions()
                .Build();

            var htmlSanitizer = new HtmlSanitizer();

            if (!string.IsNullOrWhiteSpace(project.GitMdRelativePathDocs))
            {
                // first directory has all project files
                var docsRelPath = project.GitMdRelativePathDocs;
                string articlesInRepoFolderOSPath = Path.Combine(projectRepoOsPath, docsRelPath);

                if (!Directory.Exists(articlesInRepoFolderOSPath))
                {
                    var userMsg = $"Directory '{docsRelPath}' (relative path) does not exists in repo folder\n " +
                       $"{project}\nGit Branch Name: {project.GitMdBranchName}\nGitDocsCommitHash: {project.GitDocsCommitHash}";

                    logger.LogError($"{userMsg}\nospathRepo:{projectRepoOsPath};\nGithubMdRelativePathDocs: {docsRelPath}");
                    Validation.ThrowError(true, userMsg);
                }

                string[] articles = Directory.GetFiles(articlesInRepoFolderOSPath, "*.md", SearchOption.AllDirectories);

                foreach (var mdOsPath in articles)
                {
                    var md = File.ReadAllText(mdOsPath);
                    var mdHtml = Markdig.Markdown.ToHtml(md, pp);
                    mdHtml = htmlSanitizer.Sanitize(mdHtml);
                    var fileRelativePath = mdOsPath.Substring(articlesInRepoFolderOSPath.Length + 1);

                    var saveOsPath = Path.Combine(outputArticlesFolder, fileRelativePath);
                    var saveOsDir = Path.GetDirectoryName(saveOsPath);

                    if (!Directory.Exists(saveOsDir))
                        Directory.CreateDirectory(saveOsDir);

                    File.WriteAllText(saveOsPath, mdHtml);
                }
            }

            if (!string.IsNullOrWhiteSpace(project.GitMdRelativePathReadme))
            {
                var readmeRelPath = project.GitMdRelativePathReadme;
                var readmeOsPath = Path.Combine(projectRepoOsPath, readmeRelPath);
                Validation.ThrowError(!File.Exists(readmeOsPath), $"Readme file '{readmeRelPath}' does not exists in repo folder");
                string readmeMdFileContent = File.ReadAllText(readmeOsPath);

                var homeContentHtml = Markdig.Markdown.ToHtml(readmeMdFileContent, pp);
                File.WriteAllText(outputReadmeFilePath, homeContentHtml);
            }
        }
    }
}
