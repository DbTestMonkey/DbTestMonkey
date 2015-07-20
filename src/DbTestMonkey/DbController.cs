namespace DbTestMonkey
{
   using System;
   using System.Collections.Generic;
   using System.Configuration;
   using System.Data;
   using System.Linq;
   using System.Reflection;
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

      public static void BeforeTestGroup(Type targetType, Action<string> logAction)
      {
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
         else if (dbAttributes.Any())
         {
            providerType = dbAttributes.First().ProviderType;
         }
         else
         {
            GlobalConfiguration globalConfig =
               (GlobalConfiguration)ConfigurationManager.GetSection("dbTestMonkey/global");

            providerType = globalConfig.DefaultDbProviderType;
         }
               
         var provider = Activator.CreateInstance(providerType) as IDatabaseProvider<IDbConnection>;
         provider.LogAction = logAction;
         provider.InitialiseDatabaseServer();

         List<string> alreadySetupDatabases = new List<string>();

         if (dbAttributes.Any())
         {
            // Set up all the databases defined in the UsesDatabaseAttribute.
            foreach (var databaseName in dbAttributes.First().Databases)
            {
               provider.SetupDatabase(databaseName);
               alreadySetupDatabases.Add(databaseName);
            }
         }

         // Set up all the databases defined in the application configuration file.
         ProviderConfigurationBase providerConfig =
            (ProviderConfigurationBase)ConfigurationManager.GetSection("dbTestMonkey/" + provider.ConfigurationSectionName);

         foreach (var databaseName in providerConfig.Databases
            .Cast<DatabaseConfiguration>()
            .Select(dc => dc.DatabaseName)
            .Where(dn => !alreadySetupDatabases.Any(asd => asd == dn)))
         {
            provider.SetupDatabase(databaseName);
            alreadySetupDatabases.Add(databaseName);
         }

         // Finally set up any remaining connections defined in the class.
         foreach (var connectionAttribute in targetType
            .GetProperties()
            .Where(pi => pi.CustomAttributes.Any(a => a.AttributeType.Name == "ConnectionAttribute"))
            .Select(pi => pi.GetCustomAttribute(typeof(ConnectionAttribute), true)))
         {
            var connAttribute = connectionAttribute as ConnectionAttribute;

            if (connAttribute.TargetDatabaseName != null &&
               !alreadySetupDatabases.Contains(connAttribute.TargetDatabaseName))
            {
               provider.SetupDatabase(connAttribute.TargetDatabaseName);
               alreadySetupDatabases.Add(connAttribute.TargetDatabaseName);
            }
         }
      }

      public static void AfterTestGroup(Type targetType, Action<string> logAction)
      {
      }

      /// <summary>
      /// Method called before a test is executed.
      /// </summary>
      /// <param name="sender">The object which called this method.</param>
      /// <param name="methodBase">Contextual information about the method which called this method.</param>
      public static void BeforeTest(object sender, MethodBase methodBase)
      {
         var dbAttributes = methodBase
            .DeclaringType
            .GetCustomAttributes(typeof(UsesDatabasesAttribute), true)
            .Cast<UsesDatabasesAttribute>();

         List<PropertyInfo> activeConnectionProperties = new List<PropertyInfo>();

         Type providerType = null;

         if (dbAttributes.Count() > 1)
         {
            throw new NotImplementedException(
               "Multiple UsesDatabaseAttributes have been defined on the test class. " +
               "Only one attribute is currently supported at this time.");
         }
         else if (dbAttributes.Any())
         {
            providerType = dbAttributes.First().ProviderType;
         }
         else
         {
            GlobalConfiguration globalConfig =
               (GlobalConfiguration)ConfigurationManager.GetSection("dbTestMonkey/global");

            if (globalConfig == null || globalConfig.DefaultDbProviderType == null)
            {
               throw new InvalidOperationException(
                  "No database provider was set in the UsesDatabaseAttribute constructor and no default has been set in configuration.");
            }

            providerType = globalConfig.DefaultDbProviderType;
         }

         var provider = Activator.CreateInstance(providerType) as IDatabaseProvider<IDbConnection>;

         if (dbAttributes.Any())
         {
            // Set up connections for database defined in the UsesDatabasesAttribute.
            foreach (var database in dbAttributes.First().Databases)
            {
               var connectionProperty = FindBestMatchConnectionProperty(sender, database);

               if (connectionProperty != null)
               {
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

            if (connAttribute.TargetDatabaseName != null)
            {
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
            ProviderConfigurationBase providerConfig =
               (ProviderConfigurationBase)ConfigurationManager.GetSection("dbTestMonkey/" + provider.ConfigurationSectionName);

            var databases = providerConfig.Databases.Cast<DatabaseConfiguration>();

            if (databases.Any(d => d.ConnectionPropertyName == prop.Name))
            {
               string databaseName = databases.FirstOrDefault(d => d.ConnectionPropertyName == prop.Name).DatabaseName;

               if (databaseName != null)
               {
                  SetConnectionProperty(sender, activeConnectionProperties, provider, databaseName, prop);
               }
            }
         }
      }

      private static void SetConnectionProperty(
         object sender, 
         List<PropertyInfo> activeConnectionProperties, 
         IDatabaseProvider<IDbConnection> provider, 
         string database, 
         PropertyInfo connectionProperty)
      {
         if (typeof(IDbConnection).IsAssignableFrom(connectionProperty.PropertyType))
         {
            // Property should be populated with an opened connection instance to the target database.
            IDbConnection newConnection = provider.CreateConnection(database);
            Connections.Add(newConnection);
            connectionProperty.SetValue(sender, newConnection);

            activeConnectionProperties.Add(connectionProperty);
         }
         else if (typeof(Func<IDbConnection>).IsAssignableFrom(connectionProperty.PropertyType))
         {
            // Property should be populated with a delegate that can provide a connection instance when requested.
            connectionProperty.SetValue(
               sender,
               (Func<IDbConnection>)(() => provider.CreateConnection(database)));

            activeConnectionProperties.Add(connectionProperty);
         }
         else if (connectionProperty.PropertyType == typeof(string))
         {
            // Property should be populated with a connection string that can be used to connect to the target database.
            using (var connection = provider.CreateConnection(database))
            {
               connectionProperty.SetValue(
                  sender,
                  connection.ConnectionString);
            }
         }
         else
         {
            throw new InvalidOperationException(
               "A connection was decorated with the DbTestMonkey.Contracts.ConnectionAttribute attribute but was not a type of System.Data.IDbConnection or System.Func<System.Data.IDbConnection>.");
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
      /// <param name="database">The name of the database the connection will be to.</param>
      /// <returns>A property information object containing the best match.</returns>
      private static PropertyInfo FindBestMatchConnectionProperty(object targetObject, string database)
      {
         var properties = targetObject.GetType().GetProperties(
            BindingFlags.SetProperty |
            BindingFlags.Public |
            BindingFlags.Instance |
            BindingFlags.IgnoreCase);

         // TODO: Put logic in place to be able to read mappings from application configuration.
         return properties.FirstOrDefault(prop =>
         {
            return prop.GetCustomAttributes().Cast<Attribute>().RequiresConnectionToDatabase(database) ||
               prop.Name == UppercaseFirstChar(database) + "Connection";
         });
      }

      /// <summary>
      /// Sets the first character of a string to be upper case to match
      /// proper property name style.
      /// </summary>
      /// <param name="rawString"></param>
      /// <returns></returns>
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
   }
}