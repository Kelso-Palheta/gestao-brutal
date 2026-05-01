// TODO: Custom SQLite driver obsoleto. Usar SQLiteConfiguration.Standard (driver padrão) nos testes.
// Arquivo comentado para evitar erros de compilação com NHibernate 5.4+

/*
using System.Data.Common;
using Microsoft.Data.Sqlite;
using NHibernate.Driver;
using NHibernate.SqlTypes;

namespace BatatasFritas.Infrastructure;

public class MicrosoftDataSqliteDriver : ReflectionBasedDriver
{
    public MicrosoftDataSqliteDriver()
        : base(
            typeof(SqliteConnection).Assembly.FullName!,
            typeof(SqliteConnection).FullName!,
            typeof(SqliteCommand).FullName!)
    {
    }

    public override bool UseNamedPrefixInParameter => true;
    public override string NamedPrefix => "@";
    public override bool SupportsMultipleOpenReaders => false;
    public override bool SupportsMultipleQueries => true;
    public override bool RequiresTimespanForTime => true;
    public override bool SupportsPreparingCommands => true;
    public override bool SupportsSqlBlockComment => true;
    public override char OpenQuote => '"';
    public override char CloseQuote => '"';

    public override IResultSetsCommand GetResultSetsCommand()
    {
        return new MicrosoftDataSqliteResultSetsCommand();
    }

    protected override void InitializeParameter(IDbDataParameter dbParam, string name, SqlType sqlType)
    {
        base.InitializeParameter(dbParam, name, sqlType);
    }

    private class MicrosoftDataSqliteResultSetsCommand : IResultSetsCommand
    {
        public CommandBehavior AdjustCommandBehavior(CommandBehavior commandBehavior)
        {
            return commandBehavior;
        }
    }
}
*/
