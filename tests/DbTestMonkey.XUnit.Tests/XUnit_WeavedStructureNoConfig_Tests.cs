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
   }
}