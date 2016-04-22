namespace DbTestMonkey.Providers
{
   using System;
   using System.Collections.Generic;
   using System.Configuration;
   using System.Data.SqlClient;
   using System.Data.SqlLocalDb;
   using DbTestMonkey.Contracts;
   using DbTestMonkey.Providers.SqlServer;
   using System.Linq;

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
            SetupLocalDbInstance(config);
         }
         else
         {
            // SQL Server instance must already be set up.
            LogAction("Configured SQL Server instance is not LocalDB. Assuming user has already created and started the instance.");

            return;
         }
      }

      private void SetupLocalDbInstance(ProviderConfiguration config)
      {
         var localDbApi = new SqlLocalDbApiWrapper();

         if (!localDbApi.IsLocalDBInstalled())
         {
            throw new InvalidOperationException("LocalDB is not installed on this machine.");
         }

         if (!string.IsNullOrWhiteSpace(config.LocalDbInstanceName))
         {
            LogAction("Preparing LocalDb instance: " + config.LocalDbInstanceName);

            ISqlLocalDbInstance localDbInstance =
               GetAppropriateLocalDbInstance(
                  localDbApi,
                  config.LocalDbInstanceName,
                  config.Versions
                        .Cast<LocalDbAllowedVersion>()
                        .Select(v => v.Version));

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

      private ISqlLocalDbInstance GetAppropriateLocalDbInstance(
         SqlLocalDbApiWrapper localDbApi,
         string localDbInstanceName, 
         IEnumerable<string> allowedLocalDbVersions)
      {
         SqlLocalDbProvider localDbProvider = new SqlLocalDbProvider();

         SqlLocalDbInstance existingInstance = GetExistingInstance(localDbProvider, localDbInstanceName);

         if (existingInstance != null)
         {
            return existingInstance;
         }

         // No versions configured so just create an instance without specifying a version.
         // This will create an instance using the latest version.
         if (!allowedLocalDbVersions.Any())
         {
            SqlLocalDbInstance newInstance = localDbProvider.CreateInstance(localDbInstanceName);

            return newInstance;
         }

         // Order the version numbers so we try highest version to lowest version.
         IEnumerable<string> orderedVersionNumbers =
            allowedLocalDbVersions.OrderByDescending(version => version);

         IEnumerable<string> installedVersions = localDbApi.Versions;

         foreach (string versionNumber in orderedVersionNumbers)
         {
            if (installedVersions.Contains(versionNumber))
            {
               localDbProvider.Version = versionNumber;

               SqlLocalDbInstance newInstance = localDbProvider.CreateInstance(localDbInstanceName);

               return newInstance;
            }
         }

         string errorMessage = 
$@"DbTestMonkey was unable to find an appropriate LocalDb version to use. The following versions were configured to be considered in the app.config by you:
   {string.Join(",", allowedLocalDbVersions.Select(s => "\"" + s + "\""))}
However only the following LocalDb versions were found to be installed:
   {string.Join(",", installedVersions.Select(s => "\"" + s + "\""))}
Please correct this error by one of the following options:
 * Add an installed version of LocalDb into your projects app.config <allowedLocalDbVersions> element. You can find the installed versions by running ""sqllocaldb versions"" at the command line.
 * Remove the <allowedLocalDbVersions> element from your app.config which means DbTestMonkey will use the latest installed version of LocalDb on your machine.
 * Install a version of LocalDb that is configured in your app.config <allowedLocalDbVersions> element.";

         throw new InvalidOperationException(errorMessage);
      }

      private SqlLocalDbInstance GetExistingInstance(SqlLocalDbProvider localDbProvider, string localDbInstanceName)
      {
         try
         {
            SqlLocalDbInstance existingInstance = localDbProvider.GetInstance(localDbInstanceName);

            ISqlLocalDbInstanceInfo instanceInfo = existingInstance.GetInstanceInfo();

            if (!instanceInfo.Exists)
            {
               LogAction("Existing LocalDb instance with name " + localDbInstanceName + " was found but there was no associated physical files");
               LogAction("Deleting instance and will attempt to recreate");

               if (existingInstance.IsRunning)
               {
                  existingInstance.Stop();
               }

               SqlLocalDbInstance.Delete(existingInstance);
            }
            else if (!instanceInfo.ConfigurationCorrupt)
            {
               LogAction("Existing LocalDb instance with name " + localDbInstanceName + " was found to have corrupt configuration");
               LogAction("Deleting instance and will attempt to recreate");

               if (existingInstance.IsRunning)
               {
                  existingInstance.Stop();
               }

               SqlLocalDbInstance.Delete(existingInstance);
            }
            else
            {
               LogAction("Found existing LocalDb instance with name " + localDbInstanceName + " and will use it");
               LogAction("If you would like to delete this existing instance and let DbTestMonkey create a new instance run the following commands at a command line");
               LogAction("   sqllocaldb stop " + localDbInstanceName);
               LogAction("   sqllocaldb delete " + localDbInstanceName);

               return existingInstance;
            }
         }
         catch (InvalidOperationException)
         {
            LogAction("Existing LocalDb instance with name " + localDbInstanceName + " was not found");
         }

         return null;
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