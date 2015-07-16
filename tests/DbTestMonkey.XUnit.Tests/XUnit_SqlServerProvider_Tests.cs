namespace DbTestMonkey.XUnit.Tests
{
   using System;
   using System.Collections.Generic;
   using System.Data;
   using System.Data.SqlClient;
   using System.Reflection;
   using FluentAssertions;
   using DbTestMonkey;
   using DbTestMonkey.Contracts;
   using DbTestMonkey.Providers;
   using Xunit;
   using Xunit.Abstractions;

   //[UsesDatabases]
   public class XUnit_SqlServerProvider_Tests
   {
      [Connection]
      public Func<IDbConnection> FirstConnectionFunc { get; set; }

      [Connection]
      public IDbConnection TestDatabase2Connection { get; set; }

      // TODO: Change to Func<SqlConnection> and try to fix covariance issue.
      [Connection("TestDatabase1")]
      public Func<IDbConnection> TestDb1ConnectionFunc { get; set; }

      [Connection("TestDatabase2")]
      public SqlConnection TestDb2Connection { get; set; }

      [Fact]
      public void Connection_properties_should_be_populated()
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

      [Fact]
      public void Connection_func_properties_should_be_populated()
      {
         // Arrange.
         using (var connection = FirstConnectionFunc())
         using (var command = connection.CreateCommand())
         {
            command.CommandText = "SELECT GetDate()";

            // Act.
            Action action = () =>
               command.ExecuteNonQuery();

            // Assert.
            action.ShouldNotThrow();
         }
      }

      [Fact]
      public void SqlServer_properties_should_be_populated_if_they_have_an_explicit_db_name()
      {
         // Arrange.
         using (var command = TestDb2Connection.CreateCommand())
         {
            command.CommandText = "SELECT GetDate()";

            // Act.
            Action action = () =>
               command.ExecuteNonQuery();

            // Assert.
            action.ShouldNotThrow();
         }
      }

      [Fact]
      public void SqlServer_func_properties_should_be_populated_if_they_have_an_explicit_db_name()
      {
         // Arrange.
         using (var connection = TestDb1ConnectionFunc())
         using (var command = connection.CreateCommand())
         {
            command.CommandText = "SELECT GetDate()";

            // Act.
            Action action = () =>
               command.ExecuteNonQuery();

            // Assert.
            action.ShouldNotThrow();
         }
      }

      [Fact]
      public void PostDeployment_scripts_should_be_executed()
      {
         // Arrange.
         using (var connection = FirstConnectionFunc())
         using (var command = connection.CreateCommand())
         {
            command.CommandText = "SELECT TOP 1 Name FROM dbo.TestTable";

            // Act.
            string postDeployResult = (string)command.ExecuteScalar();

            // Assert.
            postDeployResult.Should().Be("PostDeployment");
         }
      }
   }
}