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
      private readonly ITestOutputHelper _outputHelper;

      public XUnit_WeavedStructureNoConfig_Tests(ITestOutputHelper outputHelper)
      {
         _outputHelper = outputHelper;
      }

      [Fact]
      public void Empty_classes_with_usesdatabasesattribute_should_receive_correct_structure()
      {
         // Arrange.

         // Act.
         var testHelper =
            new ModuleWeaverTestHelper<ModuleWeaver>("XUnitAssemblyNoConfig.dll");

         // Assert.
         testHelper.InfoMessages.ForEach(e => _outputHelper.WriteLine(e));
         testHelper.Errors.ForEach(e => _outputHelper.WriteLine(e));
         testHelper.Errors.Count.Should().Be(0);
         var type = testHelper.ModuleDefinition.GetType("XUnitAssemblyNoConfig.EmptyClassWithDefaultProvider");
         type.HasDisposeMethod().Should().BeTrue();

         var ctor = type.Methods.Single(m => m.IsConstructor);
         ctor.Parameters.Count.Should().Be(3);
         ctor.Parameters.ElementAt(0).ParameterType.FullName.Should().Be(typeof(ITestOutputHelper).FullName);
         ctor.Parameters.ElementAt(1).ParameterType.FullName.Should().Be(typeof(ClassDatabaseFixture).FullName);
         ctor.Parameters.ElementAt(2).ParameterType.FullName.Should().Be(typeof(CollectionDatabaseFixture).FullName);
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
         ctor.Parameters.Count.Should().Be(3);
         ctor.Parameters.ElementAt(0).ParameterType.FullName.Should().Be(typeof(ITestOutputHelper).FullName);
         ctor.Parameters.ElementAt(1).ParameterType.FullName.Should().Be(typeof(ClassDatabaseFixture).FullName);
         ctor.Parameters.ElementAt(2).ParameterType.FullName.Should().Be(typeof(CollectionDatabaseFixture).FullName);
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
         ctor.Parameters.Count.Should().Be(4);
         ctor.Parameters.ElementAt(0).ParameterType.FullName.Should().Be("XUnitAssemblyNoConfig.ArbitraryFixtureClass");
         ctor.Parameters.ElementAt(1).ParameterType.FullName.Should().Be(typeof(ITestOutputHelper).FullName);
         ctor.Parameters.ElementAt(2).ParameterType.FullName.Should().Be(typeof(ClassDatabaseFixture).FullName);
         ctor.Parameters.ElementAt(3).ParameterType.FullName.Should().Be(typeof(CollectionDatabaseFixture).FullName);

         type.Interfaces.Count().Should().Be(3);
         type.Interfaces.ElementAt(0).FullName.Should().Be("Xunit.IClassFixture`1<XUnitAssemblyNoConfig.ArbitraryFixtureClass>");
         type.Interfaces.ElementAt(1).FullName.Should().Be("System.IDisposable");
         type.Interfaces.ElementAt(2).FullName.Should().Be("Xunit.IClassFixture`1<DbTestMonkey.XUnit.Fody.ClassDatabaseFixture>");

         type.CustomAttributes.Count().Should().Be(1);
         type.CustomAttributes.ElementAt(0).AttributeType.FullName.Should().Be(typeof(CollectionAttribute).FullName);
      }
   }
}