namespace DbTestMonkey.Contracts
{
   using System;
   using System.Collections.Generic;
   using System.Configuration;
   using System.Data;
   using System.Data.SqlClient;
   using System.Linq;

   /// <summary>
   /// Defines an attribute that is used for marking test classes that need database connectivity.
   /// </summary>
   [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
   public class UsesDatabasesAttribute : Attribute
   {
      /// <summary>
      /// The type of provider that should be used for this particular database server instance.
      /// </summary>
      private readonly Type _providerType;

      /// <summary>
      /// A collection of database names that should be connected up to.
      /// </summary>
      private readonly IEnumerable<string> _databases;

      /// <summary>
      /// Initializes a new instance of the UsesDatabasesAttribute class.
      /// </summary>
      public UsesDatabasesAttribute()
      {
         GlobalConfiguration config = (GlobalConfiguration)ConfigurationManager.GetSection("dbTestMonkey/global");

         if (config == null || config.DefaultDbProviderType == null)
         {
            throw new InvalidOperationException(
               "No provider type was specified in the DbTestMonkey.Contracts.UsesDatabasesAttribute constructor and no default has been configured in the project App.config.");
         }
         else if (!typeof(IDatabaseProvider<IDbConnection>).IsAssignableFrom(config.DefaultDbProviderType))
         {
            throw new InvalidOperationException(
               "The default provider type specified in configuration was not a type of DbTestMonkey.Contracts.IDatabaseProvider<System.Data.IDbConnection>.");
         }
         else
         {
            _providerType = config.DefaultDbProviderType;
         }

         _databases = Enumerable.Empty<string>();
      }

      /// <summary>
      /// Initializes a new instance of the UsesDatabasesAttribute class.
      /// </summary>
      /// <param name="providerType">The type of database provider to use.</param>
      /// <param name="databases">A collection of database names to provide connections to.</param>
      public UsesDatabasesAttribute(Type providerType, params string[] databases)
      {
         if (!typeof(IDatabaseProvider<IDbConnection>).IsAssignableFrom(providerType))
         {
            throw new ArgumentException(
               "Provider type must be a type of DbTestMonkey.Contracts.IDatabaseProvider<System.Data.IDbConnection>.", 
               "providerType");
         }

         if (databases.Any(d => string.IsNullOrWhiteSpace(d)))
         {
            throw new ArgumentException(
               "One or more database names were null, empty or whitespace. If the database is not required then remove the argument.",
               "databases");
         }

         _providerType = providerType;
         _databases = databases;
      }

      /// <summary>
      /// Gets the provider type that should be used when initializing database connections.
      /// </summary>
      public Type ProviderType
      {
         get
         {
            return _providerType;
         }
      }

      /// <summary>
      /// Gets a collection of database names that connections should be provided for.
      /// </summary>
      public IEnumerable<string> Databases
      {
         get
         {
            return _databases;
         }
      }
   }
}