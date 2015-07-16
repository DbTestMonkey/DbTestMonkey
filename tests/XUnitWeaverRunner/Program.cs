namespace XUnitWeaverRunner
{
   using System;
   using DbTestMonkey.TestAssists;
   using DbTestMonkey.XUnit.Fody;

   /// <summary>
   /// This console app is run as a post-build step of the DbTestMonkey.XUnit.Tests
   /// assembly to ensure it is weaved before any tests are executed.
   /// </summary>
   public class Program
   {
      private static void Main(string[] args)
      {
         var testHelper = new ModuleWeaverTestHelper<ModuleWeaver>(
            "DbTestMonkey.XUnit.Tests.dll",
            modifyOriginalBinaries: true);

         if (testHelper.Errors.Count != 0)
         {
            foreach (var err in testHelper.Errors)
            {
               Console.WriteLine(err);
            }

            throw new InvalidOperationException(
               "Failed to weave DbTestMonkey.XUnit.Tests assembly. Number of errors: " + testHelper.Errors.Count);
         }
      }
   }
}