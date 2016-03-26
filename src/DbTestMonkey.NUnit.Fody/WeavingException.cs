namespace DbTestMonkey.NUnit.Fody
{
   using System;
   using System.Runtime.Serialization;

   /// <summary>
   /// Defines an exception that should be thrown back to Fody when
   /// an exceptional condition occurs inside weaving code.
   /// </summary>
   [Serializable]
   public class WeavingException : Exception
   {
      /// <summary>
      /// Initializes a new instance of the WeavingException class.
      /// </summary>
      public WeavingException()
      {
      }

      /// <summary>
      /// Initializes a new instance of the WeavingException class.
      /// </summary>
      /// <param name="message">The message to include with the exception.</param>
      public WeavingException(string message)
         : base(message)
      {
      }

      /// <summary>
      /// Initializes a new instance of the WeavingException class.
      /// </summary>
      /// <param name="message">The message to include with the exception.</param>
      /// <param name="inner">Any additional inner exception to give further context.</param>
      public WeavingException(string message, Exception inner)
         : base(message, inner)
      {
      }

      /// <summary>
      /// Initializes a new instance of the WeavingException class.
      /// </summary>
      /// <param name="info">The System.Runtime.Serialization.SerializationInfo that holds the serialized
      /// object data about the exception being thrown.</param>
      /// <param name="context">The System.Runtime.Serialization.StreamingContext that contains contextual
      /// information about the source or destination.</param>
      protected WeavingException(SerializationInfo info, StreamingContext context)
         : base(info, context) 
      { 
      }
   }
}