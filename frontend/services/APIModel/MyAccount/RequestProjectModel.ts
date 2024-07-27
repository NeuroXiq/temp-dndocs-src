export default interface RequestProjectModel {
    projectName: string,
    urlPrefix: string,
    githubUrl: string,
    description: string,
    docfxTemplate: string | null,
    nugetPackages: string[],
    nupkgAutorebuild: boolean,
    mdInclude: boolean,
    mdIncludeReadme: boolean,
    mdIncludeDocs: boolean,
    gitMdRepoUrl: string | null,
    gitMdBranchName: string | null,
    gitMdRelativePathDocs: string | null,
    gitMdRelativePathReadme: string | null,
    mdAutoRebuild: boolean,
    projectFiles: any,
}