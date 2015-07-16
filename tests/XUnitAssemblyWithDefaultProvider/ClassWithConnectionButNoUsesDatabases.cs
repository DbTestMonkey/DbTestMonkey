namespace XUnitAssemblyWithDefaultProvider
{
   using System.Data;
   using DbTestMonkey.Contracts;

   public class ClassWithConnectionButNoUsesDatabases
   {
      [Connection]
      public IDbConnection Database1Connection { get; set; }
   }
}