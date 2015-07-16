namespace DbTestMonkey.TestAssists
{
   using System;
   using System.Collections.Generic;
   using System.IO;
   using System.Linq;
   using System.Reflection;
   using System.Text;
   using System.Threading.Tasks;
   using Mono.Cecil;
   using Mono.Cecil.Pdb;

   public class ModuleWeaverTestHelper<TModuleWeaver> where TModuleWeaver : class, new()
   {
      public string BeforeAssemblyPath;
      public string AfterAssemblyPath;
      public List<string> Errors;

      public ModuleWeaverTestHelper(
         string inputAssembly, 
         string inputAssemblyDirectory = null,
         bool modifyOriginalBinaries = false)
      {
         if (inputAssemblyDirectory == null)
         {
            inputAssemblyDirectory = @"..\..\..\" + Path.GetFileNameWithoutExtension(inputAssembly) + @"\bin\";
         }
         
#if (DEBUG)
         BeforeAssemblyPath = Path.GetFullPath(Path.Combine(inputAssemblyDirectory, "Debug", inputAssembly));
#else
         BeforeAssemblyPath = Path.GetFullPath(Path.Combine(inputAssemblyDirectory, "Release", inputAssembly));
#endif
         AfterAssemblyPath = BeforeAssemblyPath.Replace(".dll", "2.dll");
         
         string oldPdb = BeforeAssemblyPath.Replace(".dll", ".pdb");
         string newPdb = null;

         if (modifyOriginalBinaries)
         {
            newPdb = oldPdb;
            AfterAssemblyPath = BeforeAssemblyPath;
         }
         else
         {
            newPdb = oldPdb.Replace(".pdb", "2.pdb");

            File.Copy(oldPdb, newPdb, true);
            File.Copy(BeforeAssemblyPath, AfterAssemblyPath, true);
         }

         Errors = new List<string>();

         var assemblyResolver = new MockAssemblyResolver
         {
            Directory = Path.GetDirectoryName(AfterAssemblyPath)
         };

         using (var symbolStream = File.OpenRead(newPdb))
         {
            var readerParameters = new ReaderParameters
            {
               ReadSymbols = true,
               SymbolStream = symbolStream,
               SymbolReaderProvider = new PdbReaderProvider()
            };

            var moduleDefinition = ModuleDefinition.ReadModule(AfterAssemblyPath, readerParameters);

            dynamic weavingTask = new TModuleWeaver();

            Action<string> errorAction = s => 
               Errors.Add(s);

            weavingTask.ModuleDefinition = moduleDefinition;
            weavingTask.AssemblyResolver = assemblyResolver;
            weavingTask.LogError = errorAction;

            weavingTask.Execute();
            moduleDefinition.Write(AfterAssemblyPath);

            ModuleDefinition = moduleDefinition;
         }
      }

      public ModuleDefinition ModuleDefinition { get; private set; }
   }
}
