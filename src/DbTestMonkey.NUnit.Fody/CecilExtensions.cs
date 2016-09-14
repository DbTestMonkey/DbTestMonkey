namespace DbTestMonkey.NUnit.Fody
{
   using System.Linq;
   using Mono.Cecil;
   using Mono.Cecil.Cil;
   using Mono.Collections.Generic;

   /// <summary>
   /// Defines a series of extension methods for mono cecil which assist in the weaving process.
   /// </summary>
   public static class CecilExtensions
   {
      /// <summary>
      /// Determines whether a type contains a public instance method with OneTimeSetUpAttribute and returns it if so.
      /// </summary>
      /// <remarks>Also considers the deprecated TestFixtureSetUpAttribute which is synonymous.</remarks>
      /// <param name="type">The type of object to inspect.</param>
      /// <returns>A reference to the method if one was found. Null otherwise.</returns>
      public static MethodDefinition GetOneTimeSetUpMethod(this TypeDefinition type)
      {
         return type.Methods.FirstOrDefault(m => m.CustomAttributes.Any(a => 
            a.AttributeType.Name == "OneTimeSetUpAttribute" || a.AttributeType.Name == "TestFixtureSetUpAttribute"));
      }

      /// <summary>
      /// Determines whether a type contains a public instance method with OneTimeTearDownAttribute and returns it if so.
      /// </summary>
      /// <remarks>Also considers the deprecated TestFixtureTearDownAttribute which is synonymous.</remarks>
      /// <param name="type">The type of object to inspect.</param>
      /// <returns>A reference to the method if one was found. Null otherwise.</returns>
      public static MethodDefinition GetOneTimeTearDownMethod(this TypeDefinition type)
      {
         return type.Methods.FirstOrDefault(m => m.CustomAttributes.Any(a => 
            a.AttributeType.Name == "OneTimeTearDownAttribute" || a.AttributeType.Name == "TestFixtureTearDownAttribute"));
      }

      /// <summary>
      /// Determines whether a type contains a public instance method with SetUpAttribute and returns it if so.
      /// </summary>
      /// <param name="type">The type of object to inspect.</param>
      /// <returns>A reference to the method if one was found. Null otherwise.</returns>
      public static MethodDefinition GetSetUpMethod(this TypeDefinition type)
      {
         return type.Methods.FirstOrDefault(m => m.CustomAttributes.Any(a => a.AttributeType.Name == "SetUpAttribute"));
      }

      /// <summary>
      /// Determines whether a type contains a public instance method with TearDownAttribute and returns it if so.
      /// </summary>
      /// <param name="type">The type of object to inspect.</param>
      /// <returns>A reference to the method if one was found. Null otherwise.</returns>
      public static MethodDefinition GetTearDownMethod(this TypeDefinition type)
      {
         return type.Methods.FirstOrDefault(m => m.CustomAttributes.Any(a => a.AttributeType.Name == "TearDownAttribute"));
      }

      /// <summary>
      /// Determines whether a method is an instance constructor.
      /// </summary>
      /// <param name="methodDefinition">The method to inspect.</param>
      /// <returns>A flag indicating whether the method is an instance constructor.</returns>
      public static bool IsInstanceConstructor(this MethodDefinition methodDefinition)
      {
         return methodDefinition.IsConstructor && !methodDefinition.IsStatic;
      }

      /// <summary>
      /// Inserts a new instruction before another in a specified instruction collection.
      /// </summary>
      /// <param name="instructions">The collection of instructions to insert into.</param>
      /// <param name="target">The instruction that should be inserted before.</param>
      /// <param name="instruction">The instruction to insert.</param>
      public static void InsertBefore(this Collection<Instruction> instructions, Instruction target, Instruction instruction)
      {
         var index = instructions.IndexOf(target);
         instructions.Insert(index, instruction);
      }

      /// <summary>
      /// Determines whether a type implements a specific interface.
      /// </summary>
      /// <param name="type">The type of object to inspect.</param>
      /// <param name="interfaceFullName">The full name of the interface to look for.</param>
      /// <returns>A flag indicating whether the type implements the specified interface.</returns>
      public static bool HasInterface(this TypeReference type, string interfaceFullName)
      {
         if (type == null)
            return false;

         if (type.IsGenericParameter)
         {
            var genericType = (GenericParameter)type;
            return genericType.Constraints.Any(t => t.HasInterface(interfaceFullName));
         }

         var resolved = type.Resolve();

         if (resolved == null)
            return false;

         return
            (resolved.Interfaces != null && resolved.Interfaces.Any(i => i.FullName != null && i.FullName.Equals(interfaceFullName))) ||
            resolved.BaseType.HasInterface(interfaceFullName);
      }
   }
}