namespace XUnitAssemblyNoConfig
{
   using System;
   using System.Data;
   using DbTestMonkey.Contracts;
   using Xunit;

   public class ClassWithExistingConstructorAndClassFixture : IClassFixture<ArbitraryFixtureClass>
   {
      private readonly ArbitraryFixtureClass _arbObj;

      public ClassWithExistingConstructorAndClassFixture(ArbitraryFixtureClass arbObj)
      {
         if (arbObj == null)
         {
            throw new ArgumentNullException();
         }

         _arbObj = arbObj;
      }

      [Connection]
      public IDbConnection Database1Connection { get; set; }
   }

   public class ArbitraryFixtureClass
   {

   }
}