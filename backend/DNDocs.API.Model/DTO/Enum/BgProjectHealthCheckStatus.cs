namespace DNDocs.API.Model.DTO.Enum
{
    public enum BgProjectHealthCheckStatus
    {
        HttpGetOk = 1,
        HttpGetFail = 2,
        SystemFailedToInvokeGet = 3,
        IgnoredBecauseStatusNotActive =  4
    }
}
