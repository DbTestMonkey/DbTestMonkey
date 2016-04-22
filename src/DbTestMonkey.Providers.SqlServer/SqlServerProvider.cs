namespace DbTestMonkey.Providers
{
   using System;
   using System.Configuration;
   using System.Data.SqlClient;
   using System.Data.SqlLocalDb;
   using DbTestMonkey.Contracts;
   using DbTestMonkey.Providers.SqlServer;

   /// <summary>
   /// SqlServer specific database provider for the DbTestMonkey library.
   /// </summary>
   public class SqlServerProvider : IDatabaseProvider<SqlConnection>
   {
      /// <summary>
      /// Gets or sets a logging delegate used to provide diagnostic logging capabilities.
      /// </summary>
      public Action<string> LogAction { get; set; }

      /// <summary>
      /// Gets a name that represents the configuration section where config for this provider
      /// will be found.
      /// </summary>
      public string ConfigurationSectionName 
      { 
         get
         {
            return "sqlServer";
         }
      }

      /// <summary>
      /// Reads the associated configuration for the database instance and sets it up if
      /// it is a localdb instance requiring initialization.
      /// </summary>
      public void InitialiseDatabaseServer()
      {
         ProviderConfiguration config =
            (ProviderConfiguration)ConfigurationManager.GetSection("dbTestMonkey/" + ConfigurationSectionName);

         if (config.IsLocalDbInstance)
         {
            var localDbApi = new SqlLocalDbApiWrapper();

            if (!localDbApi.IsLocalDBInstalled())
            {
               throw new InvalidOperationException("LocalDB is not installed on this machine.");
            }

            if (!string.IsNullOrWhiteSpace(config.LocalDbInstanceName))
            {
               LogAction("Preparing localdb instance: " + config.LocalDbInstanceName);
               var localDbProvider = new SqlLocalDbProvider();

               string localDbInstanceName = config.LocalDbInstanceName;
               
               var localDbInstance = localDbProvider.GetOrCreateInstance(localDbInstanceName);

               // TODO: This is questionable logic. Try and source the updated awesome localdb code
               //       that checks for physical and logical existence of databases.
               if (!localDbInstance.GetInstanceInfo().Exists)
               {
                  LogAction("Localdb instance doesn't yet exist so it will be created.");

                  localDbInstance.Stop();
                  SqlLocalDbInstance.Delete(localDbInstance);

                  localDbInstance = localDbProvider.GetOrCreateInstance(localDbInstanceName);
               }

               localDbInstance.Start();
            }
            else if (!string.IsNullOrWhiteSpace(config.ConnectionString))
            {
               LogAction("LocalDb instance name was not provided so we must assume it is already set up.");

               // SQL Server instance must already be set up.
               return;
            }
            else
            {
               throw new InvalidOperationException(
                  "IsLocalDbInstance was true in configuration but no instance name or connection string was configured.");
            }
         }
         else
         {
            // SQL Server instance must already be set up.
            LogAction("Configured SQL Server instance is not LocalDB. Assuming user has already created and started the instance.");

            return;
         }
      }

      /// <summary>
      /// Sets up a specific database by deploying the schema scripts.
      /// </summary>
      /// <param name="databaseName">The name of the database to deploy.</param>
      public void SetupDatabase(string databaseName)
      {
         new DacManager(CreateConnection, LogAction)
            .DeployDacPac(databaseName);
      }

      /// <summary>
      /// Creates a new open connection to the configured SQLServer instance.
      /// </summary>
      /// <returns>A newly opened SqlConnection.</returns>
      public SqlConnection CreateConnection()
      {
         ProviderConfiguration config =
            (ProviderConfiguration)ConfigurationManager.GetSection("dbTestMonkey/" + ConfigurationSectionName);

         if (string.IsNullOrWhiteSpace(config.ConnectionString) && !config.IsLocalDbInstance)
         {
            string errorMessage = 
               "Configured connection string was empty or whitespace and database has not been configured as localdb. " +
               "Connection string is required in this instance.";
            throw new InvalidOperationException(errorMessage);
         }

         SqlConnection connection = null;

         if (config.IsLocalDbInstance)
         {
            if (!string.IsNullOrWhiteSpace(config.ConnectionString))
            {
               connection = new SqlConnection(config.ConnectionString);
            }
            else if (!string.IsNullOrWhiteSpace(config.LocalDbInstanceName))
            {
               var localDbProvider = new System.Data.SqlLocalDb.SqlLocalDbProvider();

               connection = localDbProvider.GetInstance(config.LocalDbInstanceName).CreateConnection();
            }
            else
            {
               throw new InvalidOperationException(
                  "IsLocalDbInstance was true in configuration but no instance name or connection string was configured.");
            }

            connection.Open();
         }
         else
         {
            if (!string.IsNullOrWhiteSpace(config.ConnectionString))
            {
               connection = new SqlConnection(config.ConnectionString);
               connection.Open();
            }
            else
            {
               throw new InvalidOperationException(
                  "IsLocalDbInstance was false in configuration but no connection string was configured.");
            }
         }

         return connection;
      }

      /// <summary>
      /// Opens a new connection to a specific database instance on the configured SQLServer instance.
      /// </summary>
      /// <param name="databaseName">The name of the database to connect to.</param>
      /// <returns>A newly opened SqlConnection instance.</returns>
      public SqlConnection CreateConnection(string databaseName)
      {
         SqlConnection connection = CreateConnection();

         SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(connection.ConnectionString);
         builder.InitialCatalog = databaseName;

         connection.Close();
         connection.ConnectionString = builder.ToString();

         connection.Open();
         
         return connection;
      }

      /// <summary>
      /// Deletes the contents of all custom tables in the target database.
      /// </summary>
      /// <param name="databaseName">The name of the database to purge the contents of.</param>
      public void ExecutePreTestTasks(string databaseName)
      {
         using (var connection = CreateConnection(databaseName))
         using (var command = connection.CreateCommand())
         {
            command.CommandText = @"
                     EXEC sp_MSForEachTable ""SET QUOTED_IDENTIFIER ON; ALTER TABLE ? NOCHECK CONSTRAINT all;""
                     EXEC sp_MSForEachTable ""SET QUOTED_IDENTIFIER ON; DELETE FROM ?""
                     EXEC sp_MSForEachTable ""SET QUOTED_IDENTIFIER ON; ALTER TABLE ? WITH CHECK CHECK CONSTRAINT all""
                     EXEC sp_MSForEachTable N'
                        IF OBJECTPROPERTY(object_id(''?''), ''TableHasIdentity'') = 1 
                        BEGIN
                           IF 
                           (
                              SELECT TOP 1 last_value
                              FROM INFORMATION_SCHEMA.COLUMNS
                                 INNER JOIN sys.identity_columns
                                    ON name = COLUMN_NAME
                              WHERE COLUMNPROPERTY(object_id(N''?''), COLUMN_NAME, ''IsIdentity'') = 1
                           ) IS NULL
                           BEGIN
                              DBCC CHECKIDENT (N''?'', RESEED, 1)
                           END
                           ELSE
                           BEGIN
                              DBCC CHECKIDENT (N''?'', RESEED, 0)
                           END
                        END'";

            command.ExecuteNonQuery();

            ProviderConfiguration config =
               (ProviderConfiguration)ConfigurationManager.GetSection("dbTestMonkey/" + ConfigurationSectionName);

            // If the user has opted to run the post-deployment script before each test then
            // run it after clearing the data.
            if (config.Databases[databaseName].ExecutePostDeploymentScriptPerTest)
            {
               new DacManager(CreateConnection, LogAction)
                  .ExecutePostDeploymentScript(databaseName);
            }
         }
      }
   }
}