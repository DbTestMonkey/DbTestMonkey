namespace DbTestMonkey.NUnit.Tests
{
   using System;
   using System.Data;
   using System.Data.SqlClient;
   using DbTestMonkey.Contracts;
   using FluentAssertions;
   using global::NUnit.Framework;
   using System.Reflection;
   [TestFixture]
   public class NUnit_SqlServerProvider_Tests
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

      [Connection("TestDatabase1")]
      public string TestDb1ConnectionString { get; set; }

      [SetUp]
      public void TestSetup()
      {
         TestContext.Write("I'm setting up things.");
      }

      [Test]
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

      [Test]
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

      [Test]
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

      [Test]
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

      [Test]
      public void Connection_string_properties_should_be_populated()
      {
         using (var connection = new SqlConnection(TestDb1ConnectionString))
         {
            connection.Open();

            using (var command = connection.CreateCommand())
            {
               command.CommandText = "SELECT GetDate()";

               // Act.
               Action action = () =>
                  command.ExecuteNonQuery();

               // Assert.
               action.ShouldNotThrow();
               connection.Database.Should().Be("TestDatabase1");
            }
         }
      }

      [Test]
      public void Cross_database_reference_should_not_throw_exception()
      {
         using (var connection = new SqlConnection(TestDb1ConnectionString))
         using (var command = new SqlCommand("TestProcedure1", connection)
         {
            CommandType = CommandType.StoredProcedure
         })
         {
            connection.Open();

            // Act.
            Action action = () =>
               command.ExecuteNonQuery();

            // Assert.
            action.ShouldNotThrow();
            connection.Database.Should().Be("TestDatabase1");
         }
      }
   }
}