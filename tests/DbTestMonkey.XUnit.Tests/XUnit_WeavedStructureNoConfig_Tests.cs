namespace DbTestMonkey.TestsRunner
{
   using System.Linq;
   using FluentAssertions;
   using DbTestMonkey;
   using DbTestMonkey.TestAssists;
   using DbTestMonkey.XUnit.Fody;
   using Xunit;
   using Xunit.Abstractions;

   public class XUnit_WeavedStructureNoConfig_Tests
   {
      [Fact]
      public void Empty_classes_with_usesdatabasesattribute_should_receive_correct_structure()
      {
         // Arrange.

         // Act.
         var testHelper =
            new ModuleWeaverTestHelper<ModuleWeaver>("XUnitAssemblyNoConfig.dll");

         // Assert.
         testHelper.Errors.Count.Should().Be(0);
         var type = testHelper.ModuleDefinition.GetType("XUnitAssemblyNoConfig.EmptyClassWithDefaultProvider");
         type.HasDisposeMethod().Should().BeTrue();

         var ctor = type.Methods.Single(m => m.IsConstructor);
         ctor.Parameters.Count.Should().Be(2);
         ctor.Parameters.First().ParameterType.FullName.Should().Be(typeof(DatabaseFixture).FullName);
         ctor.Parameters.Last().ParameterType.FullName.Should().Be(typeof(ITestOutputHelper).FullName);
      }

      [Fact]
      public void Empty_classes_with_connection_attributes_only_should_receive_correct_structure()
      {
         // Arrange.

         // Act.
         var testHelper =
            new ModuleWeaverTestHelper<ModuleWeaver>("XUnitAssemblyNoConfig.dll");

         // Assert.
         testHelper.Errors.Count.Should().Be(0);
         var type = testHelper.ModuleDefinition.GetType("XUnitAssemblyNoConfig.ClassWithConnectionButNoUsesDatabases");
         type.HasDisposeMethod().Should().BeTrue();

         var ctor = type.Methods.Single(m => m.IsConstructor);
         ctor.Parameters.Count.Should().Be(2);
         ctor.Parameters.First().ParameterType.FullName.Should().Be(typeof(DatabaseFixture).FullName);
         ctor.Parameters.Last().ParameterType.FullName.Should().Be(typeof(ITestOutputHelper).FullName);
      }

      [Fact]
      public void Classes_with_existing_constructor_and_class_fixture_should_have_constructor_merged()
      {
         // Arrange.

         // Act.
         var testHelper =
            new ModuleWeaverTestHelper<ModuleWeaver>("XUnitAssemblyNoConfig.dll");

         // Assert.
         testHelper.Errors.Count.Should().Be(0);
         var type = testHelper.ModuleDefinition.GetType("XUnitAssemblyNoConfig.ClassWithExistingConstructorAndClassFixture");
         type.HasDisposeMethod().Should().BeTrue();

         var ctor = type.Methods.Single(m => m.IsConstructor);
         ctor.Parameters.Count.Should().Be(3);
         ctor.Parameters.ElementAt(0).ParameterType.FullName.Should().Be("XUnitAssemblyNoConfig.ArbitraryFixtureClass");
         ctor.Parameters.ElementAt(1).ParameterType.FullName.Should().Be(typeof(DatabaseFixture).FullName);
         ctor.Parameters.ElementAt(2).ParameterType.FullName.Should().Be(typeof(ITestOutputHelper).FullName);

         type.Interfaces.ElementAt(0).FullName.Should().Be("Xunit.IClassFixture`1<XUnitAssemblyNoConfig.ArbitraryFixtureClass>");
         type.Interfaces.ElementAt(1).FullName.Should().Be("System.IDisposable");
         type.Interfaces.ElementAt(2).FullName.Should().Be("Xunit.IClassFixture`1<DbTestMonkey.DatabaseFixture>");
      }
   }
}