﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DNDocs.Application.Commands.Integration;
using DNDocs.Application.Shared;
using System.Diagnostics;
using DNDocs.API.Model.DTO.Integration;
using DNDocs.Application.Queries.Integration;


namespace DNDocs.Web.Controllers
{
    public class IntegrationController : ApiControllerBase
    {
        private ICommandDispatcher cd;
        private IQueryDispatcher qd;

        public IntegrationController(ICommandDispatcher cd,
            IQueryDispatcher qd)
        {
            this.cd = cd;
            this.qd = qd;
        }

        [HttpPost]
        public async Task<IActionResult> NuGetCreateProject(NugetGenerateProjectModel model)
        {
            return await ApiResult2(cd.DispatchAsync(new NugetCreateProjectCommand
            {
                PackageName = model.PackageName,
                PackageVersion = model.PackageVersion
            }));
        }

        [HttpGet]
        public async Task<IActionResult> NugetCreateProjectCheckStatus(string packageName, string packageVersion)
        {
            return await ApiResult2(qd.DispatchAsync(new GetNugetCreateProjectStatusQuery
            {
                PackageName = packageName,
                PackageVersion = packageVersion
            }));
        }

        [HttpPost]
        public async Task<IActionResult> GithubWebhookCallback()
        {
            string body = null;
            using (StreamReader stream = new StreamReader(Request.Body))
            {
                body = await stream.ReadToEndAsync();
            }

            try
            {
                await ApiResult2(this.cd.DispatchAsync(new GithubWebhookCallbackCommand() { BodyJson = body }));
            }
            catch (Exception e)
            {
                // todo, but probably always return OK to
                // do not broke remote services (github webhooks, dont want
                // to show errors probably on remote repos)
            }

            return Ok();
        }
    }
}
