namespace DbTestMonkey.Providers
{
   using System;
   using System.Configuration;
   using System.Data.SqlClient;
   using System.Data.SqlLocalDb;
   using DbTestMonkey.Contracts;
   using DbTestMonkey.Providers.SqlServer;

   public class SqlServerProvider : IDatabaseProvider<SqlConnection>
   {
      public Action<string> LogAction { get; set; }

      public string ConfigurationSectionName 
      { 
         get
         {
            return "sqlServer";
         }
      }

      public void InitialiseDatabaseServer()
      {
         ProviderConfiguration config = 
            (ProviderConfiguration)ConfigurationManager.GetSection("DbTestMonkey/" + ConfigurationSectionName);

         if (config.IsLocalDbInstance)
         {
            var localDbApi = new SqlLocalDbApiWrapper();

            if (!localDbApi.IsLocalDBInstalled())
            {
               throw new InvalidOperationException("LocalDB is not installed on this machine.");
            }

            var localDbProvider = new System.Data.SqlLocalDb.SqlLocalDbProvider();

            string localDbInstanceName = config.LocalDbInstanceName;

            var localDbInstance = localDbProvider.GetOrCreateInstance(localDbInstanceName);

            // TODO: This is questionable logic. Try and source the updated awesome localdb code.
            if (!localDbInstance.GetInstanceInfo().Exists)
            {
               localDbInstance.Stop();
               SqlLocalDbInstance.Delete(localDbInstance);

               localDbInstance = localDbProvider.GetOrCreateInstance(localDbInstanceName);
            }

            localDbInstance.Start();
         }
         else
         {
            throw new NotImplementedException(
               "Non-localdb functionality is not yet implemented. Stand by for further releases.");
         }
      }

      public void SetupDatabase(string databaseName)
      {
         new DacManager(CreateConnection, LogAction)
            .DeployDacPac(databaseName);
      }

      public SqlConnection CreateConnection()
      {
         ProviderConfiguration config =
            (ProviderConfiguration)ConfigurationManager.GetSection("DbTestMonkey/" + ConfigurationSectionName);

         if (string.IsNullOrWhiteSpace(config.ConnectionString) && !config.IsLocalDbInstance)
         {
            throw new InvalidOperationException(
               "Configured connection string was empty or whitespace and database has not been configured as localdb. Connection string is required in this instance.");
         }

         SqlConnection connection = null;

         if (config.IsLocalDbInstance && string.IsNullOrWhiteSpace(config.ConnectionString))
         {
            var localDbProvider = new System.Data.SqlLocalDb.SqlLocalDbProvider();

            connection = localDbProvider.GetInstance(config.LocalDbInstanceName).CreateConnection();
            connection.Open();
         }
         else
         {
            connection = new SqlConnection(config.ConnectionString);
            connection.Open();
         }

         return connection;
      }

      public SqlConnection CreateConnection(string databaseName)
      {
         SqlConnection connection = CreateConnection();

         connection.ChangeDatabase(databaseName);

         return connection;
      }

      public void PurgeDatabaseContents(string databaseName)
      {
         using (var connection = CreateConnection(databaseName))
         using (var command = connection.CreateCommand())
         {
            command.CommandText = @"
                     EXEC sp_MSForEachTable ""SET QUOTED_IDENTIFIER ON; ALTER TABLE ? NOCHECK CONSTRAINT all;""
                     EXEC sp_MSForEachTable ""SET QUOTED_IDENTIFIER ON; DELETE FROM ?""
                     EXEC sp_MSForEachTable ""SET QUOTED_IDENTIFIER ON; ALTER TABLE ? WITH CHECK CHECK CONSTRAINT all""
                     EXEC sp_MSForEachTable ""IF OBJECTPROPERTY(object_id('?'), 'TableHasIdentity') = 1 DBCC CHECKIDENT ('?', RESEED, 0)""";

            command.ExecuteNonQuery();
         }
      }
   }
}