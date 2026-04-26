using Microsoft.Extensions.Diagnostics.HealthChecks;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace BatatasFritas.API.HealthChecks;

public sealed class NHibernateHealthCheck : IHealthCheck
{
    private readonly NHibernate.ISession _session;

    public NHibernateHealthCheck(NHibernate.ISession session)
    {
        _session = session;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            _session.CreateSQLQuery("SELECT 1").UniqueResult();
            return Task.FromResult(HealthCheckResult.Healthy("DB reachable"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("DB unreachable", ex));
        }
    }
}
