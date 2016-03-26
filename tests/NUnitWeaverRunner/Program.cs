namespace NUnitWeaverRunner
{
   using System;
   using System.Diagnostics;
   using DbTestMonkey.TestAssists;
   using DbTestMonkey.NUnit.Fody;

   /// <summary>
   /// This console app is run as a post-build step of the DbTestMonkey.XUnit.Tests
   /// assembly to ensure it is weaved before any tests are executed.
   /// </summary>
   public class Program
   {
      private static void Main(string[] args)
      {
         var testHelper = new ModuleWeaverTestHelper<ModuleWeaver>(
            "DbTestMonkey.NUnit.Tests.dll",
            modifyOriginalBinaries: true);

         // Keeping this zombie code in for diagnostic purposes in future.
         // MSBuild task fails if I leave it in.

         /*
         Console.WriteLine("Weaving informational messages:");

         foreach (var msg in testHelper.InfoMessages)
         {
            Console.WriteLine(msg);
         }

         Console.WriteLine("Weaving error messages:");
         */

         if (testHelper.Errors.Count != 0)
         {
            foreach (var err in testHelper.Errors)
            {
               Console.WriteLine(err);
            }

            throw new InvalidOperationException(
               "Failed to weave DbTestMonkey.NUnit.Tests assembly. Number of errors: " + testHelper.Errors.Count);
         }
      }
   }
}