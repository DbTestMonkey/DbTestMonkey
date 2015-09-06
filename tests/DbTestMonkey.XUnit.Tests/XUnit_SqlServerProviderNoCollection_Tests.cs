namespace DbTestMonkey.XUnit.Tests
{
   using System;
   using System.Data;
   using System.Data.SqlClient;
   using DbTestMonkey.Contracts;
   using FluentAssertions;
   using Xunit;

   public class XUnit_SqlServerProviderNoCollection_Tests
   {
      [Connection]
      public IDbConnection TestDatabase2Connection { get; set; }

      [Fact]
      public void Connection_properties_should_be_populated_when_a_dynamic_collection_is_used()
      {
         // Arrange.
         using (var command = TestDatabase2Connection.CreateCommand())
         {
            command.CommandText = "SELECT GetDate()";

            // Act.
            Action action = () =>
               command.ExecuteNonQuery();

            // Assert.
            action.ShouldNotThrow();
         }
      }
   }
}