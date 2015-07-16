namespace DbTestMonkey.Contracts
{
   using System;
   using System.Data;

   /// <summary>
   /// Interface definition that describes a database connection provider.
   /// </summary>
   /// <typeparam name="T">
   /// Covariant type parameter defining the type of connection this provider will provide.</typeparam>
   public interface IDatabaseProvider<out T> where T : IDbConnection
   {
      string ConfigurationSectionName { get; }

      Action<string> LogAction { set; }

      void InitialiseDatabaseServer();

      void SetupDatabase(string databaseName);

      /// <summary>
      /// Factory method that will produce an instace of an IDbConnection ready for use.
      /// </summary>
      /// <returns>An open IDbConnection ready for use.</returns>
      T CreateConnection(string databaseName);
   }
}