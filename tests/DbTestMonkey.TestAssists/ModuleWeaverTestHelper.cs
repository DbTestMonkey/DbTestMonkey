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
      private string _beforeAssemblyPath;

      private string _afterAssemblyPath;

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
         _beforeAssemblyPath = Path.GetFullPath(Path.Combine(inputAssemblyDirectory, "Debug", inputAssembly));
#else
         _beforeAssemblyPath = Path.GetFullPath(Path.Combine(inputAssemblyDirectory, "Release", inputAssembly));
#endif
         _afterAssemblyPath = _beforeAssemblyPath.Replace(".dll", "2.dll");
         
         string oldPdb = _beforeAssemblyPath.Replace(".dll", ".pdb");
         string newPdb = null;

         if (modifyOriginalBinaries)
         {
            newPdb = oldPdb;
            _afterAssemblyPath = _beforeAssemblyPath;
         }
         else
         {
            newPdb = oldPdb.Replace(".pdb", "2.pdb");

            File.Copy(oldPdb, newPdb, true);
            File.Copy(_beforeAssemblyPath, _afterAssemblyPath, true);
         }

         Errors = new List<string>();
         InfoMessages = new List<string>();

         var assemblyResolver = new MockAssemblyResolver
         {
            Directory = Path.GetDirectoryName(_afterAssemblyPath)
         };

         using (var symbolStream = File.OpenRead(newPdb))
         {
            var readerParameters = new ReaderParameters
            {
               ReadSymbols = true,
               SymbolStream = symbolStream,
               SymbolReaderProvider = new PdbReaderProvider()
            };

            var moduleDefinition = ModuleDefinition.ReadModule(_afterAssemblyPath, readerParameters);

            dynamic weavingTask = new TModuleWeaver();

            Action<string> errorAction = s => 
               Errors.Add(s);

            Action<string> infoAction = s =>
               InfoMessages.Add(s);

            weavingTask.ModuleDefinition = moduleDefinition;
            weavingTask.AssemblyResolver = assemblyResolver;
            weavingTask.LogError = errorAction;
            weavingTask.LogInfo = infoAction;

            weavingTask.Execute();
            moduleDefinition.Write(_afterAssemblyPath);

            ModuleDefinition = moduleDefinition;
         }
      }

      public List<string> Errors { get; private set; }

      public List<string> InfoMessages { get; private set; }

      public ModuleDefinition ModuleDefinition { get; private set; }
   }
}
