using System.Net;
using Dan.Common.Models;
using Dan.Core.Extensions;
using Dan.Core.Models;
using Dan.Core.Services.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace Dan.Core;

/// <summary>
/// The Azure function rendering the list of accreditations for the given client certificate
/// </summary>
public class FuncAccreditationList
{
    private readonly IRequestContextService _requestContextService;
    private readonly IAccreditationRepository _accreditationRepository;
    private readonly IEvidenceStatusService _evidenceStatusService;

    /// <summary>
    /// Creates an instance of <see cref="FuncAccreditationList"/>
    /// </summary>
    /// <param name="requestContextService"></param>
    /// <param name="accreditationRepository"></param>
    /// <param name="evidenceStatusService"></param>
    public FuncAccreditationList(IRequestContextService requestContextService, IAccreditationRepository accreditationRepository, IEvidenceStatusService evidenceStatusService)
    {
        _requestContextService = requestContextService;
        _accreditationRepository = accreditationRepository;
        _evidenceStatusService = evidenceStatusService;
    }

    /// <summary>
    /// Returns a list of accreditations owned by the organization number in the supplied enterprise certificate
    /// </summary>
    /// <param name="req">
    /// The HTTP request.
    /// </param>
    /// <returns>
    /// The list of non-expired accreditations owned by the current authenticated organization.
    /// </returns>
    [Function("Accreditation")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "accreditations")]
        HttpRequestData req)
    {
        await _requestContextService.BuildRequestContext(req);

        var accreditationsQuery = new AccreditationsQuery
        {
            Requestor = req.GetQueryParam("requestor")
        };

        if (DateTime.TryParse(req.GetQueryParam("changedafter"), out DateTime changedAfter))
        {
            accreditationsQuery.ChangedAfter = changedAfter;
        }

        var accreditations =
            await _accreditationRepository.QueryAccreditationsAsync(accreditationsQuery, _requestContextService.AuthenticatedOrgNumber);

        await _evidenceStatusService.DetermineAggregateStatus(accreditations);

        if (req.GetBoolQueryParam("onlyavailable"))
        {
            accreditations = accreditations.Where(x => x.AggregateStatus == EvidenceStatusCode.Available).ToList();
        }

        // TODO! Should we clear the 

        return req.CreateExternalResponse(HttpStatusCode.OK, accreditations);
    }
}