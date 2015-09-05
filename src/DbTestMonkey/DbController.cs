namespace DbTestMonkey
{
   using System;
   using System.Collections.Generic;
   using System.Configuration;
   using System.Data;
   using System.Linq;
   using System.Reflection;
   using System.Threading.Tasks;
   using DbTestMonkey.Contracts;

   /// <summary>
   /// Defines a class that contains core database setup/teardown logic which is provider
   /// agnostic. It should be highly configurable on a per assembly basis.
   /// </summary>
   public static class DbController
   {
      /// <summary>
      /// Defines a collection which is used to hold connections for the current thread.
      /// Set as thread static so that many tests can run in parallel without threading issues.
      /// </summary>
      [ThreadStatic]
      private static IList<IDbConnection> connections;

      /// <summary>
      /// Gets an internal collection which is used to hold connections for the current thread.
      /// Private property is required for thread specific static initialisation.
      /// </summary>
      private static IList<IDbConnection> Connections
      {
         get
         {
            if (connections == null)
            {
               connections = new List<IDbConnection>();
            }

            return connections;
         }
      }

      /// <summary>
      /// Performs pre-test batch processing such as setting up database instances and deploying
      /// databases.
      /// </summary>
      /// <param name="targetType">The type of class running tests that requested the set up.</param>
      /// <param name="logAction">A diagnostic delegate used for logging messages to the output window.</param>
      public static void BeforeTestGroup(Type targetType, Action<string> logAction)
      {
         logAction("Executing pre-test group DbTestMonkey actions.");

         GlobalConfiguration globalConfig =
            (GlobalConfiguration)ConfigurationManager.GetSection("dbTestMonkey/global");

         var dbAttributes = targetType
            .GetCustomAttributes(typeof(UsesDatabasesAttribute), true)
            .Cast<UsesDatabasesAttribute>();

         Type providerType = null;

         if (dbAttributes.Count() > 1)
         {
            throw new NotImplementedException(
               "Multiple UsesDatabaseAttributes have been defined on the test class. " + 
               "Only one attribute is currently supported at this time.");
         }
         else if (globalConfig != null && globalConfig.DeployDatabasesEachClass && dbAttributes.Any())
         {
            // Only use this if databases are being deployed each class. It's not safe otherwise.
            providerType = dbAttributes.First().ProviderType;
            logAction("Using the provider type " + providerType.Name + " as configured in the UsesDatabasesAttribute.");
         }
         else
         {
            if (globalConfig == null || string.IsNullOrWhiteSpace(globalConfig.DefaultDbProvider))
            {
               throw new InvalidOperationException(
                  "DefaultDbProvider global configuration setting has not been configured but it is required for the current configuration.");
            }
            else
            {
               providerType = globalConfig.DefaultDbProviderType;

               logAction("Using the provider type " + providerType.Name + " as configured in app.config.");
            }
         }
               
         var provider = Activator.CreateInstance(providerType) as IDatabaseProvider<IDbConnection>;

         if (provider == null)
         {
            throw new InvalidOperationException(
               "Provider type " + providerType.FullName + 
               " does not implement the DbTestMonkey.Contracts.IDatabaseProvider<IDbConnection> interface.");
         }

         provider.LogAction = logAction;
         provider.InitialiseDatabaseServer();

         logAction("Database server has been successfully initialised. Databases will now be set up.");

         // Set up each of the configured databases.
         ExecuteActionForAllDatabases(targetType, dbAttributes, provider, dbName => provider.SetupDatabase(dbName));

         logAction("Databases have been successfully set up.");
      }

      /// <summary>
      /// Performs post-test batch processing. No actions currently exist here and it is established for future
      /// expansion only.
      /// </summary>
      /// <param name="targetType">The type of class running tests that requested the tear down.</param>
      /// <param name="logAction">A diagnostic delegate used for logging messages to the output window.</param>
      public static void AfterTestGroup(Type targetType, Action<string> logAction)
      {
         logAction("Executing post-test group DbTestMonkey actions.");
      }

      /// <summary>
      /// Method called before a test is executed.
      /// </summary>
      /// <param name="sender">The object which called this method.</param>
      /// <param name="methodBase">Contextual information about the method which called this method.</param>
      public static void BeforeTest(object sender, MethodBase methodBase, Action<string> logAction)
      {
         logAction("Preparing to execute before test DbTestMonkey actions.");

         var dbAttributes = methodBase
            .DeclaringType
            .GetCustomAttributes(typeof(UsesDatabasesAttribute), true)
            .Cast<UsesDatabasesAttribute>();

         List<PropertyInfo> activeConnectionProperties = new List<PropertyInfo>();

         Type providerType = null;

         GlobalConfiguration globalConfig =
            (GlobalConfiguration)ConfigurationManager.GetSection("dbTestMonkey/global");

         if (dbAttributes.Count() > 1)
         {
            throw new NotImplementedException(
               "Multiple UsesDatabaseAttributes have been defined on the test class. " +
               "Only one attribute is currently supported at this time.");
         }
         else if (globalConfig != null && globalConfig.DeployDatabasesEachClass && dbAttributes.Any())
         {
            providerType = dbAttributes.First().ProviderType;
            logAction("Using the provider type " + providerType.Name + " as configured in the UsesDatabasesAttribute.");
         }
         else
         {
            if (globalConfig == null || string.IsNullOrWhiteSpace(globalConfig.DefaultDbProvider))
            {
               throw new InvalidOperationException(
                  "DefaultDbProvider global configuration setting has not been configured but it is required for the current configuration.");
            }

            providerType = globalConfig.DefaultDbProviderType;
            logAction("Using the provider type " + providerType.Name + " as configured in app.config.");
         }

         var provider = Activator.CreateInstance(providerType) as IDatabaseProvider<IDbConnection>;

         if (provider == null)
         {
            throw new InvalidOperationException(
               "Provider type " + providerType.FullName +
               " does not implement the DbTestMonkey.Contracts.IDatabaseProvider<IDbConnection> interface.");
         }

         provider.LogAction = logAction;

         logAction(
            "Provider has been successfully created. Preparing to clear database and run pre-deployment scripts if required.");

         // Clear all the existing data out of the configured databases and re-seed base data.
         ExecuteActionForAllDatabases(sender.GetType(), dbAttributes, provider, dbName => provider.ExecutePreTestTasks(dbName));

         logAction("Databases have successfully been cleared and potentially re-seeded. Connections will now be set up.");

         ProviderConfigurationBase providerConfig =
            (ProviderConfigurationBase)ConfigurationManager.GetSection("dbTestMonkey/" + provider.ConfigurationSectionName);

         if (dbAttributes.Any())
         {
            // Set up connections for database defined in the UsesDatabasesAttribute.
            foreach (var database in dbAttributes.First().Databases)
            {
               var connectionProperty = FindBestMatchConnectionProperty(sender, database, providerConfig);

               if (connectionProperty != null)
               {
                  logAction("Populating connection property " + connectionProperty.Name + " as configured in the UsesDatabaseAttribute.");

                  SetConnectionProperty(sender, activeConnectionProperties, provider, database, connectionProperty);
               }
            }
         }

         // Set up any remaining connections that have target database names defined in a ConnectionAttribute.
         foreach (var connectionProperty in methodBase.DeclaringType
            .GetProperties()
            .Where(pi =>
               !activeConnectionProperties.Contains(pi) &&
               pi.CustomAttributes.Any(a => a.AttributeType.Name == "ConnectionAttribute")))
         {
            var connAttribute = connectionProperty.GetCustomAttribute(typeof(ConnectionAttribute), true) as ConnectionAttribute;

            if (connAttribute != null && connAttribute.TargetDatabaseName != null)
            {
               logAction("Populating connection property " + connectionProperty.Name + " as configured in the ConnectionAttribute.");

               SetConnectionProperty(
                  sender,
                  activeConnectionProperties,
                  provider,
                  connAttribute.TargetDatabaseName,
                  connectionProperty);
            }
         }

         // Get all the properties that haven't yet been populated and populate them.
         foreach (var prop in methodBase.DeclaringType.GetProperties().Where(p => !activeConnectionProperties.Any(c => c.Name == p.Name)))
         {
            var databases = providerConfig.Databases.Cast<DatabaseConfiguration>();

            if (databases.Any(d => d.ConnectionPropertyName == prop.Name))
            {
               string databaseName = databases.FirstOrDefault(d => d.ConnectionPropertyName == prop.Name).DatabaseName;

               if (databaseName != null)
               {
                  logAction("Populating connection property " + prop.Name + " as configured in app.config.");

                  SetConnectionProperty(sender, activeConnectionProperties, provider, databaseName, prop);
               }
            }
         }
      }

      /// <summary>
      /// Takes a property and database provider, then determines how to populate that property
      /// based on type.      
      /// </summary>
      /// <param name="sender">The object in which the connection property resides.</param>
      /// <param name="activeConnectionProperties">
      /// A collection of properties that have already been populated.</param>
      /// <param name="provider">The database provider that will be used for fetching connections.</param>
      /// <param name="databaseName">The name of the database that a connection should be made to.</param>
      /// <param name="connectionProperty">The property that needs to be populated on the sender object.</param>
      private static void SetConnectionProperty(
         object sender, 
         List<PropertyInfo> activeConnectionProperties, 
         IDatabaseProvider<IDbConnection> provider, 
         string databaseName, 
         PropertyInfo connectionProperty)
      {
         if (typeof(IDbConnection).IsAssignableFrom(connectionProperty.PropertyType))
         {
            // Property should be populated with an opened connection instance to the target database.
            IDbConnection newConnection = provider.CreateConnection(databaseName);
            Connections.Add(newConnection);
            connectionProperty.SetValue(sender, newConnection);

            activeConnectionProperties.Add(connectionProperty);
         }
         else if (typeof(Func<IDbConnection>).IsAssignableFrom(connectionProperty.PropertyType))
         {
            // Property should be populated with a delegate that can provide a connection instance when requested.
            connectionProperty.SetValue(
               sender,
               (Func<IDbConnection>)(() => provider.CreateConnection(databaseName)));

            activeConnectionProperties.Add(connectionProperty);
         }
         else if (connectionProperty.PropertyType == typeof(string))
         {
            // Property should be populated with a connection string that can be used to connect to the target database.
            using (var connection = provider.CreateConnection(databaseName))
            {
               connectionProperty.SetValue(
                  sender,
                  connection.ConnectionString);
            }
         }
         else
         {
            throw new InvalidOperationException(
               "A connection was decorated with the DbTestMonkey.Contracts.ConnectionAttribute " +
               "attribute but was not a type of System.Data.IDbConnection or System.Func<System.Data.IDbConnection>.");
         }
      }

      /// <summary>
      /// Method called after a test is executed.
      /// </summary>
      /// <param name="methodBase">Contextual information about the method which called this method.</param>
      public static void AfterTest(MethodBase methodBase)
      {
         for (int i = 0; i < Connections.Count; i++)
         {
            if (Connections[i] != null)
            {
               Connections[i].Dispose();
               Connections.Remove(Connections[i]);
            }
         }
      }

      /// <summary>
      /// Examines an object to determine the correct property to populate with a database connection.
      /// </summary>
      /// <param name="targetObject">The object to examine for best match properties.</param>
      /// <param name="databaseName">The name of the database the connection will be to.</param>
      /// <param name="providerConfig">
      /// An object representing the app.config configuration for the current provider.</param>
      /// <returns>A property information object containing the best match.</returns>
      private static PropertyInfo FindBestMatchConnectionProperty(
         object targetObject, 
         string databaseName, 
         ProviderConfigurationBase providerConfig)
      {
         var properties = targetObject.GetType().GetProperties(
            BindingFlags.SetProperty |
            BindingFlags.Public |
            BindingFlags.Instance |
            BindingFlags.IgnoreCase);

         string preConfiguredConnectionPropertyName = null;

         if (providerConfig.Databases.Cast<DatabaseConfiguration>()
            .Any(d => d.DatabaseName == databaseName && !string.IsNullOrWhiteSpace(d.ConnectionPropertyName)))
         {
            preConfiguredConnectionPropertyName = 
               providerConfig.Databases.Cast<DatabaseConfiguration>().First().ConnectionPropertyName;
         }

         return properties.FirstOrDefault(prop =>
         {
            if (string.IsNullOrWhiteSpace(preConfiguredConnectionPropertyName))
            {
               return prop.GetCustomAttributes().Cast<Attribute>().RequiresConnectionToDatabase(databaseName) ||
                  prop.Name == UppercaseFirstChar(databaseName) + "Connection";
            }
            else
            {
               return prop.Name == preConfiguredConnectionPropertyName;
            }
         });
      }

      /// <summary>
      /// Sets the first character of a string to be upper case to match
      /// proper property name style.
      /// </summary>
      /// <param name="rawString">The string to have the first character uppercased in.</param>
      /// <returns>The raw string with the first character upper cased.</returns>
      private static string UppercaseFirstChar(string rawString)
      {
         // Exit out early if the target string is empty.
         if (string.IsNullOrEmpty(rawString))
         {
            return string.Empty;
         }

         // Return the post-processed string.
         return char.ToUpper(rawString[0]) + rawString.Substring(1);
      }

      /// <summary>
      /// Performs an action for all the databases configured from all sources.
      /// </summary>
      /// <param name="targetType">The type of the class currently running a test.</param>
      /// <param name="dbAttributes">
      /// All the <see cref="UsesDatabasesAttribute"/> attributes on the target class.</param>
      /// <param name="provider">The current provider for which connections are being provided through.</param>
      /// <param name="action">The action to execute for each database.</param>
      private static void ExecuteActionForAllDatabases(
         Type targetType, 
         IEnumerable<UsesDatabasesAttribute> dbAttributes,
         IDatabaseProvider<IDbConnection> provider,
         Action<string> action)
      {
         List<Action> dbSetupActions = new List<Action>();
         List<string> alreadyActionedDatabases = new List<string>();

         if (dbAttributes.Any())
         {
            // Perform the action for all the databases defined in the UsesDatabaseAttribute.
            foreach (var databaseName in dbAttributes.First().Databases)
            {
               dbSetupActions.Add(() => action(databaseName));
               alreadyActionedDatabases.Add(databaseName);
            }
         }

         // Perform the action for all the databases defined in the application configuration file.
         ProviderConfigurationBase providerConfig =
            (ProviderConfigurationBase)ConfigurationManager.GetSection("dbTestMonkey/" + provider.ConfigurationSectionName);

         foreach (var databaseName in providerConfig.Databases
            .Cast<DatabaseConfiguration>()
            .Select(dc => dc.DatabaseName)
            .Where(dn => !alreadyActionedDatabases.Any(asd => asd == dn)))
         {
            dbSetupActions.Add(() => action(databaseName));
            alreadyActionedDatabases.Add(databaseName);
         }

         // Finally perform the action for all databases defined on connection properties in the class.
         foreach (var connectionAttribute in targetType
            .GetProperties()
            .Where(pi => pi.CustomAttributes.Any(a => a.AttributeType.Name == "ConnectionAttribute"))
            .Select(pi => pi.GetCustomAttribute(typeof(ConnectionAttribute), true)))
         {
            var connAttribute = connectionAttribute as ConnectionAttribute;

            if (connAttribute.TargetDatabaseName != null &&
               !alreadyActionedDatabases.Contains(connAttribute.TargetDatabaseName))
            {
               dbSetupActions.Add(() => action(connAttribute.TargetDatabaseName));
               alreadyActionedDatabases.Add(connAttribute.TargetDatabaseName);
            }
         }

         GlobalConfiguration globalConfig =
            (GlobalConfiguration)ConfigurationManager.GetSection("dbTestMonkey/global");

         if (globalConfig.UseParallelInitialisation)
         {
            Parallel.Invoke(dbSetupActions.ToArray());
         }
         else
         {
            dbSetupActions.ForEach(act => act.Invoke());
         }         
      }
   }
}